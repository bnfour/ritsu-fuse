using Bnfour.RitsuFuse.Proper.Validation.Validators;

namespace Bnfour.RitsuFuse.Proper.Validation;

internal static class SettingsValidatorFactory
{
    /// <summary>
    /// Factory method for the settings validators
    /// </summary>
    /// <returns>An enumerable of active validators.</returns>
    internal static IEnumerable<ISettingsValidator> GetValidators()
    {
        yield return new LogAvailabilityValidator();
        yield return new LinkNameValidator();
        yield return new TimeoutValidator();
        yield return new FileSystemRootFolderValidator();
        yield return new TargerFolderValidator();
        yield return new DifferentFoldersValidator();
    }
}
