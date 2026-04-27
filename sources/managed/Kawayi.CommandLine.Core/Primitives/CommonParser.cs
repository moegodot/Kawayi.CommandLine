// Copyright (c) 2026 MoeGodot<me@kawayi.moe>.
// Licensed under the GNU Affero General Public License v3-or-later license.

using System.Collections.Immutable;
using System.Globalization;
using Kawayi.CommandLine.Abstractions;

namespace Kawayi.CommandLine.Core.Primitives;

public sealed class CommonParser
    : Abstractions.IParsable<Guid>,
      Abstractions.IParsable<Uri>,
      Abstractions.IParsable<string>,
      Abstractions.IParsable<DateTime>,
      Abstractions.IParsable<DateTimeOffset>,
      Abstractions.IParsable<DateOnly>,
      Abstractions.IParsable<TimeOnly>
{
    public static ParsingResult CreateParsing(ParsingOptions options, ImmutableArray<Token> arguments, Guid initialState) =>
        Parse(arguments,
              initialState,
              "Guid",
              static (string value, out Guid result) => Guid.TryParse(value, out result));

    public static ParsingResult CreateParsing(ParsingOptions options, ImmutableArray<Token> arguments,
                                              string initialState) =>
        Parse(arguments,
              initialState,
              "string",
              static (string value, out string result) =>
              {
                  result = value;
                  return true;
              });

    public static ParsingResult CreateParsing(ParsingOptions options, ImmutableArray<Token> arguments, Uri initialState) =>
        Parse(arguments,
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

    public static ParsingResult CreateParsing(ParsingOptions options, ImmutableArray<Token> arguments, DateTime initialState) =>
        Parse(arguments,
              initialState,
              "DateTime at DateTimeStyles.None",
              static (string value, out DateTime result) =>
                  DateTime.TryParse(value, null, DateTimeStyles.None, out result));

    public static ParsingResult CreateParsing(ParsingOptions options, ImmutableArray<Token> arguments, DateTimeOffset initialState) =>
        Parse(arguments,
              initialState,
              "DateTimeOffset at DateTimeStyles.None",
              static (string value, out DateTimeOffset result) =>
                  DateTimeOffset.TryParse(value, null, DateTimeStyles.None, out result));

    public static ParsingResult CreateParsing(ParsingOptions options, ImmutableArray<Token> arguments, DateOnly initialState) =>
        Parse(arguments,
              initialState,
              "DateOnly at DateTimeStyles.None",
              static (string value, out DateOnly result) =>
                  DateOnly.TryParse(value, null, DateTimeStyles.None, out result));

    public static ParsingResult CreateParsing(ParsingOptions options, ImmutableArray<Token> arguments, TimeOnly initialState) =>
        Parse(arguments,
              initialState,
              "TimeOnly at DateTimeStyles.None",
              static (string value, out TimeOnly result) =>
                  TimeOnly.TryParse(value, null, DateTimeStyles.None, out result));

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
