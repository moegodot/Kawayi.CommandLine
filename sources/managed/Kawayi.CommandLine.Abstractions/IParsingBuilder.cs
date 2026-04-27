// Copyright (c) 2026 MoeGodot<me@kawayi.moe>.
// Licensed under the GNU Affero General Public License v3-or-later license.

using System.Collections.Immutable;

namespace Kawayi.CommandLine.Abstractions;

public interface IParsingBuilder : IParsable<IParsingResultCollection>
{
    ParsingOptions ParsingOptions { get; }
    ImmutableDictionary<string,CommandDefinition> Subcommands { get; }
    ImmutableDictionary<string,PropertyDefinition> Properties { get; }
    IList<ArgumentDefinition> Argument { get; }
}
