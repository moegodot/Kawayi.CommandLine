// Copyright (c) 2026 MoeGodot<me@kawayi.moe>.
// Licensed under the GNU Affero General Public License v3-or-later license.

using System.Collections.Immutable;

namespace Kawayi.CommandLine.Abstractions;

/// <summary>
/// Represents an immutable parsing schema snapshot.
/// </summary>
/// <param name="ParsingOptions">The parsing options associated with the snapshot.</param>
/// <param name="SubcommandDefinitions">The available subcommand definitions keyed by name.</param>
/// <param name="Subcommands">The child parsing snapshots keyed by subcommand name.</param>
/// <param name="Properties">The available property definitions keyed by canonical name.</param>
/// <param name="Argument">The ordered positional argument definitions.</param>
public readonly record struct ParsingInput(
    ParsingOptions ParsingOptions,
    ImmutableDictionary<string, CommandDefinition> SubcommandDefinitions,
    ImmutableDictionary<string, ParsingInput> Subcommands,
    ImmutableDictionary<string, PropertyDefinition> Properties,
    ImmutableList<ArgumentDefinition> Argument);
