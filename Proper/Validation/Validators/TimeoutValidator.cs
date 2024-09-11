using Bnfour.RitsuFuse.Proper.Exceptions;

namespace Bnfour.RitsuFuse.Proper.Validation.Validators;

public class TimeoutValidator : ISettingsValidator
{
    public void Validate(RitsuFuseSettings settings)
    {
        // TODO is 1ms precision enough?
        if (settings.Timeout < TimeSpan.FromMilliseconds(1))
        {
            throw new SettingsValidationException("Invalid timeout value.");
        }
    }
}
