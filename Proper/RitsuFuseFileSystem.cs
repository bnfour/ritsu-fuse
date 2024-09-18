using System.Diagnostics;
using System.Text;

using Mono.Fuse.NETStandard;
using Mono.Unix.Native;

using Bnfour.RitsuFuse.Proper.Utilities;

namespace Bnfour.RitsuFuse.Proper;

/// <summary>
/// A class to implement actual FUSE logic in.
/// </summary>
internal sealed class RitsuFuseFileSystem : FileSystem
{
    /// <summary>
    /// The time this instance was initialized.
    /// Used as folder's mtime&ctime and as fallback for other values
    /// if the FS is yet to be accessed.
    /// </summary>
    private readonly DateTimeOffset _fsCreationTimestamp;

    /// <summary>
    /// Settings instance. Assumed to be previously validated.
    /// </summary>
    private readonly RitsuFuseSettings _settings;

    /// <summary>
    /// List of the files in the target folder to make links to.
    /// </summary>
    private readonly List<string> _filenames;

    /// <summary>
    /// Name of the file that was a target before the current one.
    /// Used to prevent the same file returned two times in a row,
    /// if <see cref="RitsuFuseSettings.PreventRepeats"/> option is enabled.
    /// </summary>
    private string? _lastFile;

    /// <summary>
    /// Name of the file currently being a target.
    /// Used to keep track of it for subsequent requests.
    /// </summary>
    private string _currentFile;

    /// <summary>
    /// A queue to hold shuffled list of files yet to return.
    /// Used to enable <see cref="RitsuFuseSettings.UseQueue"/> option.
    /// </summary>
    private Queue<string>? _shuffledQueue;

    /// <summary>
    /// Time of last access to the link. If null -- no access yet.
    /// Reported as link's atime.
    /// If accessed later than <see cref="RitsuFuseSettings.Timeout"/>,
    /// new link target is selected. (Most real-world apps read the link more than once, so requests in rapid
    /// succession keep their target stable.)
    /// </summary>
    private DateTimeOffset? _lastLinkReadTimestamp;

    /// <summary>
    /// Time when the link last changed its target.
    /// Reported as its ctime and mtime.
    /// </summary>
    private DateTimeOffset? _lastLinkModifiedTimestamp;

    /// <summary>
    /// Time when the folder was last accessed.
    /// Reported as its atime.
    /// </summary>
    private DateTimeOffset? _lastFolderAccessTimestamp;

    private readonly Random _random;

    private readonly FileSystemWatcher _fsWatcher;

    private bool _disposed = false;

    internal RitsuFuseFileSystem(RitsuFuseSettings settings)
    {
        _settings = settings;

        _fsCreationTimestamp = DateTimeOffset.UtcNow;

        Log($"{nameof(RitsuFuseFileSystem)} starting at {_fsCreationTimestamp}. Hello, world!");

        MountPoint = _settings.FileSystemRoot;
        DefaultUserId = GetId("-u");
        DefaultGroupId = GetId("-g");

        Log($"Filesystem owner will be set to {DefaultUserId}:{DefaultGroupId}.");
        Log($"Random file from {_settings.TargetFolder} is available at {Path.Combine(_settings.FileSystemRoot, _settings.LinkName)}");

        _random = new();

        // TODO we're assuming the folder has at least a file,
        // it's going to be validated externally,
        // still, add some robustness
        _filenames = [.. Directory.GetFiles(_settings.TargetFolder)];

        if (_settings.UseQueue)
        {
            Log("Initializing queue.");
            _shuffledQueue = new(_filenames.OrderBy(fn => _random.Next()));
            _currentFile = _shuffledQueue.Dequeue();
        }
        else
        {
            _currentFile = _filenames[_random.Next(_filenames.Count)];
        }

        _fsWatcher = CreateWatcher();
    }

    private FileSystemWatcher CreateWatcher()
    {
        FileSystemWatcher watcher = new(_settings.TargetFolder)
        {
            EnableRaisingEvents = true,
            NotifyFilter = NotifyFilters.Attributes
                | NotifyFilters.CreationTime
                | NotifyFilters.DirectoryName
                | NotifyFilters.FileName
                | NotifyFilters.LastAccess
                | NotifyFilters.LastWrite
                | NotifyFilters.Security
                | NotifyFilters.Size
        };

        watcher.Created += HandleFileCreated;
        watcher.Deleted += HandleFileDeleted;
        watcher.Renamed += HandleFileRenamed;
        // TODO error handler as well

        return watcher;
    }

