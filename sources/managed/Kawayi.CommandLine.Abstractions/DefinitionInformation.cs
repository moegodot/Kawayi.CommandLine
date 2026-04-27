
using System.Collections.Immutable;

namespace Kawayi.CommandLine.Abstractions;

/// <summary>
/// basical definition information of an argument,command or option
/// </summary>
/// <param name="Name">the name the element</param>
/// <param name="ConciseDescription">one line concise description</param>
/// <param name="HelpText">full help text</param>
public record DefinitionInformation(
    NameWithVisibility Name,
    string ConciseDescription,
    string HelpText);

public abstract record Symbol(DefinitionInformation Information, Symbol? ParentSymbol);
