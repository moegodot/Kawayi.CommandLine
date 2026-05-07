// Copyright (c) 2026 MoeGodot<me@kawayi.moe>.
// Licensed under the GNU Affero General Public License v3-or-later license.

using System.Collections.Immutable;

namespace Kawayi.CommandLine.Abstractions;

/// <summary>
/// Represents an immutable parsing schema snapshot.
/// This may not be schema of command line interface root,
/// meaning it may be schema of a subcommand.
/// </summary>
/// <param name="GeneratedFrom">The type which the <see cref="CliSchema"/> generated from.</param>
/// <param name="SubcommandDefinitions">The available subcommand definitions keyed by name.</param>
/// <param name="Subcommands">The child parsing snapshots keyed by subcommand name.</param>
/// <param name="Properties">The available property definitions keyed by canonical name.</param>
/// <param name="Argument">The ordered positional argument definitions.</param>
public readonly record struct CliSchema(
    Type? GeneratedFrom,
    ImmutableDictionary<OptionToken, CommandDefinition> SubcommandDefinitions,
    ImmutableDictionary<ArgumentOrCommandToken, CliSchema> Subcommands,
    ImmutableDictionary<OptionToken, PropertyDefinition> Properties,
    ImmutableList<ParameterDefinition> Argument)
{
}
