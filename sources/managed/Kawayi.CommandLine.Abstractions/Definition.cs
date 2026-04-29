
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;

namespace Kawayi.CommandLine.Abstractions;

/// <summary>
/// Defines a subcommand entry in the command schema.
/// </summary>
/// <param name="Information">The command metadata.</param>
/// <param name="Alias">The alternative names for the command.</param>
/// <param name="ParentCommand">The parent command when this command is nested.</param>
public sealed record CommandDefinition(DefinitionInformation Information,
                                       ImmutableDictionary<string, NameWithVisibility> Alias,
                                       CommandDefinition? ParentCommand)
    : Symbol(Information, ParentCommand);

/// <summary>
/// Represents a typed schema definition such as an argument or property.
/// </summary>
/// <param name="Information">The definition metadata.</param>
/// <param name="ParentSymbol">The parent symbol when the definition is nested.</param>
/// <param name="Type">The CLR type bound to the definition.</param>
/// <param name="Requirement">Whether the definition is required.</param>
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

/// <summary>
/// Represents a positional argument definition.
/// </summary>
/// <param name="Information">The argument metadata.</param>
/// <param name="ParentSymbol">The parent symbol when the argument is nested.</param>
/// <param name="ValueRange">The number of values the argument may consume.</param>
/// <param name="Type">The CLR type bound to the argument.</param>
/// <param name="Requirement">Whether the argument is required.</param>
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

/// <summary>
/// Represents an option or property definition.
/// </summary>
/// <param name="Information">The property metadata.</param>
/// <param name="LongName">The long option names.</param>
/// <param name="ShortName">The short option names.</param>
/// <param name="ParentSymbol">The parent symbol when the property is nested.</param>
/// <param name="Type">The CLR type bound to the property.</param>
/// <param name="Requirement">Whether the property is required.</param>
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
    /// <summary>
    /// Gets the accepted number of option values.
    /// </summary>
    public ValueRange NumArgs { get; init; } = ValueRange.ZeroOrMore;

    /// <summary>
    /// Gets the metavariable name shown in help output.
    /// </summary>
    public string? ValueName { get; init; }

    /// <summary>
    /// Gets the possible values metadata used for help rendering.
    /// </summary>
    public PossibleValues? PossibleValues { get; init; }
}
