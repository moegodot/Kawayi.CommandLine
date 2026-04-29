
using System.Collections.Immutable;

namespace Kawayi.CommandLine.Abstractions;

/// <summary>
/// Represents the user-facing documentation for a command-line symbol.
/// </summary>
/// <param name="ConciseDescription">The concise one-line description.</param>
/// <param name="HelpText">The full help text.</param>
public sealed record Document(
    string ConciseDescription,
    string HelpText
);

/// <summary>
/// Represents the shared metadata for an argument, command, or option.
/// </summary>
/// <param name="Name">The command-line name metadata.</param>
/// <param name="Document">The user-facing documentation.</param>
public record DefinitionInformation(
    NameWithVisibility Name,
    Document Document);

/// <summary>
/// Represents a node in the exported command-line symbol graph.
/// </summary>
/// <param name="Information">The metadata associated with the symbol.</param>
/// <param name="ParentSymbol">The parent symbol when one exists.</param>
public abstract record Symbol(DefinitionInformation Information, Symbol? ParentSymbol);
