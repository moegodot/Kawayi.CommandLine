// Copyright (c) 2026 MoeGodot<me@kawayi.moe>.
// Licensed under the GNU Affero General Public License v3-or-later license.

using System.Collections.Immutable;
using Kawayi.CommandLine.Abstractions;

namespace Kawayi.CommandLine.Core.Tests;

public class ResponseFileReplacerTests
{
    [Test]
    public async Task Replace_NoResponseFileTokens_ReturnsOriginalTokens()
    {
        ImmutableArray<Token> input =
        [
            new ArgumentOrCommandToken("target"),
            new ShortOptionToken("short"),
            new LongOptionToken("long")
        ];

        var replacer = new ResponseFileReplacer(new Tokenizer());

        var result = replacer.Replace(input);

        await AssertTokenSequence(result, input);
    }

    [Test]
    public async Task Replace_ArgumentResponseFileToken_ExpandsInPlace()
    {
        var path = Path.Combine(Path.GetTempPath(), $"{nameof(ResponseFileReplacerTests)}-{Guid.NewGuid():N}.txt");

        try
        {
            File.WriteAllLines(path, ["plain", "-x", "--name"]);

            ImmutableArray<Token> input =
            [
                new ArgumentOrCommandToken("before"),
                new ArgumentOrCommandToken($"@{path}"),
                new ShortOptionToken("after")
            ];

            var replacer = new ResponseFileReplacer(new Tokenizer());

            var result = replacer.Replace(input);

            await Assert.That(result.Length).EqualTo(5);
            await Assert.That(result[0]).IsTypeOf<ArgumentOrCommandToken>().And.EqualTo(new ArgumentOrCommandToken("before"));
            await Assert.That(result[1]).IsTypeOf<ArgumentOrCommandToken>().And.EqualTo(new ArgumentOrCommandToken("plain"));
            await Assert.That(result[2]).IsTypeOf<ShortOptionToken>().And.EqualTo(new ShortOptionToken("x"));
            await Assert.That(result[3]).IsTypeOf<LongOptionToken>().And.EqualTo(new LongOptionToken("name"));
            await Assert.That(result[4]).IsTypeOf<ShortOptionToken>().And.EqualTo(new ShortOptionToken("after"));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public async Task Replace_NonArgumentAtTokens_AreNotExpanded()
    {
        var path = Path.Combine(Path.GetTempPath(), $"{nameof(ResponseFileReplacerTests)}-{Guid.NewGuid():N}.txt");

        try
        {
            File.WriteAllLines(path, ["plain"]);

            ImmutableArray<Token> input =
            [
                new ShortOptionToken($"@{path}"),
                new LongOptionToken($"@{path}")
            ];

            var replacer = new ResponseFileReplacer(new Tokenizer());

            var result = replacer.Replace(input);

            await AssertTokenSequence(result, input);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public async Task Replace_ArgumentTokens_AfterOptionTerminator_AreNotExpanded()
    {
        var path = Path.Combine(Path.GetTempPath(), $"{nameof(ResponseFileReplacerTests)}-{Guid.NewGuid():N}.txt");

        try
        {
            File.WriteAllLines(path, ["--secret-token", "hush"]);

            ImmutableArray<Token> input =
            [
                new OptionTerminatorToken(),
                new ArgumentToken($"@{path}")
            ];

            var replacer = new ResponseFileReplacer(new Tokenizer());

            var result = replacer.Replace(input);

            await AssertTokenSequence(result, input);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public async Task Replace_MissingResponseFile_ThrowsFileNotFoundException()
    {
        var path = Path.Combine(Path.GetTempPath(), $"{nameof(ResponseFileReplacerTests)}-{Guid.NewGuid():N}.txt");
        ImmutableArray<Token> input = [new ArgumentOrCommandToken($"@{path}")];

        var replacer = new ResponseFileReplacer(new Tokenizer());

        try
        {
            _ = replacer.Replace(input);
            throw new InvalidOperationException("Expected a FileNotFoundException to be thrown.");
        }
        catch (FileNotFoundException exception)
        {
            await Assert.That(exception.FileName).EqualTo(path);
        }
    }

    private static async Task AssertTokenSequence(ImmutableArray<Token> actual, ImmutableArray<Token> expected)
    {
        await Assert.That(actual.Length).EqualTo(expected.Length);

        for (var index = 0; index < actual.Length; index++)
        {
            await Assert.That(actual[index]).EqualTo(expected[index]);
        }
    }
}
