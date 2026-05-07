// Copyright (c) 2026 GodotAsync<me@kawayi.moe>.
// Licensed under the GNU Affero General Public License v3-or-later license.

using System.Collections.Immutable;

namespace Kawayi.CommandLine.Abstractions;

/// <summary>
/// Represents an immutable parsing result snapshot of a <see cref="CliSchema"/>.
/// This may not be schema of command line interface root,
/// meaning it may be schema of a subcommand.
/// </summary>
/// <param name="ParsingOptions">options used to create this result</param>
/// <param name="Schema">the schema the result obey</param>
/// <param name="ParentCommand">parent command's parsing result.If current command is root command, this must be null</param>
/// <param name="CurrentCommandDefinition">parent command's definition for current subcommand.If current command is root command, this must be null</param>
/// <param name="Arguments">Parsed argument values, including explicit values and generated defaults.</param>
/// <param name="Properties">Parsed property values, including explicit values and generated defaults.</param>
/// <param name="Subcommands">parsed subcommands</param>
/// <param name="ToProgramArguments">Tokens after the option terminator that should be forwarded to another program.</param>
public sealed record Cli(
    ParsingOptions ParsingOptions,
    CliSchema Schema,
    Cli? ParentCommand,
    CommandDefinition? CurrentCommandDefinition,
    ImmutableDictionary<ParameterDefinition, object> Arguments,
    ImmutableDictionary<PropertyDefinition, object> Properties,
    ImmutableDictionary<CommandDefinition, Cli> Subcommands,
    ImmutableArray<Token> ToProgramArguments);