    protected override Errno OnGetPathStatus(string path, out Stat stat)
    {
        stat = new();

        if (IsFileSystemRoot(path))
        {
            stat.st_mode = FilePermissions.S_IFDIR | NativeConvert.FromOctalPermissionString("0444");
            stat.st_nlink = 2;

            stat.st_atim = (_lastFolderAccessTimestamp ?? _fsCreationTimestamp).ToTimeSpec();
            stat.st_ctim = _fsCreationTimestamp.ToTimeSpec();
            stat.st_mtim = _fsCreationTimestamp.ToTimeSpec();

            return 0;
        }
        else if (IsSymlinkPath(path))
        {
            stat.st_mode = FilePermissions.S_IFLNK | NativeConvert.FromOctalPermissionString("0444");
            stat.st_size = _currentFile.Length;

            stat.st_atim = (_lastLinkReadTimestamp ?? _fsCreationTimestamp).ToTimeSpec();
            stat.st_ctim = (_lastLinkModifiedTimestamp ?? _fsCreationTimestamp).ToTimeSpec();
            stat.st_mtim = (_lastLinkModifiedTimestamp ?? _fsCreationTimestamp).ToTimeSpec();

            return 0;
        }

        return Errno.ENOENT;
    }

    protected override Errno OnReadDirectory(string directory, OpenedPathInfo info, out IEnumerable<DirectoryEntry> paths)
    {
        if (IsFileSystemRoot(directory))
        {
            _lastFolderAccessTimestamp = DateTimeOffset.UtcNow;

            paths =
            [
                new DirectoryEntry("."),
                new DirectoryEntry(".."),
                new DirectoryEntry($"{_settings.LinkName}")
            ];
            return 0;
        }
        paths = [];
        return Errno.ENOENT;
    }

    protected override Errno OnReadSymbolicLink(string link, out string target)
    {
        if (!IsSymlinkPath(link))
        {
            target = "/dev/null";
            return Errno.ENOENT;
        }

        var now = DateTimeOffset.UtcNow;
        // if _lastAccessTimestamp (and so delta) is null,
        // this is the first request, rerolling is not necessary
        TimeSpan? delta = now - _lastLinkReadTimestamp;

        var message = delta.HasValue
            ? $"{delta.Value.TotalMilliseconds:0}ms since last "
            : "First ";
        var sb = new StringBuilder(message);
        sb.Append($"{nameof(OnReadSymbolicLink)} request, ");


        if (delta > _settings.Timeout)
        {
            _lastFile = _currentFile;
            _currentFile = GetNewTarget(_lastFile);
            _lastLinkModifiedTimestamp = now;

            sb.Append("rerolling the target.");
        }
        else
        {
            sb.Append("keeping existing target");
        }

        _lastLinkReadTimestamp = now;
        target = _currentFile;

        Log(sb.ToString());

        return 0;
    }

    // TODO should probably make this stuff thread-safe,
    // at least using locks

    /// <summary>
    /// Dispatcher method to get the new target file name in a way relevant to the settings.
    /// </summary>
    /// <param name="lastTarget">Name of the last file used.</param>
    /// <returns>New file name to use.</returns>
    private string GetNewTarget(string lastTarget)
    {
        switch (_filenames.Count)
        {
            case 0:
                Log("No files in the folder, short-circuiting to /dev/null.");
                return "/dev/null";
            case 1:
                Log("Only one file in the folder, short-circuiting to it.");
                return _filenames.First();
            default:
                return _settings.UseQueue
                    ? GetNewTargetFromQueue(lastTarget)
                    : GetNewTargetFromFileList(lastTarget);
        }
    }

    /// <summary>
    /// Gets a new random file name from all files in the folder,
    /// optionally preventing the same one file showing up twice in a row.
    /// </summary>
    /// <param name="lastTarget">Name of the last file used.</param>
    /// <returns>New file name to use.</returns>
    private string GetNewTargetFromFileList(string lastTarget)
    {
        string newTarget;
        var rerollCount = 0;
        do
        {
            newTarget = _filenames[_random.Next(_filenames.Count)];
            if (++rerollCount > 1)
            {
                Log($"Rerolling the file again{(rerollCount > 2 ? new string('!', rerollCount - 2) : string.Empty)} to prevent repeating file. 1/{Math.Pow(_filenames.Count, rerollCount - 1)} odds.");
            }
        }
        while (_settings.PreventRepeats && newTarget == lastTarget);

        return newTarget;
    }

    /// <summary>
    /// Get a new file name from the shuffled queue, regenerating it if needed,
    /// optionally preventing the same one file showing up twice in a row.
    /// </summary>
    /// <param name="lastTarget">Name of the last file used.</param>
    /// <returns>New file name to use.</returns>
    private string GetNewTargetFromQueue(string lastTarget)
    {
        // this should only be called when queue mode is initialized,
        // so it's probably safe to assume it's not null here
        if (_shuffledQueue!.TryDequeue(out var newTarget))
        {
            return newTarget;
        }
        else
        {
            Log("The queue has ended, rerolling.");
            var rerollCount = 0;
            do
            {
                _shuffledQueue = new(_filenames.OrderBy(fn => _random.Next()));
                if (++rerollCount > 1)
                {
                    Log($"Rerolling the queue again{(rerollCount > 2 ? new string('!', rerollCount - 2) : string.Empty)} to prevent repeating file. 1/{Math.Pow(_filenames.Count, rerollCount - 1)} odds.");
                }
            }
            while (_settings.PreventRepeats && _shuffledQueue.Peek() == lastTarget);
            return _shuffledQueue.Dequeue();
        }
    }

