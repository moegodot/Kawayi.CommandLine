// Copyright (c) 2026 MoeGodot<me@kawayi.moe>.
// Licensed under the GNU Affero General Public License v3-or-later license.

using System.Collections.Immutable;
using System.Globalization;
using Kawayi.CommandLine.Abstractions;
using Kawayi.CommandLine.Core;

namespace Kawayi.CommandLine.Core.Primitives;

/// <summary>
/// Parses floating-point numeric types from command-line tokens.
/// </summary>
public sealed class FloatParser : Abstractions.IParsable<float>,
                                     Abstractions.IParsable<double>,
                                     Abstractions.IParsable<decimal>
{
    /// <summary>
    /// Gets the default number styles used for floating-point parsing.
    /// </summary>
    public static NumberStyles DefaultNumberStyles { get; }
        = NumberStyles.Float;

    /// <summary>
    /// Parses a <see cref="float"/> value from the supplied tokens.
    /// </summary>
    /// <param name="options">The parsing options for this operation.</param>
    /// <param name="arguments">The tokens to parse.</param>
    /// <param name="initialState">The fallback value used when no token is supplied.</param>
    /// <returns>The parsing result.</returns>
    public static ParsingResult CreateParsing(ParsingOptions options, ImmutableArray<Token> arguments, float initialState) =>
        Parse(options,
              arguments,
              initialState,
              "float at NumberStyles.Float",
              static (string value, out float result) =>
                  float.TryParse(value, DefaultNumberStyles, CultureInfo.InvariantCulture, out result));

    /// <summary>
    /// Parses a <see cref="double"/> value from the supplied tokens.
    /// </summary>
    /// <param name="options">The parsing options for this operation.</param>
    /// <param name="arguments">The tokens to parse.</param>
    /// <param name="initialState">The fallback value used when no token is supplied.</param>
    /// <returns>The parsing result.</returns>
    public static ParsingResult CreateParsing(ParsingOptions options, ImmutableArray<Token> arguments, double initialState) =>
        Parse(options,
              arguments,
              initialState,
              "double at NumberStyles.Float",
              static (string value, out double result) =>
                  double.TryParse(value, DefaultNumberStyles, CultureInfo.InvariantCulture, out result));

    /// <summary>
    /// Parses a <see cref="decimal"/> value from the supplied tokens.
    /// </summary>
    /// <param name="options">The parsing options for this operation.</param>
    /// <param name="arguments">The tokens to parse.</param>
    /// <param name="initialState">The fallback value used when no token is supplied.</param>
    /// <returns>The parsing result.</returns>
    public static ParsingResult CreateParsing(ParsingOptions options, ImmutableArray<Token> arguments, decimal initialState) =>
        Parse(options,
              arguments,
              initialState,
              "decimal at NumberStyles.Float",
              static (string value, out decimal result) =>
                  decimal.TryParse(value, DefaultNumberStyles, CultureInfo.InvariantCulture, out result));

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
