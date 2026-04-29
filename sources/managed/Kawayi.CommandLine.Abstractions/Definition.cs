
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;

namespace Kawayi.CommandLine.Abstractions;

/// <summary>
/// Defines a subcommand entry in the command schema.
/// </summary>
public sealed record CommandDefinition(DefinitionInformation Information,
                                       ImmutableDictionary<string, NameWithVisibility> Alias,
                                       CommandDefinition? ParentCommand)
    : Symbol(Information,ParentCommand);

public abstract record TypedDefinition(
    DefinitionInformation Information,
    Symbol? ParentSymbol,
    [property: DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)]
    [param: DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)]
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
    ValueRange ValueRange,
    [param: DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)]
    Type Type,
    bool Requirement) : TypedDefinition(Information, ParentSymbol, Type, Requirement)
{
    /// <summary>
    /// Defines how many values this positional argument may consume.
    /// When multiple positional arguments are present, the parser assigns values greedily
    /// from left to right while reserving enough values to satisfy later arguments'
    /// minimum arity requirements.
    /// </summary>
    public ValueRange ValueRange { get; init; } = ValueRange;
}

public sealed record PropertyDefinition(
    DefinitionInformation Information,
    ImmutableDictionary<string, NameWithVisibility> LongName,
    ImmutableDictionary<string, NameWithVisibility> ShortName,
    Symbol? ParentSymbol,
    [param: DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)]
    Type Type,
    bool Requirement)
    : TypedDefinition(Information, ParentSymbol, Type, Requirement)
{
    public ValueRange NumArgs { get; init; } = ValueRange.ZeroOrMore;

    public string? ValueName { get; init; }

    public PossibleValues? PossibleValues { get; init; }
}
