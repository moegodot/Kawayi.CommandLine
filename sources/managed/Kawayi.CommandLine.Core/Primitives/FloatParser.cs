// Copyright (c) 2026 MoeGodot<me@kawayi.moe>.
// Licensed under the GNU Affero General Public License v3-or-later license.

using System.Collections.Immutable;
using System.Globalization;
using Kawayi.CommandLine.Abstractions;
using Kawayi.CommandLine.Core;

namespace Kawayi.CommandLine.Core.Primitives;

public sealed class FloatParser    : Abstractions.IParsable<float>,
                                     Abstractions.IParsable<double>,
                                     Abstractions.IParsable<decimal>
{
    public static NumberStyles DefaultNumberStyles { get; }
        = NumberStyles.Float;

    public static ParsingResult CreateParsing(ParsingOptions options, ImmutableArray<Token> arguments, float initialState) =>
        Parse(options,
              arguments,
              initialState,
              "float at NumberStyles.Float",
              static (string value, out float result) =>
                  float.TryParse(value, DefaultNumberStyles, null, out result));

    public static ParsingResult CreateParsing(ParsingOptions options, ImmutableArray<Token> arguments, double initialState) =>
        Parse(options,
              arguments,
              initialState,
              "double at NumberStyles.Float",
              static (string value, out double result) =>
                  double.TryParse(value, DefaultNumberStyles, null, out result));

    public static ParsingResult CreateParsing(ParsingOptions options, ImmutableArray<Token> arguments, decimal initialState) =>
        Parse(options,
              arguments,
              initialState,
              "decimal at NumberStyles.Float",
              static (string value, out decimal result) =>
                  decimal.TryParse(value, DefaultNumberStyles, null, out result));

    private delegate bool TryParseDelegate<T>(string value, out T result);

    private static ParsingResult Parse<T>(ParsingOptions options,
                                          ImmutableArray<Token> arguments,
                                          T initialState,
                                          string expect,
                                          TryParseDelegate<T> tryParse)
    {
        var selectedToken = arguments.IsDefaultOrEmpty ? null : arguments[^1].Value;

        if (arguments.IsDefaultOrEmpty)
        {
            return DebugOutput.Emit(options,
                                    new ParsingFinished<T>(initialState),
                                    new DebugContext(nameof(FloatParser),
                                                     Tokens: arguments,
                                                     TargetType: typeof(T),
                                                     Expectation: expect));
        }

        var token = arguments[^1];

        if (tryParse(token.Value, out var parsedValue))
        {
            return DebugOutput.Emit(options,
                                    new ParsingFinished<T>(parsedValue),
                                    new DebugContext(nameof(FloatParser),
                                                     Tokens: arguments,
                                                     TargetType: typeof(T),
                                                     Expectation: expect,
                                                     SelectedToken: selectedToken));
        }

        return DebugOutput.Emit(options,
                                new InvalidArgumentDetected(token.Value, expect, null),
                                new DebugContext(nameof(FloatParser),
                                                 Tokens: arguments,
                                                 TargetType: typeof(T),
                                                 Expectation: expect,
                                                 SelectedToken: selectedToken));
    }
}
