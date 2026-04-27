// Copyright (c) 2026 MoeGodot<me@kawayi.moe>.
// Licensed under the GNU Affero General Public License v3-or-later license.

using System.Collections.Immutable;
using Kawayi.CommandLine.Abstractions;

namespace Kawayi.CommandLine.Extensions;

public static class IParsableExtension
{
    extension<T,TU>(TU parsable)
        where T:Abstractions.IParsable<TU>
        where TU:new()
    {
        public static ParsingResult CreateParsing(ParsingOptions options, ImmutableArray<Token> arguments)
        {
            return T.CreateParsing(options, arguments,new TU());
        }

        public ParsingResult Parse(ParsingOptions options, ImmutableArray<Token> arguments)
        {
            return T.CreateParsing(options, arguments, parsable);
        }
    }
}
