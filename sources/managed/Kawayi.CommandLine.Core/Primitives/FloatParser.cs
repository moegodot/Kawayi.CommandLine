// Copyright (c) 2026 MoeGodot<me@kawayi.moe>.
// Licensed under the GNU Affero General Public License v3-or-later license.

using System.Collections.Immutable;
using System.Globalization;
using Kawayi.CommandLine.Abstractions;

namespace Kawayi.CommandLine.Core.Primitives;

public sealed class FloatParser    : Abstractions.IParsable<float>,
                                     Abstractions.IParsable<double>,
                                     Abstractions.IParsable<decimal>
{
    public static NumberStyles DefaultNumberStyles { get; }
        = NumberStyles.Float;

    public static ParsingResult CreateParsing(ParsingOptions options, ImmutableArray<Token> arguments, float initialState) =>
        Parse(arguments,
              initialState,
              "float at NumberStyles.Float",
              static (string value, out float result) =>
                  float.TryParse(value, DefaultNumberStyles, null, out result));

    public static ParsingResult CreateParsing(ParsingOptions options, ImmutableArray<Token> arguments, double initialState) =>
        Parse(arguments,
              initialState,
              "double at NumberStyles.Float",
              static (string value, out double result) =>
                  double.TryParse(value, DefaultNumberStyles, null, out result));

    public static ParsingResult CreateParsing(ParsingOptions options, ImmutableArray<Token> arguments, decimal initialState) =>
        Parse(arguments,
              initialState,
              "decimal at NumberStyles.Float",
              static (string value, out decimal result) =>
                  decimal.TryParse(value, DefaultNumberStyles, null, out result));

    private delegate bool TryParseDelegate<T>(string value, out T result);

    private static ParsingResult Parse<T>(ImmutableArray<Token> arguments,
                                          T initialState,
                                          string expect,
                                          TryParseDelegate<T> tryParse)
    {
        if (arguments.IsDefaultOrEmpty)
        {
            return new ParsingFinished<T>(initialState);
        }

        var token = arguments[^1];

        if (tryParse(token.RawValue, out var result))
        {
            return new ParsingFinished<T>(result);
        }

        return new InvalidArgumentDetected(token.RawValue, expect, null);
    }
}
