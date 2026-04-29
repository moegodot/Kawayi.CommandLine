// Copyright (c) 2026 MoeGodot<me@kawayi.moe>.
// Licensed under the GNU Affero General Public License v3-or-later license.

using System.Collections.Immutable;

namespace Kawayi.CommandLine.Abstractions;

public interface IParsingBuilder
{
    ParsingOptions ParsingOptions { get; }
    ImmutableDictionary<string, CommandDefinition>.Builder SubcommandDefinitions { get; }
    ImmutableDictionary<string, IParsingBuilder>.Builder Subcommands { get; }
    ImmutableDictionary<string, PropertyDefinition>.Builder Properties { get; }
    ImmutableList<ArgumentDefinition>.Builder Argument { get; }
    ParsingInput Build();
}
