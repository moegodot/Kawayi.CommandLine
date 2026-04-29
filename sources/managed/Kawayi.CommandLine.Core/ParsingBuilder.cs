// Copyright (c) 2026 MoeGodot<me@kawayi.moe>.
// Licensed under the GNU Affero General Public License v3-or-later license.

using System.Collections.Immutable;
using Kawayi.CommandLine.Abstractions;

namespace Kawayi.CommandLine.Core;

/// <summary>
/// Builds a mutable command-line schema and produces immutable <see cref="ParsingInput"/> snapshots.
/// Parsing is delegated to <see cref="ParsingInputParser"/>.
/// </summary>
public sealed class ParsingBuilder : IParsingBuilder
{
    /// <summary>
    /// Initializes a new mutable parsing builder.
    /// </summary>
    /// <param name="parsingOptions">The parsing options attached to the builder.</param>
    /// <param name="subcommandDefinitions">The initial subcommand definitions.</param>
    /// <param name="properties">The initial property definitions.</param>
    /// <param name="argument">The initial positional argument definitions.</param>
    /// <param name="subcommands">The initial child parsing builders.</param>
    public ParsingBuilder(ParsingOptions parsingOptions,
                          ImmutableDictionary<string, CommandDefinition>? subcommandDefinitions = null,
                          ImmutableDictionary<string, PropertyDefinition>? properties = null,
                          IList<ArgumentDefinition>? argument = null,
                          ImmutableDictionary<string, IParsingBuilder>? subcommands = null)
    {
        ParsingOptions = parsingOptions ?? throw new ArgumentNullException(nameof(parsingOptions));
        SubcommandDefinitions = CreateBuilder(subcommandDefinitions);
        Properties = CreateBuilder(properties);
        Argument = CreateArgumentBuilder(argument);
        Subcommands = CreateBuilder(subcommands);
    }

    /// <summary>
    /// Parses tokens using <see cref="ParsingInputParser"/> for backward compatibility with callers that
    /// previously invoked <see cref="ParsingBuilder"/> directly.
    /// </summary>
    /// <param name="options">The parsing options for this operation.</param>
    /// <param name="arguments">The tokens to parse.</param>
    /// <param name="initialState">The immutable schema snapshot to parse against.</param>
    /// <returns>The parsing outcome.</returns>
    public static ParsingResult CreateParsing(ParsingOptions options, ImmutableArray<Token> arguments, ParsingInput initialState)
    {
        return ParsingInputParser.CreateParsing(options, arguments, initialState);
    }

    /// <summary>
    /// Captures the current mutable builder graph as an immutable parsing snapshot.
    /// </summary>
    /// <param name="builder">The builder to snapshot.</param>
    /// <returns>An immutable parsing snapshot.</returns>
    public static ParsingInput CreateInput(IParsingBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var subcommandDefinitions = builder.SubcommandDefinitions.ToImmutable();
        var properties = builder.Properties.ToImmutable();
        var argument = builder.Argument.ToImmutable();
        var subcommands = ImmutableDictionary.CreateBuilder<string, ParsingInput>(StringComparer.Ordinal);

        foreach (var (key, childBuilder) in builder.Subcommands)
        {
            subcommands[key] = childBuilder.Build();
        }

        return new ParsingInput(
            builder.ParsingOptions,
            subcommandDefinitions,
            subcommands.ToImmutable(),
            properties,
            argument);
    }

    /// <summary>
    /// Produces an immutable parsing snapshot from the current builder state.
    /// </summary>
    /// <returns>The immutable parsing snapshot.</returns>
    public ParsingInput Build() => CreateInput(this);

    /// <inheritdoc />
    public ParsingOptions ParsingOptions { get; }

    /// <inheritdoc />
    public ImmutableDictionary<string, CommandDefinition>.Builder SubcommandDefinitions { get; }

    /// <inheritdoc />
    public ImmutableDictionary<string, PropertyDefinition>.Builder Properties { get; }

    /// <inheritdoc />
    public ImmutableList<ArgumentDefinition>.Builder Argument { get; }

    /// <inheritdoc />
    public ImmutableDictionary<string, IParsingBuilder>.Builder Subcommands { get; }

    private static ImmutableDictionary<string, T>.Builder CreateBuilder<T>(ImmutableDictionary<string, T>? source)
    {
        var builder = ImmutableDictionary.CreateBuilder<string, T>(StringComparer.Ordinal);

        if (source is not null)
        {
            foreach (var (key, value) in source)
            {
                builder[key] = value;
            }
        }

        return builder;
    }

    private static ImmutableList<ArgumentDefinition>.Builder CreateArgumentBuilder(IEnumerable<ArgumentDefinition>? argument)
    {
        var builder = ImmutableList.CreateBuilder<ArgumentDefinition>();

        if (argument is not null)
        {
            builder.AddRange(argument);
        }

        return builder;
    }
}
