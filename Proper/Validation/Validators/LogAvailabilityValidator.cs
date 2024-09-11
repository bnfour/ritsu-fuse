using Bnfour.RitsuFuse.Proper.Exceptions;

namespace Bnfour.RitsuFuse.Proper.Validation.Validators;

public class LogAvailabilityValidator : ISettingsValidator
{
    public void Validate(RitsuFuseSettings settings)
    {
        if (settings.Verbose && settings.LogAction == null)
        {
            throw new SettingsValidationException("No action to use on active logs.");
        }
    }
}
