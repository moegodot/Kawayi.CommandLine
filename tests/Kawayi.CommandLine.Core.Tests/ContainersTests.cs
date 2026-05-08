// Copyright (c) 2026 MoeGodot<me@kawayi.moe>.
// Licensed under the GNU Affero General Public License v3-or-later license.

using System.Collections.Immutable;
using Kawayi.CommandLine.Abstractions;

namespace Kawayi.CommandLine.Core.Tests;

public sealed class ContainersTests
{
    private static readonly ParsingOptions DefaultOptions = new(
        new ProgramInformation("test", new("test", "test"), new Version(1, 0), "https://example.com"));

    [Test]
    public async Task ParseBuiltInExtended_Returns_Empty_Containers_When_No_Arguments_Are_Provided()
    {
        var arrayResult = ParseContainer(typeof(ImmutableArray<int>), []);
        var dictionaryResult = ParseContainer(typeof(ImmutableDictionary<string, int>), []);

        await AssertImmutableArray(arrayResult, []);
        await AssertImmutableDictionary(dictionaryResult, []);
    }

    [Test]
    public async Task ParseBuiltInExtended_Parses_ImmutableArray_Of_Int()
    {
        ImmutableArray<Token> arguments =
        [
            new ArgumentOrCommandToken("1"),
            new ArgumentOrCommandToken("2"),
            new ArgumentOrCommandToken("3")
        ];

        await AssertImmutableArray(ParseContainer(typeof(ImmutableArray<int>), arguments), [1, 2, 3]);
    }

    [Test]
    public async Task ParseBuiltInExtended_Parses_ImmutableList_Of_Bool()
    {
        ImmutableArray<Token> arguments =
        [
            new ArgumentOrCommandToken("true"),
            new ArgumentOrCommandToken("FALSE")
        ];

        var result = ParseContainer(typeof(ImmutableList<bool>), arguments);

        if (result is not ParsingFinished { UntypedResult: ImmutableList<bool> list })
        {
            throw new InvalidOperationException($"Expected {nameof(ParsingFinished)}, got {result.GetType().FullName}.");
        }

        await Assert.That(list.Count).IsEqualTo(2);
        await Assert.That(list[0]).IsTrue();
        await Assert.That(list[1]).IsFalse();
    }

    [Test]
    public async Task ParseBuiltInExtended_Parses_ImmutableHashSet_Of_Guid()
    {
        var first = Guid.Parse("2b5c66c5-f2a0-4720-a8f3-80abf153e8d3");
        var second = Guid.Parse("f4602e26-b7b4-4735-b931-d58d8f6f74c2");
        ImmutableArray<Token> arguments =
        [
            new ArgumentOrCommandToken(first.ToString()),
            new ArgumentOrCommandToken(second.ToString())
        ];

        var result = ParseContainer(typeof(ImmutableHashSet<Guid>), arguments);

        if (result is not ParsingFinished { UntypedResult: ImmutableHashSet<Guid> set })
        {
            throw new InvalidOperationException($"Expected {nameof(ParsingFinished)}, got {result.GetType().FullName}.");
        }

        await Assert.That(set.Count).IsEqualTo(2);
        await Assert.That(set.Contains(first)).IsTrue();
        await Assert.That(set.Contains(second)).IsTrue();
    }

    [Test]
    public async Task ParseBuiltInExtended_Parses_ImmutableArray_Of_Enum()
    {
        ImmutableArray<Token> arguments =
        [
            new ArgumentOrCommandToken("basic"),
            new ArgumentOrCommandToken("2")
        ];

        var result = ParseContainer(typeof(ImmutableArray<SampleMode>), arguments);

        if (result is not ParsingFinished { UntypedResult: ImmutableArray<SampleMode> values })
        {
            throw new InvalidOperationException($"Expected {nameof(ParsingFinished)}, got {result.GetType().FullName}.");
        }

        await Assert.That(values.Length).IsEqualTo(2);
        await Assert.That(values[0]).IsEqualTo(SampleMode.Basic);
        await Assert.That(values[1]).IsEqualTo(SampleMode.Advanced);
    }

