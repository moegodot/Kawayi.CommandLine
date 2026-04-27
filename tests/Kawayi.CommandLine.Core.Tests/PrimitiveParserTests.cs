// Copyright (c) 2026 MoeGodot<me@kawayi.moe>.
// Licensed under the GNU Affero General Public License v3-or-later license.

using System.Collections.Immutable;
using Kawayi.CommandLine.Abstractions;
using Kawayi.CommandLine.Core.Primitives;

namespace Kawayi.CommandLine.Core.Tests;

public sealed class PrimitiveParserTests
{
    private static readonly ParsingOptions DefaultOptions = new(
        new ProgramInformation("test", "test", "test", new Version(1, 0), "https://example.com"));

    [Test]
    public async Task NumberParser_Returns_Initial_State_When_No_Arguments_Are_Provided()
    {
        var result = NumberParser.CreateParsing(DefaultOptions, [], 42);

        await AssertParsingFinished(result, 42);
    }

    [Test]
    public async Task NumberParser_Uses_The_Last_Token()
    {
        ImmutableArray<Token> arguments =
        [
            new ArgumentOrCommandToken("1"),
            new ArgumentOrCommandToken("2")
        ];

        var result = NumberParser.CreateParsing(DefaultOptions, arguments, 0);

        await AssertParsingFinished(result, 2);
    }

    [Test]
    [Arguments("127")]
    [Arguments("-32768")]
    [Arguments("65535")]
    [Arguments("4294967295")]
    [Arguments("9223372036854775807")]
    [Arguments("18446744073709551615")]
    public async Task NumberParser_Parses_Supported_Integer_Types(string rawValue)
    {
        ImmutableArray<Token> arguments = [new ArgumentOrCommandToken(rawValue)];

        switch (rawValue)
        {
            case "127":
                await AssertParsingFinished(
                    NumberParser.CreateParsing(DefaultOptions, arguments, (sbyte)0),
                    (sbyte)127);
                return;
            case "-32768":
                await AssertParsingFinished(
                    NumberParser.CreateParsing(DefaultOptions, arguments, (short)0),
                    (short)-32768);
                return;
            case "65535":
                await AssertParsingFinished(
                    NumberParser.CreateParsing(DefaultOptions, arguments, (ushort)0),
                    ushort.MaxValue);
                return;
            case "4294967295":
                await AssertParsingFinished(
                    NumberParser.CreateParsing(DefaultOptions, arguments, 0u),
                    uint.MaxValue);
                return;
            case "9223372036854775807":
                await AssertParsingFinished(
                    NumberParser.CreateParsing(DefaultOptions, arguments, 0L),
                    long.MaxValue);
                return;
            case "18446744073709551615":
                await AssertParsingFinished(
                    NumberParser.CreateParsing(DefaultOptions, arguments, 0UL),
                    ulong.MaxValue);
                return;
            default:
                throw new InvalidOperationException($"Unsupported raw value '{rawValue}'.");
        }
    }

    [Test]
    public async Task NumberParser_Returns_InvalidArgument_For_Invalid_Integer_Input()
    {
        ImmutableArray<Token> arguments = [new ArgumentOrCommandToken("12.5")];

        var result = NumberParser.CreateParsing(DefaultOptions, arguments, 0);

        await AssertInvalidArgument(result, "12.5", "int at NumberStyles.Integer");
    }

    [Test]
    [Arguments("3.5", 3.5f)]
    [Arguments("-12.25", -12.25d)]
    [Arguments("79228162514264337593543950335", "79228162514264337593543950335")]
    public async Task FloatParser_Parses_Supported_Floating_Types(string rawValue, object expected)
    {
        ImmutableArray<Token> arguments = [new ArgumentOrCommandToken(rawValue)];

        ParsingResult result = expected switch
        {
            float value => FloatParser.CreateParsing(DefaultOptions, arguments, value + 1),
            double value => FloatParser.CreateParsing(DefaultOptions, arguments, value + 1),
            string value => FloatParser.CreateParsing(DefaultOptions, arguments, decimal.Zero),
            _ => throw new InvalidOperationException($"Unsupported expected type: {expected.GetType().FullName}")
        };

        var normalizedExpected = expected is string decimalRaw
            ? decimal.Parse(decimalRaw)
            : expected;

        await AssertParsingFinished(result, normalizedExpected);
    }

