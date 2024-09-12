using Bnfour.RitsuFuse.Proper.Exceptions;

namespace Bnfour.RitsuFuse.Proper.Validation.Validators;

/// <summary>
/// Checks that the file system root folder and target folder are different.
/// </summary>
public class DifferentFoldersValidator : ISettingsValidator
{
    public void Validate(RitsuFuseSettings settings)
    {
        if (settings.TargetFolder == settings.FileSystemRoot)
        {
            throw new SettingsValidationException($"The same folder is used both as file system root and target folder.");
        }
    }
}
