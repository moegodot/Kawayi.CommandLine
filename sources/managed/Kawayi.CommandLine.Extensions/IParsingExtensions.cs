// Copyright (c) 2026 MoeGodot<me@kawayi.moe>.
// Licensed under the GNU Affero General Public License v3-or-later license.

using Kawayi.CommandLine.Abstractions;
using Kawayi.CommandLine.Core;

namespace Kawayi.CommandLine.Extensions;

public static class IParsingExtensions
{
    extension(IParsing parsing)
    {
        ParsingResult ParseToFinished()
        {
            var result = parsing.Parse();

            while (result is not ParsingFinished finished)
            {
                switch (result)
                {
                    case ShouldExit exit:
                        return exit;
                    case NeedToParse parse:
                        result = parse.ArgsParsing.Parse();
                        break;
                }
            }
            return result;
        }
    }
}