    [Test]
    public async Task FloatParser_Returns_Initial_State_When_No_Arguments_Are_Provided()
    {
        var result = FloatParser.CreateParsing(DefaultOptions, [], 4.25m);

        await AssertParsingFinished(result, 4.25m);
    }

    [Test]
    public async Task FloatParser_Returns_InvalidArgument_For_Invalid_Decimal_Input()
    {
        ImmutableArray<Token> arguments = [new ArgumentOrCommandToken("not-a-number")];

        var result = FloatParser.CreateParsing(DefaultOptions, arguments, decimal.Zero);

        await AssertInvalidArgument(result, "not-a-number", "decimal at NumberStyles.Float");
    }

    [Test]
    [Arguments("true", true)]
    [Arguments("FALSE", false)]
    public async Task BooleanParser_Parses_True_And_False(string rawValue, bool expected)
    {
        ImmutableArray<Token> arguments = [new ArgumentOrCommandToken(rawValue)];

        var result = BooleanParser.CreateParsing(DefaultOptions, arguments, !expected);

        await AssertParsingFinished(result, expected);
    }

    [Test]
    [Arguments("1")]
    [Arguments("yes")]
    public async Task BooleanParser_Rejects_Non_Bcl_Boolean_Literals(string rawValue)
    {
        ImmutableArray<Token> arguments = [new ArgumentOrCommandToken(rawValue)];

        var result = BooleanParser.CreateParsing(DefaultOptions, arguments, false);

        await AssertInvalidArgument(result, rawValue, "bool");
    }

    [Test]
    public async Task CommonParser_Parses_Guid()
    {
        var expected = Guid.Parse("2b5c66c5-f2a0-4720-a8f3-80abf153e8d3");
        ImmutableArray<Token> arguments = [new ArgumentOrCommandToken(expected.ToString())];

        var result = CommonParser.CreateParsing(DefaultOptions, arguments, Guid.Empty);

        await AssertParsingFinished(result, expected);
    }

    [Test]
    public async Task CommonParser_Parses_Absolute_And_Relative_Uri()
    {
        ImmutableArray<Token> absoluteArguments = [new ArgumentOrCommandToken("https://example.com/path?q=1")];
        ImmutableArray<Token> relativeArguments = [new ArgumentOrCommandToken("docs/guide")];

        var absoluteResult = CommonParser.CreateParsing(DefaultOptions, absoluteArguments, new Uri("https://fallback.example"));
        var relativeResult = CommonParser.CreateParsing(DefaultOptions, relativeArguments, new Uri("https://fallback.example"));

        await AssertParsingFinished(absoluteResult, new Uri("https://example.com/path?q=1"));
        await AssertParsingFinished(relativeResult, new Uri("docs/guide", UriKind.Relative));
    }

    [Test]
    public async Task CommonParser_Parses_Date_And_Time_Types()
    {
        ImmutableArray<Token> dateTimeArguments = [new ArgumentOrCommandToken("2026-04-27T12:34:56")];
        ImmutableArray<Token> dateTimeOffsetArguments = [new ArgumentOrCommandToken("2026-04-27T12:34:56+08:00")];
        ImmutableArray<Token> dateOnlyArguments = [new ArgumentOrCommandToken("2026-04-27")];
        ImmutableArray<Token> timeOnlyArguments = [new ArgumentOrCommandToken("12:34:56")];

        var dateTimeResult = CommonParser.CreateParsing(DefaultOptions, dateTimeArguments, default(DateTime));
        var dateTimeOffsetResult = CommonParser.CreateParsing(DefaultOptions, dateTimeOffsetArguments, default(DateTimeOffset));
        var dateOnlyResult = CommonParser.CreateParsing(DefaultOptions, dateOnlyArguments, default(DateOnly));
        var timeOnlyResult = CommonParser.CreateParsing(DefaultOptions, timeOnlyArguments, default(TimeOnly));

        await AssertParsingFinished(dateTimeResult, DateTime.Parse("2026-04-27T12:34:56"));
        await AssertParsingFinished(dateTimeOffsetResult, DateTimeOffset.Parse("2026-04-27T12:34:56+08:00"));
        await AssertParsingFinished(dateOnlyResult, DateOnly.Parse("2026-04-27"));
        await AssertParsingFinished(timeOnlyResult, TimeOnly.Parse("12:34:56"));
    }

