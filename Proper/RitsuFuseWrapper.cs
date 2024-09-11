using System.Reflection;

namespace Bnfour.RitsuFuse.Proper;

/// <summary>
/// A class that provides settings to the actual FS class.
/// Some are fuse options not available as first-class properties in the lib we use,
/// and some are provided by the users of this lib.
/// </summary>
public class RitsuFuseWrapper
{
    /// <summary>
    /// Common options for the FUSE file system.
    /// </summary>
    private readonly string[] _fuseOptions =
    [
        // mount as read-only file system
        "-o ro",
        // release the file system when app is terminated
        // TODO it is considered dangerous in the man, is it worth it?
        "-o auto_unmount",
        // file system subtype, diplayed in third field in /etc/mtab
        // as "fuse.ritsu"
        "-o subtype=ritsu",
        // to be replaced by file system type, first field in /etc/mtab,
        // as "{settings.TargetFolder}/random-file" when the data is available
        string.Empty
    ];

    public void Start(RitsuFuseSettings settings)
    {
        Validate(settings);

        using (var fs = new RitsuFuseFileSystem(settings))
        {
            _fuseOptions[^1] = $"-o fsname={Path.Combine(settings.TargetFolder, "random-file")}";
            fs.ParseFuseArguments(_fuseOptions);
            // TODO fs.Start();
        }
    }

    public static Version GetVersion()
        => Assembly.GetExecutingAssembly().GetName().Version ?? throw new ApplicationException("Unable to determine library version");

    private void Validate(RitsuFuseSettings settings)
    {
        // TODO validation "pipeline"
        throw new NotImplementedException();
    }
}