    /// <summary>
    /// Invokes the provided logging action for a message, if verbose flag is set.
    /// </summary>
    /// <param name="message">Message to log.</param>
    private void Log(string message)
    {
        if (_settings.Verbose)
        {
            _settings.LogAction?.Invoke(message);
        }
    }

    private bool IsFileSystemRoot(string path) => path == "/";

    private bool IsSymlinkPath(string path) => path == $"/{_settings.LinkName}";

    private void HandleFileCreated(object sender, FileSystemEventArgs e)
    {
        if (e.ChangeType != WatcherChangeTypes.Created)
        {
            throw new ApplicationException("Invalid FileSystemEventArgs passed");
        }
        if (!File.Exists(e.FullPath))
        {
            Log($"{e.FullPath} added, but it's not a file. Not my job.");
            return;
        }

        Log($"{e.FullPath} added, registering.");

        _filenames.Add(e.FullPath);

        if (_filenames.Count == 1)
        {
            Log("Consider adding more files to get random target symlinks.");
        }

        if (_settings.UseQueue)
        {
            Log($"Creating a new queue from {_shuffledQueue!.Count} remaining and the new element.");
            _shuffledQueue = new(_shuffledQueue!.Append(e.FullPath).OrderBy(fn => _random.Next()));
        }
    }

    private void HandleFileDeleted(object sender, FileSystemEventArgs e)
    {
        if (e.ChangeType != WatcherChangeTypes.Deleted)
        {
            throw new ApplicationException("Invalid FileSystemEventArgs passed");
        }

        if (!_filenames.Remove(e.FullPath))
        {
            Log($"{e.FullPath} removed, but it was not tracked anyway.");
            return;
        }

        Log($"{e.FullPath} removed, unregistering.");

        var countMessage = _filenames.Count switch
        {
            0 => "No files left! Link will point to /dev/null until some files are added!",
            1 => "Only one file left! Consider adding more files to get random target symlinks.",
            _ => null
        };
        if (countMessage != null)
        {
            Log(countMessage);
        }

        if (_settings.UseQueue)
        {
            _shuffledQueue = new(_shuffledQueue!.Where(fn => fn != e.FullPath));
            Log($"Removing the file from the queue as well. {_shuffledQueue.Count} elements remain in the queue.");
        }

        if (_currentFile == e.FullPath)
        {
            Log("The deleted file was the current target, rerolling.");
            _currentFile = GetNewTarget(_lastFile ?? string.Empty);
        }
    }

    private void HandleFileRenamed(object sender, RenamedEventArgs e)
    {
        if (e.ChangeType != WatcherChangeTypes.Renamed)
        {
            throw new ApplicationException("Invalid RenamedEventArgs passed");
        }

        if (!File.Exists(e.FullPath))
        {
            Log($"{e.OldFullPath} renamed to {e.FullPath}, but it's not a file. Not my job.");
            return;
        }

        Log($"{e.OldFullPath} renamed to {e.FullPath}, registering.");

        _filenames.Remove(e.OldFullPath);
        _filenames.Add(e.FullPath);
        if (_settings.UseQueue)
        {
            Log("Recreating the queue following renaming.");
            _shuffledQueue = new(
                _shuffledQueue!.Where(fn => fn != e.OldFullPath)
                .Append(e.FullPath)
                .OrderBy(fn => _random.Next()));
        }

        if (_currentFile == e.OldFullPath)
        {
            Log("The renamed file was the current target, renaming.");
            _currentFile = e.FullPath;
        }
    }

    /// <summary>
    /// Invokes "id argument" to get an id.
    /// </summary>
    /// <param name="argument">Argument to id. Either -u for uid or -g for gid.</param>
    /// <returns>User or group id.</returns>
    private long GetId(string argument)
    {
        // TODO more robustness
        if (argument != "-u" && argument != "-g")
        {
            throw new ArgumentOutOfRangeException(nameof(argument), "Unsupported id argument.");
        }

        using (var idProcess = new Process())
        {
            idProcess.StartInfo.UseShellExecute = false;
            idProcess.StartInfo.CreateNoWindow = true;
            idProcess.StartInfo.FileName = "id";
            idProcess.StartInfo.Arguments = argument;
            idProcess.StartInfo.RedirectStandardOutput = true;
            idProcess.Start();

            return long.Parse(idProcess.StandardOutput.ReadToEnd());
        }
    }

    protected override void Dispose(bool disposing)
    {
        // Check to see if Dispose has already been called.
        if (!_disposed)
        {
            // If disposing equals true, dispose all managed
            // and unmanaged resources.
            if (disposing)
            {
                // Dispose managed resources.
                _fsWatcher.Dispose();
            }
            // Note disposing has been done.
            _disposed = true;
        }
    }
}