    [Test]
    [Arguments("not-a-guid", "Guid")]
    [Arguments("http://[", "Uri at UriKind.RelativeOrAbsolute")]
    [Arguments("2026-99-99", "DateTime at DateTimeStyles.None")]
    [Arguments("2026-04-27T12:34:56+99:00", "DateTimeOffset at DateTimeStyles.None")]
    [Arguments("2026-15-01", "DateOnly at DateTimeStyles.None")]
    [Arguments("25:61:00", "TimeOnly at DateTimeStyles.None")]
    public async Task CommonParser_Returns_InvalidArgument_For_Invalid_Inputs(string rawValue, string expectedDescription)
    {
        ImmutableArray<Token> arguments = [new ArgumentOrCommandToken(rawValue)];

        ParsingResult result = expectedDescription switch
        {
            "Guid" => CommonParser.CreateParsing(DefaultOptions, arguments, Guid.Empty),
            "Uri at UriKind.RelativeOrAbsolute" => CommonParser.CreateParsing(DefaultOptions, arguments, new Uri("https://fallback.example")),
            "DateTime at DateTimeStyles.None" => CommonParser.CreateParsing(DefaultOptions, arguments, default(DateTime)),
            "DateTimeOffset at DateTimeStyles.None" => CommonParser.CreateParsing(DefaultOptions, arguments, default(DateTimeOffset)),
            "DateOnly at DateTimeStyles.None" => CommonParser.CreateParsing(DefaultOptions, arguments, default(DateOnly)),
            "TimeOnly at DateTimeStyles.None" => CommonParser.CreateParsing(DefaultOptions, arguments, default(TimeOnly)),
            _ => throw new InvalidOperationException($"Unsupported expected description: {expectedDescription}")
        };

        await AssertInvalidArgument(result, rawValue, expectedDescription);
    }

    [Test]
    public async Task CommonParser_Returns_Initial_State_When_No_Arguments_Are_Provided()
    {
        var initialState = new Uri("https://initial.example/path");

        var result = CommonParser.CreateParsing(DefaultOptions, [], initialState);

        await AssertParsingFinished(result, initialState);
    }

    private static async Task AssertParsingFinished<T>(ParsingResult result, T expected)
    {
        if (result is not ParsingFinished finished)
        {
            throw new InvalidOperationException($"Expected {nameof(ParsingFinished)}, got {result.GetType().FullName}.");
        }

        if (!Equals(finished.UntypedResult, expected))
        {
            throw new InvalidOperationException($"Expected parsed value '{expected}', got '{finished.UntypedResult}'.");
        }

        await Assert.That(finished.UntypedResult.GetType()).IsEqualTo(expected!.GetType());
    }

    private static async Task AssertInvalidArgument(ParsingResult result, string argument, string expect)
    {
        if (result is not InvalidArgumentDetected invalid)
        {
            throw new InvalidOperationException($"Expected {nameof(InvalidArgumentDetected)}, got {result.GetType().FullName}.");
        }

        await Assert.That(invalid.Argument).EqualTo(argument);
        await Assert.That(invalid.Expect).EqualTo(expect);
        await Assert.That(invalid.Exception).EqualTo(null);
    }
}
