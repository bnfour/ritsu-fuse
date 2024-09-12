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
        // TODO support for files added and removed from the target folder
        // FileSystemWatcher?
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

        if (path == "/")
        {
            stat.st_mode = FilePermissions.S_IFDIR | NativeConvert.FromOctalPermissionString("0444");
            stat.st_nlink = 2;
            return 0;
        }
        else if (path == $"/{_settings.LinkName}")
        {
            stat.st_mode = FilePermissions.S_IFLNK | NativeConvert.FromOctalPermissionString("0444");
            stat.st_size = _currentFile.Length;
            return 0;
        }

        return Errno.ENOENT;
    }

    protected override Errno OnReadDirectory(string directory, OpenedPathInfo info, out IEnumerable<DirectoryEntry> paths)
    {
        if (directory == "/")
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
        if (link != $"/{_settings.LinkName}")
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
            // TODO move net item logic to separate methods
            _lastFile = _currentFile;
            if (_settings.UseQueue)
            {
                if (_shuffledQueue!.TryDequeue(out var newFile))
                {
                    _currentFile = newFile;
                }
                else
                {
                    do
                    {
                        _shuffledQueue = new(_filenames.OrderBy(fn => _random.Next()));
                    }
                    while (_settings.PreventRepeats && _shuffledQueue.Peek() == _lastFile);
                    _currentFile = _shuffledQueue.Dequeue();
                }
            }
            else
            {
                do
                {
                    _currentFile = _filenames[_random.Next(_filenames.Count)];
                }
                while (_settings.PreventRepeats && _currentFile == _lastFile);
            }
            
        }

        _lastAccessTimestamp = now;
        target = _currentFile;
        return 0;
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
}
