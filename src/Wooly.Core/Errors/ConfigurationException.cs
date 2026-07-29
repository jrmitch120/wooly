namespace Wooly.Core.Errors;

/// <summary>
///     One of this client's own files exists but could not be made sense of. The message names the file and what is
///     wrong with it, because the user is the only one who can fix a file they are invited to hand-edit.
/// </summary>
public sealed class ConfigurationException(string path, string problem)
    : WoolyException($"Could not read {path}: {problem}");
