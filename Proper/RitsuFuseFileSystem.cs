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
    /// </summary>
    private readonly long _fsTimestamp;

    /// <summary>
    /// Settings instance. Assumed to be previously validated.
    /// </summary>
    private readonly RitsuFuseSettings _settings;

    internal RitsuFuseFileSystem(RitsuFuseSettings settings)
    {
        _settings = settings;

        _fsTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        MountPoint = _settings.FileSystemRoot;
        DefaultUserId = GetId("-u");
        DefaultGroupId = GetId("-g");
    }

    protected override Errno OnGetPathStatus(string path, out Stat stat)
    {   
        stat = new()
        {
            // we don't need the ns precision
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
            // TODO acutal link size
            stat.st_size = 9;
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
        // TODO the thing this repo is for
        target = "/dev/null";
        return link == $"/{_settings.LinkName}" ? 0 : Errno.ENOENT;
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
