using System.Reflection;

namespace Bnfour.RitsuFuse.Proper;

/// <summary>
/// A class that provides settings to the actual FS class.
/// Some are fuse options not available as first-class properties in the lib we use,
/// and some are provided by the users of this lib.
/// </summary>
public class RitsuFuseWrapper
{
    private readonly string[] _fuseOptions = [];

    public void Start(RitsuFuseSettings settings)
    {
        Validate(settings);

        using (var fs = new RitsuFuseFileSystem(settings))
        {
            // TODO provide fuse options, like ro mount and auto unmount
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
