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
    public async Task CreateParsing_Returns_Empty_Containers_When_No_Arguments_Are_Provided()
    {
        var arrayResult = Containers.CreateParsing(
            DefaultOptions,
            [],
            new ContainerType(typeof(ImmutableArray<int>), null, typeof(int)));
        var dictionaryResult = Containers.CreateParsing(
            DefaultOptions,
            [],
            new ContainerType(typeof(ImmutableDictionary<string, int>), typeof(string), typeof(int)));

        await AssertImmutableArray(arrayResult, []);
        await AssertImmutableDictionary(dictionaryResult, []);
    }

    [Test]
    public async Task CreateParsing_Parses_ImmutableArray_Of_Int()
    {
        ImmutableArray<Token> arguments =
        [
            new ArgumentOrCommandToken("1"),
            new ArgumentOrCommandToken("2"),
            new ArgumentOrCommandToken("3")
        ];

        var result = Containers.CreateParsing(
            DefaultOptions,
            arguments,
            new ContainerType(typeof(ImmutableArray<int>), null, typeof(int)));

        await AssertImmutableArray(result, [1, 2, 3]);
    }

    [Test]
    public async Task CreateParsing_Parses_ImmutableList_Of_Bool()
    {
        ImmutableArray<Token> arguments =
        [
            new ArgumentOrCommandToken("true"),
            new ArgumentOrCommandToken("FALSE")
        ];

        var result = Containers.CreateParsing(
            DefaultOptions,
            arguments,
            new ContainerType(typeof(ImmutableList<bool>), null, typeof(bool)));

        if (result is not ParsingFinished { UntypedResult: ImmutableList<bool> list })
        {
            throw new InvalidOperationException($"Expected {nameof(ParsingFinished)}, got {result.GetType().FullName}.");
        }

        await Assert.That(list.Count).IsEqualTo(2);
        await Assert.That(list[0]).IsTrue();
        await Assert.That(list[1]).IsFalse();
    }

    [Test]
    public async Task CreateParsing_Parses_ImmutableHashSet_Of_Guid()
    {
        var first = Guid.Parse("2b5c66c5-f2a0-4720-a8f3-80abf153e8d3");
        var second = Guid.Parse("f4602e26-b7b4-4735-b931-d58d8f6f74c2");
        ImmutableArray<Token> arguments =
        [
            new ArgumentOrCommandToken(first.ToString()),
            new ArgumentOrCommandToken(second.ToString())
        ];

        var result = Containers.CreateParsing(
            DefaultOptions,
            arguments,
            new ContainerType(typeof(ImmutableHashSet<Guid>), null, typeof(Guid)));

        if (result is not ParsingFinished { UntypedResult: ImmutableHashSet<Guid> set })
        {
            throw new InvalidOperationException($"Expected {nameof(ParsingFinished)}, got {result.GetType().FullName}.");
        }

        await Assert.That(set.Count).IsEqualTo(2);
        await Assert.That(set.Contains(first)).IsTrue();
        await Assert.That(set.Contains(second)).IsTrue();
    }

    [Test]
    public async Task CreateParsing_Parses_ImmutableDictionary_And_Uses_Last_Value_For_Duplicates()
    {
        ImmutableArray<Token> arguments =
        [
            new ArgumentOrCommandToken("answer=41"),
            new ArgumentOrCommandToken("answer=42")
        ];

        var result = Containers.CreateParsing(
            DefaultOptions,
            arguments,
            new ContainerType(typeof(ImmutableDictionary<string, int>), typeof(string), typeof(int)));

        await AssertImmutableDictionary(result, [new KeyValuePair<string, int>("answer", 42)]);
    }

    [Test]
    public async Task CreateParsing_Parses_ImmutableSortedDictionary_With_Escaped_Equals_And_Value_Equals()
    {
        ImmutableArray<Token> arguments =
        [
            new ArgumentOrCommandToken(@"key\=part=value\=part"),
            new ArgumentOrCommandToken("plain=value=tail")
        ];

        var result = Containers.CreateParsing(
            DefaultOptions,
            arguments,
            new ContainerType(typeof(ImmutableSortedDictionary<string, string>), typeof(string), typeof(string)));

        if (result is not ParsingFinished { UntypedResult: ImmutableSortedDictionary<string, string> dictionary })
        {
            throw new InvalidOperationException($"Expected {nameof(ParsingFinished)}, got {result.GetType().FullName}.");
        }

        await Assert.That(dictionary["key=part"]).IsEqualTo("value=part");
        await Assert.That(dictionary["plain"]).IsEqualTo("value=tail");
    }

    [Test]
    public async Task CreateParsing_Returns_InvalidArgument_For_Invalid_Element_Value()
    {
        ImmutableArray<Token> arguments = [new ArgumentOrCommandToken("not-an-int")];

        var result = Containers.CreateParsing(
            DefaultOptions,
            arguments,
            new ContainerType(typeof(ImmutableArray<int>), null, typeof(int)));

        await AssertInvalidArgument(result, "not-an-int", "int at NumberStyles.Integer");
    }

    [Test]
    public async Task CreateParsing_Returns_InvalidArgument_For_Missing_Dictionary_Separator()
    {
        ImmutableArray<Token> arguments = [new ArgumentOrCommandToken(@"missing\=separator")];

        var result = Containers.CreateParsing(
            DefaultOptions,
            arguments,
            new ContainerType(typeof(ImmutableDictionary<string, string>), typeof(string), typeof(string)));

        await AssertInvalidArgument(result, @"missing\=separator", "key=value");
    }

    [Test]
    public async Task CreateParsing_Returns_GotError_For_Unsupported_Types()
    {
        ImmutableArray<Token> arguments = [new ArgumentOrCommandToken("1.0=value")];

        var result = Containers.CreateParsing(
            DefaultOptions,
            arguments,
            new ContainerType(typeof(ImmutableDictionary<Version, string>), typeof(Version), typeof(string)));

        await AssertGotError<NotSupportedException>(result);
    }

    [Test]
    public async Task CreateParsing_Returns_GotError_For_NonComparable_Sorted_Values()
    {
        ImmutableArray<Token> arguments =
        [
            new ArgumentOrCommandToken("https://example.com/a"),
            new ArgumentOrCommandToken("https://example.com/b")
        ];

        var result = Containers.CreateParsing(
            DefaultOptions,
            arguments,
            new ContainerType(typeof(ImmutableSortedSet<Uri>), null, typeof(Uri)));

        await AssertGotError<Exception>(result);
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
}
