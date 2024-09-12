using Bnfour.RitsuFuse.Proper.Exceptions;

namespace Bnfour.RitsuFuse.Proper.Validation.Validators;

/// <summary>
/// Checks that the target folder exists and contains at least two files.
/// </summary>
public class TargerFolderValidator : ISettingsValidator
{
    public void Validate(RitsuFuseSettings settings)
    {
        var tFolder = settings.TargetFolder;

        try
        {
            if (!Directory.Exists(tFolder))
            {
                throw new SettingsValidationException($"Unable to locate target folder {tFolder}");
            }
            if (Directory.GetFiles(tFolder).Length < 2)
            {
                throw new SettingsValidationException($"Folder {tFolder} does not contain at least two files.");
            }
        }
        catch (Exception ex) when (ex is not SettingsValidationException)
        {
            throw new SettingsValidationException($"Some kind of error checking target folder {tFolder}. Access rights?");
        }
    }
}