    [Test]
    public async Task ParseBuiltInExtended_Parses_ImmutableDictionary_And_Uses_Last_Value_For_Duplicates()
    {
        ImmutableArray<Token> arguments =
        [
            new ArgumentOrCommandToken("answer=41"),
            new ArgumentOrCommandToken("answer=42")
        ];

        await AssertImmutableDictionary(ParseContainer(typeof(ImmutableDictionary<string, int>), arguments), [new KeyValuePair<string, int>("answer", 42)]);
    }

    [Test]
    public async Task ParseBuiltInExtended_Parses_ImmutableDictionary_With_Enum_Keys_And_Values()
    {
        ImmutableArray<Token> arguments =
        [
            new ArgumentOrCommandToken("basic=advanced"),
            new ArgumentOrCommandToken("advanced=basic")
        ];

        var result = ParseContainer(typeof(ImmutableDictionary<SampleMode, SampleMode>), arguments);

        if (result is not ParsingFinished { UntypedResult: ImmutableDictionary<SampleMode, SampleMode> dictionary })
        {
            throw new InvalidOperationException($"Expected {nameof(ParsingFinished)}, got {result.GetType().FullName}.");
        }

        await Assert.That(dictionary[SampleMode.Basic]).IsEqualTo(SampleMode.Advanced);
        await Assert.That(dictionary[SampleMode.Advanced]).IsEqualTo(SampleMode.Basic);
    }

    [Test]
    public async Task ParseBuiltInExtended_Parses_ImmutableSortedDictionary_With_Escaped_Equals_And_Value_Equals()
    {
        ImmutableArray<Token> arguments =
        [
            new ArgumentOrCommandToken(@"key\=part=value\=part"),
            new ArgumentOrCommandToken("plain=value=tail")
        ];

        var result = ParseContainer(typeof(ImmutableSortedDictionary<string, string>), arguments);

        if (result is not ParsingFinished { UntypedResult: ImmutableSortedDictionary<string, string> dictionary })
        {
            throw new InvalidOperationException($"Expected {nameof(ParsingFinished)}, got {result.GetType().FullName}.");
        }

        await Assert.That(dictionary["key=part"]).IsEqualTo("value=part");
        await Assert.That(dictionary["plain"]).IsEqualTo("value=tail");
    }

    [Test]
    public async Task ParseBuiltInExtended_Uses_Definition_Format_For_Temporal_Elements()
    {
        ImmutableArray<Token> arguments =
        [
            new ArgumentOrCommandToken("20260508"),
            new ArgumentOrCommandToken("20260509")
        ];

        var result = ParseContainer(typeof(ImmutableArray<DateOnly>), arguments, format: "yyyyMMdd");

        if (result is not ParsingFinished { UntypedResult: ImmutableArray<DateOnly> values })
        {
            throw new InvalidOperationException($"Expected {nameof(ParsingFinished)}, got {result.GetType().FullName}.");
        }

        await Assert.That(values).IsEquivalentTo([new DateOnly(2026, 5, 8), new DateOnly(2026, 5, 9)]);
    }

    [Test]
    public async Task ParseBuiltInExtended_Returns_InvalidArgument_For_Invalid_Element_Value()
    {
        ImmutableArray<Token> arguments = [new ArgumentOrCommandToken("not-an-int")];

        await AssertInvalidArgument(ParseContainer(typeof(ImmutableArray<int>), arguments), "not-an-int", "Int32 at NumberStyles.Integer");
    }

    [Test]
    public async Task ParseBuiltInExtended_Returns_InvalidArgument_For_Invalid_Enum_Element_Value()
    {
        ImmutableArray<Token> arguments = [new ArgumentOrCommandToken("sideways")];

        await AssertInvalidArgument(ParseContainer(typeof(ImmutableArray<SampleMode>), arguments), "sideways", "SampleMode enum");
    }

