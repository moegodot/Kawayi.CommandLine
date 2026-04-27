
using System.Collections.Immutable;

namespace Kawayi.CommandLine.Abstractions;

public sealed record CommandDefinition(DefinitionInformation Information,
                                       CommandDefinition? ParentCommand)
    : Symbol(Information,ParentCommand);

public abstract record TypedDefinition(
    DefinitionInformation Information,
    Symbol? ParentSymbol,
    Type Type,
    bool Requirement) : Symbol(Information, ParentSymbol)
{
    /// <summary>
    /// the factory to get the default value,
    /// input the parsing result and return the default value
    /// </summary>
    public Func<object, object>? DefaultValueFactory { get; init; }

    /// <summary>
    /// validation of the value, called on any value(including default value),
    /// return null if the value is valid, or return the error message.
    /// </summary>
    public Func<object, string?>? Validation { get; init; }
}

public sealed record ArgumentDefinition(
    DefinitionInformation Information,
    Symbol? ParentSymbol,
    ArgumentArity Arity,
    Type Type,
    bool Requirement) : TypedDefinition(Information, ParentSymbol, Type, Requirement)
{
    /// <summary>
    /// Defines how many values this positional argument may consume.
    /// When multiple positional arguments are present, the parser assigns values greedily
    /// from left to right while reserving enough values to satisfy later arguments'
    /// minimum arity requirements.
    /// </summary>
    public ArgumentArity Arity { get; init; } = Arity;
}

public sealed record PropertyDefinition(
    DefinitionInformation Information,
    ImmutableDictionary<string, NameWithVisibility> LongName,
    ImmutableDictionary<string, NameWithVisibility> ShortName,
    Symbol? ParentSymbol,
    Type Type,
    bool Requirement)
    : TypedDefinition(Information, ParentSymbol, Type, Requirement)
{
    public PossibleValues? PossibleValues { get; init; }
}
