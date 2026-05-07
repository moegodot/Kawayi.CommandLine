// Copyright (c) 2026 GodotAsync<me@kawayi.moe>.
// Licensed under the GNU Affero General Public License v3-or-later license.

using System.Collections.Immutable;
using Kawayi.CommandLine.Abstractions;

namespace Kawayi.CommandLine.Core;

public class CliSchemaParser
    : Abstractions.IParsable<CliSchema>
{
    public static ParsingResult CreateParsing(ParsingOptions options, ImmutableArray<Token> arguments, CliSchema initialState) => throw new NotImplementedException();
}
