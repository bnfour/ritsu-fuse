using Bnfour.RitsuFuse.Proper.Exceptions;

namespace Bnfour.RitsuFuse.Proper.Validation.Validators;

/// <summary>
/// Checks that the link name is not something that could destroy the app:
/// ".", "..", and anything with a path delimiter is considered an invalid name.
/// Not ideal, but should work "for now".
/// </summary>
public class LinkNameValidator : ISettingsValidator
{
    public void Validate(RitsuFuseSettings settings)
    {
        if (settings.LinkName == "." || settings.LinkName == ".."
            || settings.LinkName.Contains(Path.PathSeparator))
        {
            throw new SettingsValidationException("Invalid symlink name.");
        }
    }
}
