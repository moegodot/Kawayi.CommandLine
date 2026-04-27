// Copyright (c) 2026 MoeGodot<me@kawayi.moe>.
// Licensed under the GNU Affero General Public License v3-or-later license.

using System.Collections.Immutable;
using Kawayi.CommandLine.Abstractions;

namespace Kawayi.CommandLine.Core.Primitives;

public sealed class BooleanParser : Abstractions.IParsable<bool>
{
    public static ParsingResult CreateParsing(ParsingOptions options,
                                              ImmutableArray<Token> arguments,
                                              bool initialState)
    {
        if (arguments.IsDefaultOrEmpty)
        {
            return new ParsingFinished<bool>(initialState);
        }

        var token = arguments[^1];

        if (bool.TryParse(token.RawValue, out var result))
        {
            return new ParsingFinished<bool>(result);
        }

        return new InvalidArgumentDetected(token.RawValue, "bool", null);
    }
}
