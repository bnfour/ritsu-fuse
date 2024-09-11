namespace Bnfour.RitsuFuse.Proper.Exceptions;

/// <summary>
/// Indicates a validation error for the settings.
/// </summary>
/// <param name="message">Human-readable description of the error.</param>
public class SettingsValidationException(string message) : Exception(message) { }
