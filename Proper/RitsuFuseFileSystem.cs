using System.Diagnostics;
using Mono.Fuse.NETStandard;
using Mono.Unix.Native;

namespace Bnfour.RitsuFuse.Proper;

/// <summary>
/// A class to implement actual FUSE logic in.
/// </summary>
internal sealed class RitsuFuseFileSystem : FileSystem
{
    /// <summary>
    /// Timestamp to be applied for everithing in the file system.
    /// Unixtime in seconds, as st_atime. Still filled as st_atim with nanoseconds set to 0.
    /// </summary>
    private readonly long _fsTimestamp;

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
    /// Time of last access to the link. If accessed later than <see cref="RitsuFuseSettings.Timeout"/>,
    /// new link target is selected. (Most real-world apps read the link more than once, so requests in rapid
    /// succession keep their target stable.)
    /// </summary>
    // TODO does it have to be nullable?
    private DateTimeOffset? _lastAccessTimestamp;

    private readonly Random _random;

    private readonly FileSystemWatcher _fsWatcher;

    private bool Disposed = false;

    internal RitsuFuseFileSystem(RitsuFuseSettings settings)
    {
        _settings = settings;

        _fsTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        MountPoint = _settings.FileSystemRoot;
        DefaultUserId = GetId("-u");
        DefaultGroupId = GetId("-g");

        _random = new();

        // TODO we're assuming the folder has at least a file,
        // it's going to be validated externally,
        // still, add some robustness
        _filenames = [.. Directory.GetFiles(_settings.TargetFolder)];

        if (_settings.UseQueue)
        {
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
        stat = new()
        {
            // we don't need the ns precision,
            // but let's not use the backward compatibility st_Xtime
            st_atim = new Timespec
            {
                tv_sec = _fsTimestamp,
                tv_nsec = 0
            },
            st_mtim = new Timespec
            {
                tv_sec = _fsTimestamp,
                tv_nsec = 0
            },
            st_ctim = new Timespec
            {
                tv_sec = _fsTimestamp,
                tv_nsec = 0
            },
        };

        if (IsFileSystemRoot(path))
        {
            stat.st_mode = FilePermissions.S_IFDIR | NativeConvert.FromOctalPermissionString("0444");
            stat.st_nlink = 2;
            return 0;
        }
        else if (IsSymlinkPath(path))
        {
            stat.st_mode = FilePermissions.S_IFLNK | NativeConvert.FromOctalPermissionString("0444");
            stat.st_size = _currentFile.Length;
            return 0;
        }

        return Errno.ENOENT;
    }

    protected override Errno OnReadDirectory(string directory, OpenedPathInfo info, out IEnumerable<DirectoryEntry> paths)
    {
        if (IsFileSystemRoot(directory))
        {
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
        TimeSpan? delta = now - _lastAccessTimestamp;

        if (delta > _settings.Timeout)
        {
            _lastFile = _currentFile;
            _currentFile = GetNewTarget(_lastFile);
        }

        _lastAccessTimestamp = now;
        target = _currentFile;
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
        => _settings.UseQueue ? GetNewTargetFromQueue(lastTarget) : GetNewTargetFromFileList(lastTarget);

    // TODO handle the cases where files were deleted to the point there are
    // 0 files -- return an error?
    // 1 file -- return it without getting stuck in a loop if PreventRepeats is set

    /// <summary>
    /// Gets a new random file name from all files in the folder,
    /// optionally preventing the same one file showing up twice in a row.
    /// </summary>
    /// <param name="lastTarget">Name of the last file used.</param>
    /// <returns>New file name to use.</returns>
    private string GetNewTargetFromFileList(string lastTarget)
    {
        string newTarget;
        do
        {
            newTarget = _filenames[_random.Next(_filenames.Count)];
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
            do
            {
                _shuffledQueue = new(_filenames.OrderBy(fn => _random.Next()));
            }
            while (_settings.PreventRepeats && _shuffledQueue.Peek() == lastTarget);
            return _shuffledQueue.Dequeue();
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

        _filenames.Add(e.FullPath);
        if (_settings.UseQueue)
        {
            // reshuffle the current queue after adding the new file
            _shuffledQueue = new(_shuffledQueue!.Append(e.FullPath).OrderBy(fn => _random.Next()));
        }
    }

    private void HandleFileDeleted(object sender, FileSystemEventArgs e)
    {
        if (e.ChangeType != WatcherChangeTypes.Deleted)
        {
            throw new ApplicationException("Invalid FileSystemEventArgs passed");
        }

        _filenames.Remove(e.FullPath);
        if (_settings.UseQueue)
        {
            // remove the deleted file name from the queue
            _shuffledQueue = new(_shuffledQueue!.Where(fn => fn != e.FullPath));
        }
    }

    private void HandleFileRenamed(object sender, RenamedEventArgs e)
    {
        if (e.ChangeType != WatcherChangeTypes.Renamed)
        {
            throw new ApplicationException("Invalid RenamedEventArgs passed");
        }

        _filenames.Remove(e.OldFullPath);
        _filenames.Add(e.FullPath);
        if (_settings.UseQueue)
        {
            _shuffledQueue = new(
                _shuffledQueue!.Where(fn => fn != e.OldFullPath)
                .Append(e.FullPath)
                .OrderBy(fn => _random.Next()));
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
        if (!Disposed)
        {
            // If disposing equals true, dispose all managed
            // and unmanaged resources.
            if (disposing)
            {
                // Dispose managed resources.
                _fsWatcher.Dispose();
            }
            // Note disposing has been done.
            Disposed = true;
        }
    }
}
