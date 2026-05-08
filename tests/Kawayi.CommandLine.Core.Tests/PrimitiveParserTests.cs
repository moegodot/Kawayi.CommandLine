// Copyright (c) 2026 MoeGodot<me@kawayi.moe>.
// Licensed under the GNU Affero General Public License v3-or-later license.

using System.Collections.Immutable;
using System.Globalization;
using Kawayi.CommandLine.Abstractions;

namespace Kawayi.CommandLine.Core.Tests;

public sealed class PrimitiveParserTests
{
    private static readonly ParsingOptions DefaultOptions = new(
        new ProgramInformation("test", new("test", "test"), new Version(1, 0), "https://example.com"));

    [Test]
    public async Task NumberParser_Returns_Default_Value_When_No_Arguments_Are_Provided()
    {
        await AssertParsingFinished(ParseExact(typeof(int), []), 0);
    }

    [Test]
    public async Task NumberParser_Uses_The_Last_Token()
    {
        ImmutableArray<Token> arguments =
        [
            new ArgumentOrCommandToken("1"),
            new ArgumentOrCommandToken("2")
        ];

        await AssertParsingFinished(ParseExact(typeof(int), arguments), 2);
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
                await AssertParsingFinished(ParseExact(typeof(sbyte), arguments), (sbyte)127);
                return;
            case "-32768":
                await AssertParsingFinished(ParseExact(typeof(short), arguments), (short)-32768);
                return;
            case "65535":
                await AssertParsingFinished(ParseExact(typeof(ushort), arguments), ushort.MaxValue);
                return;
            case "4294967295":
                await AssertParsingFinished(ParseExact(typeof(uint), arguments), uint.MaxValue);
                return;
            case "9223372036854775807":
                await AssertParsingFinished(ParseExact(typeof(long), arguments), long.MaxValue);
                return;
            case "18446744073709551615":
                await AssertParsingFinished(ParseExact(typeof(ulong), arguments), ulong.MaxValue);
                return;
            default:
                throw new InvalidOperationException($"Unsupported raw value '{rawValue}'.");
        }
    }

    [Test]
    public async Task NumberParser_Returns_InvalidArgument_For_Invalid_Integer_Input()
    {
        ImmutableArray<Token> arguments = [new ArgumentOrCommandToken("12.5")];

        await AssertInvalidArgument(ParseExact(typeof(int), arguments), "12.5", "Int32 at NumberStyles.Integer");
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
            float => ParseExact(typeof(float), arguments),
            double => ParseExact(typeof(double), arguments),
            string => ParseExact(typeof(decimal), arguments),
            _ => throw new InvalidOperationException($"Unsupported expected type: {expected.GetType().FullName}")
        };

        var normalizedExpected = expected is string decimalRaw
            ? decimal.Parse(decimalRaw, CultureInfo.InvariantCulture)
            : expected;

        await AssertParsingFinished(result, normalizedExpected);
    }

    [Test]
    public async Task FloatParser_Returns_Default_Value_When_No_Arguments_Are_Provided()
    {
        await AssertParsingFinished(ParseExact(typeof(decimal), []), 0m);
    }

    [Test]
    public async Task FloatParser_Returns_InvalidArgument_For_Invalid_Decimal_Input()
    {
        ImmutableArray<Token> arguments = [new ArgumentOrCommandToken("not-a-number")];

        await AssertInvalidArgument(ParseExact(typeof(decimal), arguments), "not-a-number", "Decimal at NumberStyles.Float");
    }

    [Test]
    public async Task Numeric_And_Date_Parsers_Use_Invariant_Culture()
    {
        var previousCulture = CultureInfo.CurrentCulture;
        var previousUiCulture = CultureInfo.CurrentUICulture;

        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("fr-FR");

            await AssertParsingFinished(ParseExact(typeof(decimal), [new ArgumentOrCommandToken("3.5")]), 3.5m);
            await AssertParsingFinished(ParseExact(typeof(DateOnly), [new ArgumentOrCommandToken("2026-05-08")]), new DateOnly(2026, 5, 8));
            await AssertParsingFinished(ParseExact(typeof(TimeOnly), [new ArgumentOrCommandToken("12:34:56")]), new TimeOnly(12, 34, 56));
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
            CultureInfo.CurrentUICulture = previousUiCulture;
        }
    }

    [Test]
    [Arguments("true", true)]
    [Arguments("FALSE", false)]
    public async Task BooleanParser_Parses_True_And_False(string rawValue, bool expected)
    {
        ImmutableArray<Token> arguments = [new ArgumentOrCommandToken(rawValue)];

        await AssertParsingFinished(ParseExact(typeof(bool), arguments), expected);
    }

    [Test]
    [Arguments("1")]
    [Arguments("yes")]
    public async Task BooleanParser_Rejects_Non_Bcl_Boolean_Literals(string rawValue)
    {
        ImmutableArray<Token> arguments = [new ArgumentOrCommandToken(rawValue)];

        await AssertInvalidArgument(ParseExact(typeof(bool), arguments), rawValue, "bool");
    }

    [Test]
    public async Task CommonParser_Parses_Guid()
    {
        var expected = Guid.Parse("2b5c66c5-f2a0-4720-a8f3-80abf153e8d3");
        ImmutableArray<Token> arguments = [new ArgumentOrCommandToken(expected.ToString())];

        await AssertParsingFinished(ParseExact(typeof(Guid), arguments), expected);
    }

    [Test]
    public async Task CommonParser_Parses_Absolute_And_Relative_Uri()
    {
        ImmutableArray<Token> absoluteArguments = [new ArgumentOrCommandToken("https://example.com/path?q=1")];
        ImmutableArray<Token> relativeArguments = [new ArgumentOrCommandToken("docs/guide")];

        await AssertParsingFinished(ParseExact(typeof(Uri), absoluteArguments), new Uri("https://example.com/path?q=1"));
        await AssertParsingFinished(ParseExact(typeof(Uri), relativeArguments), new Uri("docs/guide", UriKind.Relative));
    }

    [Test]
    public async Task CommonParser_Parses_Date_And_Time_Types()
    {
        await AssertParsingFinished(ParseExact(typeof(DateTime), [new ArgumentOrCommandToken("2026-04-27T12:34:56")]), DateTime.Parse("2026-04-27T12:34:56", CultureInfo.InvariantCulture));
        await AssertParsingFinished(ParseExact(typeof(DateTimeOffset), [new ArgumentOrCommandToken("2026-04-27T12:34:56+08:00")]), DateTimeOffset.Parse("2026-04-27T12:34:56+08:00", CultureInfo.InvariantCulture));
        await AssertParsingFinished(ParseExact(typeof(DateOnly), [new ArgumentOrCommandToken("2026-04-27")]), DateOnly.Parse("2026-04-27", CultureInfo.InvariantCulture));
        await AssertParsingFinished(ParseExact(typeof(TimeOnly), [new ArgumentOrCommandToken("12:34:56")]), TimeOnly.Parse("12:34:56", CultureInfo.InvariantCulture));
    }

    [Test]
    public async Task CommonParser_Parses_Date_And_Time_Types_With_Exact_Formats()
    {
        await AssertParsingFinished(ParseExact(typeof(DateTime), [new ArgumentOrCommandToken("20260508-091011")], "yyyyMMdd-HHmmss"), new DateTime(2026, 5, 8, 9, 10, 11));
        await AssertParsingFinished(ParseExact(typeof(DateTimeOffset), [new ArgumentOrCommandToken("20260508-091011+00:00")], "yyyyMMdd-HHmmsszzz"), new DateTimeOffset(2026, 5, 8, 9, 10, 11, TimeSpan.Zero));
        await AssertParsingFinished(ParseExact(typeof(DateOnly), [new ArgumentOrCommandToken("20260508")], "yyyyMMdd"), new DateOnly(2026, 5, 8));
        await AssertParsingFinished(ParseExact(typeof(TimeOnly), [new ArgumentOrCommandToken("091011")], "HHmmss"), new TimeOnly(9, 10, 11));
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

        var targetType = expectedDescription switch
        {
            "Guid" => typeof(Guid),
            "Uri at UriKind.RelativeOrAbsolute" => typeof(Uri),
            "DateTime at DateTimeStyles.None" => typeof(DateTime),
            "DateTimeOffset at DateTimeStyles.None" => typeof(DateTimeOffset),
            "DateOnly at DateTimeStyles.None" => typeof(DateOnly),
            "TimeOnly at DateTimeStyles.None" => typeof(TimeOnly),
            _ => throw new InvalidOperationException($"Unsupported expected description: {expectedDescription}")
        };

        await AssertInvalidArgument(ParseExact(targetType, arguments), rawValue, expectedDescription);
    }

    [Test]
    public async Task CommonParser_Returns_InvalidArgument_For_Mismatched_Exact_Formats()
    {
        await AssertInvalidArgument(ParseExact(typeof(DateTime), [new ArgumentOrCommandToken("2026-05-08T09:10:11")], "yyyyMMdd-HHmmss"), "2026-05-08T09:10:11", "DateTime at exact format 'yyyyMMdd-HHmmss'");
        await AssertInvalidArgument(ParseExact(typeof(DateTimeOffset), [new ArgumentOrCommandToken("2026-05-08T09:10:11+00:00")], "yyyyMMdd-HHmmsszzz"), "2026-05-08T09:10:11+00:00", "DateTimeOffset at exact format 'yyyyMMdd-HHmmsszzz'");
        await AssertInvalidArgument(ParseExact(typeof(DateOnly), [new ArgumentOrCommandToken("2026-05-08")], "yyyyMMdd"), "2026-05-08", "DateOnly at exact format 'yyyyMMdd'");
        await AssertInvalidArgument(ParseExact(typeof(TimeOnly), [new ArgumentOrCommandToken("09:10:11")], "HHmmss"), "09:10:11", "TimeOnly at exact format 'HHmmss'");
    }

    [Test]
    public async Task CommonParser_Returns_Default_Value_When_No_Arguments_Are_Provided()
    {
        await AssertParsingFinished(ParseExact(typeof(string), []), string.Empty);
        await AssertParsingFinished(ParseExact(typeof(Guid), []), Guid.Empty);
        await AssertParsingFinished(ParseExact(typeof(DateTime), []), default(DateTime));
    }

    [Test]
    public async Task EnumParser_Parses_Named_Value_Case_Insensitively()
    {
        await AssertParsingFinished(ParseExtended(typeof(SampleMode), [new ArgumentOrCommandToken("advanced")]), SampleMode.Advanced);
    }

    [Test]
    public async Task EnumParser_Parses_Numeric_Underlying_Value()
    {
        await AssertParsingFinished(ParseExtended(typeof(SampleMode), [new ArgumentOrCommandToken("2")]), SampleMode.Advanced);
    }

    [Test]
    public async Task EnumParser_Parses_Flags_Combinations()
    {
        await AssertParsingFinished(ParseExtended(typeof(SamplePermissions), [new ArgumentOrCommandToken("Read, Write")]), SamplePermissions.Read | SamplePermissions.Write);
    }

    [Test]
    public async Task EnumParser_Returns_Default_Zero_Value_When_No_Arguments_Are_Provided()
    {
        await AssertParsingFinished(ParseExtended(typeof(SampleMode), []), (SampleMode)0);
    }

    [Test]
    public async Task EnumParser_Returns_InvalidArgument_For_Invalid_Enum_Input()
    {
        await AssertInvalidArgument(ParseExtended(typeof(SampleMode), [new ArgumentOrCommandToken("sideways")]), "sideways", "SampleMode enum");
    }

    private static ParsingResult ParseExact(Type targetType, ImmutableArray<Token> arguments, string? format = null)
    {
        return TypeProviderResolver.ParseBuiltInExact(
                   DefaultOptions,
                   arguments,
                   targetType,
                   format,
                   TypeProviderResolver.BuiltinTypeProviders)
               ?? throw new InvalidOperationException($"No built-in exact provider handled '{targetType.FullName}'.");
    }

    private static ParsingResult ParseExtended(Type targetType, ImmutableArray<Token> arguments, string? format = null)
    {
        return TypeProviderResolver.ParseBuiltInExtended(
                   DefaultOptions,
                   arguments,
                   targetType,
                   format,
                   TypeProviderResolver.BuiltinTypeProviders)
               ?? throw new InvalidOperationException($"No built-in extended provider handled '{targetType.FullName}'.");
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

        var actualValue = finished.UntypedResult ?? throw new InvalidOperationException("Expected parsed value to be non-null.");
        var expectedValue = expected ?? throw new InvalidOperationException("Expected comparison value to be non-null.");

        await Assert.That(actualValue.GetType()).IsEqualTo(expectedValue.GetType());
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

    private enum SampleMode
    {
        Basic = 1,
        Advanced = 2
    }

    [Flags]
    private enum SamplePermissions
    {
        None = 0,
        Read = 1,
        Write = 2,
        Execute = 4
    }
}
