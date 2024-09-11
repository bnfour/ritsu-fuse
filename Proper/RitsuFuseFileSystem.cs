using System.Diagnostics;
using Mono.Fuse.NETStandard;

namespace Bnfour.RitsuFuse.Proper;

/// <summary>
/// A class to implement actual FUSE logic in.
/// </summary>
internal class RitsuFuseFileSystem : FileSystem
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

        _fsTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        MountPoint = _settings.FileSystemRoot;
        DefaultUserId = GetId("-u");
        DefaultGroupId = GetId("-g");
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
