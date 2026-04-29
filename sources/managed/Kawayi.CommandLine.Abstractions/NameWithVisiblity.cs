namespace Kawayi.CommandLine.Abstractions;

/// <summary>
/// Couples a command-line name with its visibility setting.
/// </summary>
/// <param name="Value">The command-line name.</param>
/// <param name="Visible">Whether the name should be shown to users.</param>
public readonly record struct NameWithVisibility(string Value, bool Visible);
