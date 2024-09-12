using Bnfour.RitsuFuse.Proper.Exceptions;

namespace Bnfour.RitsuFuse.Proper.Validation.Validators;

/// <summary>
/// Checks that the folder to base the filesystem in exists and is empty.
/// </summary>
public class FileSystemRootFolderValidator : ISettingsValidator
{
    public void Validate(RitsuFuseSettings settings)
    {
        var fsRoot = settings.FileSystemRoot;

        try
        {
            if (!Directory.Exists(fsRoot))
            {
                throw new SettingsValidationException($"Unable to locate folder {fsRoot}");
            }
            if (Directory.GetFileSystemEntries(fsRoot).Length > 0)
            {
                throw new SettingsValidationException($"Folder {fsRoot} is not empty.");
            }
        }
        catch (Exception ex) when (ex is not SettingsValidationException)
        {
            throw new SettingsValidationException($"Some kind of error checking folder {fsRoot}. Access rights?");
        }
    }
}