    [Test]
    public async Task ParseBuiltInExtended_Returns_InvalidArgument_For_Invalid_Enum_Dictionary_Key()
    {
        ImmutableArray<Token> arguments = [new ArgumentOrCommandToken("sideways=basic")];

        await AssertInvalidArgument(ParseContainer(typeof(ImmutableDictionary<SampleMode, SampleMode>), arguments), "sideways", "SampleMode enum");
    }

    [Test]
    public async Task ParseBuiltInExtended_Returns_InvalidArgument_For_Missing_Dictionary_Separator()
    {
        ImmutableArray<Token> arguments = [new ArgumentOrCommandToken(@"missing\=separator")];

        await AssertInvalidArgument(ParseContainer(typeof(ImmutableDictionary<string, string>), arguments), @"missing\=separator", "key=value");
    }

    [Test]
    public async Task ParseBuiltInExtended_Returns_GotError_For_Unsupported_Types()
    {
        ImmutableArray<Token> arguments = [new ArgumentOrCommandToken("1.0=value")];

        await AssertGotError<NotSupportedException>(ParseContainer(typeof(ImmutableDictionary<Version, string>), arguments));
    }

    [Test]
    public async Task ParseBuiltInExtended_Returns_GotError_For_NonComparable_Sorted_Values()
    {
        ImmutableArray<Token> arguments =
        [
            new ArgumentOrCommandToken("https://example.com/a"),
            new ArgumentOrCommandToken("https://example.com/b")
        ];

        await AssertGotError<Exception>(ParseContainer(typeof(ImmutableSortedSet<Uri>), arguments));
    }

    private static ParsingResult ParseContainer(Type targetType,
                                                ImmutableArray<Token> arguments,
                                                string? format = null,
                                                TypeProviders? customProviders = null)
    {
        var visibleProviders = TypeProviderResolver.CreateVisibleProviders(customProviders ?? TypeProviders.Empty);
        return TypeProviderResolver.ParseBuiltInExtended(DefaultOptions, arguments, targetType, format, visibleProviders)
               ?? throw new InvalidOperationException($"No built-in container provider handled '{targetType.FullName}'.");
    }

    private static async Task AssertImmutableArray(ParsingResult result, ImmutableArray<int> expected)
    {
        if (result is not ParsingFinished { UntypedResult: ImmutableArray<int> actual })
        {
            throw new InvalidOperationException($"Expected {nameof(ParsingFinished)}, got {result.GetType().FullName}.");
        }

        await Assert.That(actual.Length).IsEqualTo(expected.Length);

        for (var index = 0; index < actual.Length; index++)
        {
            await Assert.That(actual[index]).IsEqualTo(expected[index]);
        }
    }

    private static async Task AssertImmutableDictionary(ParsingResult result,
                                                         ImmutableArray<KeyValuePair<string, int>> expected)
    {
        if (result is not ParsingFinished { UntypedResult: ImmutableDictionary<string, int> actual })
        {
            throw new InvalidOperationException($"Expected {nameof(ParsingFinished)}, got {result.GetType().FullName}.");
        }

        await Assert.That(actual.Count).IsEqualTo(expected.Length);

        foreach (var (key, value) in expected)
        {
            await Assert.That(actual[key]).IsEqualTo(value);
        }
    }

    private static async Task AssertInvalidArgument(ParsingResult result, string argument, string expect)
    {
        if (result is not InvalidArgumentDetected invalid)
        {
            throw new InvalidOperationException($"Expected {nameof(InvalidArgumentDetected)}, got {result.GetType().FullName}.");
        }

        await Assert.That(invalid.Argument).EqualTo(argument);
        await Assert.That(invalid.Expect).EqualTo(expect);
    }

    private static async Task AssertGotError<TException>(ParsingResult result)
        where TException : Exception
    {
        if (result is not GotError { Exception: TException exception })
        {
            throw new InvalidOperationException($"Expected {nameof(GotError)} with {typeof(TException).FullName}, got {result.GetType().FullName}.");
        }

        await Assert.That(exception).IsNotNull();
    }

    private enum SampleMode
    {
        Basic = 1,
        Advanced = 2
    }
}
