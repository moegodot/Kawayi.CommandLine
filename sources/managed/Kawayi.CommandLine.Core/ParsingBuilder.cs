// Copyright (c) 2026 MoeGodot<me@kawayi.moe>.
// Licensed under the GNU Affero General Public License v3-or-later license.

using System.Collections.Immutable;
using Kawayi.CommandLine.Abstractions;

namespace Kawayi.CommandLine.Core;

public sealed class ParsingBuilder : IParsingBuilder
{
    public static ParsingResult CreateParsing(ParsingOptions options, ImmutableArray<Token> arguments,
                                              IParsingResultCollection initialState) =>
        throw new NotImplementedException();

    public ParsingOptions ParsingOptions { get; }
    public ImmutableDictionary<string, CommandDefinition> Subcommands { get; }
    public ImmutableDictionary<string, PropertyDefinition> Properties { get; }
    public IList<ArgumentDefinition> Argument { get; }
}
