// Copyright (c) 2026 MoeGodot<me@kawayi.moe>.
// Licensed under the GNU Affero General Public License v3-or-later license.

using System.Collections.Immutable;
using System.Globalization;
using Kawayi.CommandLine.Abstractions;

namespace Kawayi.CommandLine.Core.Primitives;

public sealed class NumberParser
    : Abstractions.IParsable<byte>,
      Abstractions.IParsable<sbyte>,
      Abstractions.IParsable<ushort>,
      Abstractions.IParsable<short>,
      Abstractions.IParsable<int>,
      Abstractions.IParsable<uint>,
      Abstractions.IParsable<long>,
      Abstractions.IParsable<ulong>
{
    public static NumberStyles DefaultNumberStyles { get; }
        = NumberStyles.Integer;

    public static ParsingResult CreateParsing(ParsingOptions options,
                                              ImmutableArray<Token> arguments,
                                              byte initialState) =>
        Parse(arguments,
              initialState,
              "byte at NumberStyles.Integer",
              static (string value, out byte result) =>
                  byte.TryParse(value, DefaultNumberStyles, null, out result));

    public static ParsingResult CreateParsing(ParsingOptions options, ImmutableArray<Token> arguments, sbyte initialState) =>
        Parse(arguments,
              initialState,
              "sbyte at NumberStyles.Integer",
              static (string value, out sbyte result) =>
                  sbyte.TryParse(value, DefaultNumberStyles, null, out result));

    public static ParsingResult CreateParsing(ParsingOptions options, ImmutableArray<Token> arguments, ushort initialState) =>
        Parse(arguments,
              initialState,
              "ushort at NumberStyles.Integer",
              static (string value, out ushort result) =>
                  ushort.TryParse(value, DefaultNumberStyles, null, out result));

    public static ParsingResult CreateParsing(ParsingOptions options, ImmutableArray<Token> arguments, short initialState) =>
        Parse(arguments,
              initialState,
              "short at NumberStyles.Integer",
              static (string value, out short result) =>
                  short.TryParse(value, DefaultNumberStyles, null, out result));

    public static ParsingResult CreateParsing(ParsingOptions options, ImmutableArray<Token> arguments, int initialState) =>
        Parse(arguments,
              initialState,
              "int at NumberStyles.Integer",
              static (string value, out int result) =>
                  int.TryParse(value, DefaultNumberStyles, null, out result));

    public static ParsingResult CreateParsing(ParsingOptions options, ImmutableArray<Token> arguments, uint initialState) =>
        Parse(arguments,
              initialState,
              "uint at NumberStyles.Integer",
              static (string value, out uint result) =>
                  uint.TryParse(value, DefaultNumberStyles, null, out result));

    public static ParsingResult CreateParsing(ParsingOptions options, ImmutableArray<Token> arguments, long initialState) =>
        Parse(arguments,
              initialState,
              "long at NumberStyles.Integer",
              static (string value, out long result) =>
                  long.TryParse(value, DefaultNumberStyles, null, out result));

    public static ParsingResult CreateParsing(ParsingOptions options, ImmutableArray<Token> arguments, ulong initialState) =>
        Parse(arguments,
              initialState,
              "ulong at NumberStyles.Integer",
              static (string value, out ulong result) =>
                  ulong.TryParse(value, DefaultNumberStyles, null, out result));

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
