// Copyright (c) 2026 MoeGodot<me@kawayi.moe>.
// Licensed under the GNU Affero General Public License v3-or-later license.

using System.Collections.Immutable;

namespace Kawayi.CommandLine.Abstractions;

public readonly record struct ParsingInput(
    ParsingOptions ParsingOptions,
    ImmutableDictionary<string, CommandDefinition> SubcommandDefinitions,
    ImmutableDictionary<string, ParsingInput> Subcommands,
    ImmutableDictionary<string, PropertyDefinition> Properties,
    ImmutableList<ArgumentDefinition> Argument);
