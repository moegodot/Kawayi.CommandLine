// Copyright (c) 2026 MoeGodot<me@kawayi.moe>.
// Licensed under the GNU Affero General Public License v3-or-later license.

using System.Collections.Immutable;
using System.Globalization;
using Kawayi.CommandLine.Abstractions;
using Kawayi.CommandLine.Core;

namespace Kawayi.CommandLine.Core.Primitives;

/// <summary>
/// Parses common framework types from command-line tokens.
/// </summary>
public sealed class CommonParser
    : Abstractions.IParsable<Guid>,
      Abstractions.IParsable<Uri>,
      Abstractions.IParsable<string>,
      Abstractions.IParsable<DateTime>,
      Abstractions.IParsable<DateTimeOffset>,
      Abstractions.IParsable<DateOnly>,
      Abstractions.IParsable<TimeOnly>
{
    /// <summary>
    /// Parses a <see cref="Guid"/> value from the supplied tokens.
    /// </summary>
    /// <param name="options">The parsing options for this operation.</param>
    /// <param name="arguments">The tokens to parse.</param>
    /// <param name="initialState">The fallback value used when no token is supplied.</param>
    /// <returns>The parsing result.</returns>
    public static ParsingResult CreateParsing(ParsingOptions options, ImmutableArray<Token> arguments, Guid initialState) =>
        Parse(options,
              arguments,
              initialState,
              "Guid",
              static (string value, out Guid result) => Guid.TryParse(value, out result));

    /// <summary>
    /// Parses a <see cref="string"/> value from the supplied tokens.
    /// </summary>
    /// <param name="options">The parsing options for this operation.</param>
    /// <param name="arguments">The tokens to parse.</param>
    /// <param name="initialState">The fallback value used when no token is supplied.</param>
    /// <returns>The parsing result.</returns>
    public static ParsingResult CreateParsing(ParsingOptions options, ImmutableArray<Token> arguments,
                                              string initialState) =>
        Parse(options,
              arguments,
              initialState,
              "string",
              static (string value, out string result) =>
              {
                  result = value;
                  return true;
              });

    /// <summary>
    /// Parses a <see cref="Uri"/> value from the supplied tokens.
    /// </summary>
    /// <param name="options">The parsing options for this operation.</param>
    /// <param name="arguments">The tokens to parse.</param>
    /// <param name="initialState">The fallback value used when no token is supplied.</param>
    /// <returns>The parsing result.</returns>
    public static ParsingResult CreateParsing(ParsingOptions options, ImmutableArray<Token> arguments, Uri initialState) =>
        Parse(options,
              arguments,
              initialState,
              "Uri at UriKind.RelativeOrAbsolute",
              static (string value, out Uri result) =>
              {
                  if (Uri.TryCreate(value, UriKind.RelativeOrAbsolute, out var uri) && uri is not null)
                  {
                      result = uri;
                      return true;
                  }

                  result = null!;
                  return false;
              });

    /// <summary>
    /// Parses a <see cref="DateTime"/> value from the supplied tokens.
    /// </summary>
    /// <param name="options">The parsing options for this operation.</param>
    /// <param name="arguments">The tokens to parse.</param>
    /// <param name="initialState">The fallback value used when no token is supplied.</param>
    /// <returns>The parsing result.</returns>
    public static ParsingResult CreateParsing(ParsingOptions options, ImmutableArray<Token> arguments, DateTime initialState) =>
        Parse(options,
              arguments,
              initialState,
              "DateTime at DateTimeStyles.None",
              static (string value, out DateTime result) =>
                  DateTime.TryParse(value, null, DateTimeStyles.None, out result));

    /// <summary>
    /// Parses a <see cref="DateTimeOffset"/> value from the supplied tokens.
    /// </summary>
    /// <param name="options">The parsing options for this operation.</param>
    /// <param name="arguments">The tokens to parse.</param>
    /// <param name="initialState">The fallback value used when no token is supplied.</param>
    /// <returns>The parsing result.</returns>
    public static ParsingResult CreateParsing(ParsingOptions options, ImmutableArray<Token> arguments, DateTimeOffset initialState) =>
        Parse(options,
              arguments,
              initialState,
              "DateTimeOffset at DateTimeStyles.None",
              static (string value, out DateTimeOffset result) =>
                  DateTimeOffset.TryParse(value, null, DateTimeStyles.None, out result));

    /// <summary>
    /// Parses a <see cref="DateOnly"/> value from the supplied tokens.
    /// </summary>
    /// <param name="options">The parsing options for this operation.</param>
    /// <param name="arguments">The tokens to parse.</param>
    /// <param name="initialState">The fallback value used when no token is supplied.</param>
    /// <returns>The parsing result.</returns>
    public static ParsingResult CreateParsing(ParsingOptions options, ImmutableArray<Token> arguments, DateOnly initialState) =>
        Parse(options,
              arguments,
              initialState,
              "DateOnly at DateTimeStyles.None",
              static (string value, out DateOnly result) =>
                  DateOnly.TryParse(value, null, DateTimeStyles.None, out result));

    /// <summary>
    /// Parses a <see cref="TimeOnly"/> value from the supplied tokens.
    /// </summary>
    /// <param name="options">The parsing options for this operation.</param>
    /// <param name="arguments">The tokens to parse.</param>
    /// <param name="initialState">The fallback value used when no token is supplied.</param>
    /// <returns>The parsing result.</returns>
    public static ParsingResult CreateParsing(ParsingOptions options, ImmutableArray<Token> arguments, TimeOnly initialState) =>
        Parse(options,
              arguments,
              initialState,
              "TimeOnly at DateTimeStyles.None",
              static (string value, out TimeOnly result) =>
                  TimeOnly.TryParse(value, null, DateTimeStyles.None, out result));

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
                                    new DebugContext(nameof(CommonParser),
                                                     Tokens: arguments,
                                                     TargetType: typeof(T),
                                                     Expectation: expect));
        }

        var token = arguments[^1];

        if (tryParse(token.Value, out var parsedValue))
        {
            return DebugOutput.Emit(options,
                                    new ParsingFinished<T>(parsedValue),
                                    new DebugContext(nameof(CommonParser),
                                                     Tokens: arguments,
                                                     TargetType: typeof(T),
                                                     Expectation: expect,
                                                     SelectedToken: selectedToken));
        }

        return DebugOutput.Emit(options,
                                new InvalidArgumentDetected(token.Value, expect, null),
                                new DebugContext(nameof(CommonParser),
                                                 Tokens: arguments,
                                                 TargetType: typeof(T),
                                                 Expectation: expect,
                                                 SelectedToken: selectedToken));
    }
}
