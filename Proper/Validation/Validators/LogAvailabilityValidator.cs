using Bnfour.RitsuFuse.Proper.Exceptions;

namespace Bnfour.RitsuFuse.Proper.Validation.Validators;

/// <summary>
/// Verifies that there is an action to perform with log data
/// if verbose mode is enabled.
/// </summary>
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
