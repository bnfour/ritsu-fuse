using System.Reflection;
using Bnfour.RitsuFuse.Proper.Exceptions;
using Bnfour.RitsuFuse.Proper.Validation;

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
            // fs.Start();
        }
    }

    public static Version GetVersion()
        => Assembly.GetExecutingAssembly().GetName().Version ?? throw new ApplicationException("Unable to determine library version");

    /// <summary>
    /// Throws if settings do not pass the validation.
    /// </summary>
    /// <param name="settings">Settings instance to validate.</param>
    /// <exception cref="AggregateException">Contains all validation exceptions
    /// as <see cref="SettingsValidationException"/></exception>
    private void Validate(RitsuFuseSettings settings)
    {
        List<SettingsValidationException> caught = [];
        foreach (var validator in SettingsValidatorFactory.GetValidators())
        {
            try
            {
                validator.Validate(settings);
            }
            catch (SettingsValidationException ex)
            {
                caught.Add(ex);
            }
        }
        if (caught.Count > 0)
        {
            throw new AggregateException(caught);
        }
    }
}
