
using System.Collections.Immutable;

namespace Kawayi.CommandLine.Abstractions;

/// <param name="ConciseDescription">one line concise description</param>
/// <param name="HelpText">full help text</param>
public sealed record Document(
    string ConciseDescription,
    string HelpText
);

/// <summary>
/// basical definition information of an argument,command or option
/// </summary>
/// <param name="Name">the name the element</param>
public record DefinitionInformation(
    NameWithVisibility Name,
    Document Document);

public abstract record Symbol(DefinitionInformation Information, Symbol? ParentSymbol);
