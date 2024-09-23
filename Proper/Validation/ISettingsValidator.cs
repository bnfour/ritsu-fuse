namespace Bnfour.RitsuFuse.Proper.Validation;

/// <summary>
/// Interface for the settings validators.
/// </summary>
internal interface ISettingsValidator
{
    /// <summary>
    /// Throws a SettingsValidationException if provided settings instance does not match expectations.
    /// </summary>
    /// <param name="settings">Instance to check.</param>
    void Validate(RitsuFuseSettings settings);
}
