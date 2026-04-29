// Copyright (c) 2026 MoeGodot<me@kawayi.moe>.
// Licensed under the GNU Affero General Public License v3-or-later license.

using System.Collections.Immutable;
using Kawayi.CommandLine.Abstractions;

namespace Kawayi.CommandLine.Core.Primitives;

public sealed class EnumParser
{
    public static ParsingResult CreateParsing(ParsingOptions options,
                                              ImmutableArray<Token> arguments,
                                              Type enumType,
                                              object initialState)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(enumType);

        if (!enumType.IsEnum)
        {
            throw new ArgumentException($"Type '{enumType.FullName}' is not an enum.", nameof(enumType));
        }

        var selectedToken = arguments.IsDefaultOrEmpty ? null : arguments[^1].Value;
        var expectation = $"{enumType.Name} enum";

        if (arguments.IsDefaultOrEmpty)
        {
            return DebugOutput.Emit(options,
                                    new ParsingFinished<object>(initialState),
                                    new DebugContext(nameof(EnumParser),
                                                     Tokens: arguments,
                                                     TargetType: enumType,
                                                     Expectation: expectation));
        }

        var token = arguments[^1];

        if (Enum.TryParse(enumType, token.Value, true, out var parsedValue) && parsedValue is not null)
        {
            return DebugOutput.Emit(options,
                                    new ParsingFinished<object>(parsedValue),
                                    new DebugContext(nameof(EnumParser),
                                                     Tokens: arguments,
                                                     TargetType: enumType,
                                                     Expectation: expectation,
                                                     SelectedToken: selectedToken));
        }

        return DebugOutput.Emit(options,
                                new InvalidArgumentDetected(token.Value, expectation, null),
                                new DebugContext(nameof(EnumParser),
                                                 Tokens: arguments,
                                                 TargetType: enumType,
                                                 Expectation: expectation,
                                                 SelectedToken: selectedToken));
    }
}
