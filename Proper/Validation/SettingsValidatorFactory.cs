using Bnfour.RitsuFuse.Proper.Validation.Validators;

namespace Bnfour.RitsuFuse.Proper.Validation;

internal static class SettingsValidatorFactory
{
    /// <summary>
    /// Factory method for the settings validators
    /// </summary>
    /// <returns>An enumerable of activa validators.</returns>
    internal static IEnumerable<ISettingsValidator> GetValidators()
    {
        // TODO more validators
        yield return new LogAvailabilityValidator();
    }
}
