// Copyright (c) 2026 MoeGodot<me@kawayi.moe>.
// Licensed under the GNU Affero General Public License v3-or-later license.

using System.Collections.Immutable;
using Kawayi.CommandLine.Abstractions;

namespace Kawayi.CommandLine.Core.Tests;

public class TokenizerTests
{
    [Test]
    public async Task NoToken_Tests()
    {
        ImmutableArray<string> input = [];

        var tokenizer = new Tokenizer();

        var result = tokenizer.Tokenlize(input);

        await Assert.That(result.Length).EqualTo(0);
    }

    [Test]
    public async Task SimpleTokenize_Tests()
    {
        ImmutableArray<string> input = ["target", "-s", "--long", "or command", "-s short", "-- long", "  "];

        var tokenizer = new Tokenizer();

        var result = tokenizer.Tokenlize(input);

        await Assert.That(result.Length).EqualTo(7);
        await Assert.That(result[0]).IsTypeOf<ArgumentOrCommandToken>().And.EqualTo(new("target"));
        await Assert.That(result[1]).IsTypeOf<ShortOptionToken>().And.EqualTo(new("s"));
        await Assert.That(result[2]).IsTypeOf<LongOptionToken>().And.EqualTo(new("long"));
        await Assert.That(result[3]).IsTypeOf<ArgumentOrCommandToken>().And.EqualTo(new("or command"));
        await Assert.That(result[4]).IsTypeOf<ShortOptionToken>().And.EqualTo(new("s short"));
        await Assert.That(result[5]).IsTypeOf<LongOptionToken>().And.EqualTo(new(" long"));
        await Assert.That(result[6]).IsTypeOf<ArgumentOrCommandToken>().And.EqualTo(new("  "));
    }

    [Test]
    public async Task PrefixOnlyTokenize_Tests()
    {
        ImmutableArray<string> input = ["-", "--"];

        var tokenizer = new Tokenizer();

        var result = tokenizer.Tokenlize(input);

        await Assert.That(result.Length).EqualTo(2);
        await Assert.That(result[0]).IsTypeOf<ShortOptionToken>().And.EqualTo(new(string.Empty));
        await Assert.That(result[1]).IsTypeOf<LongOptionToken>().And.EqualTo(new(string.Empty));
    }

    [Test]
    public async Task LongOptionWithInlineValue_Tokenize_Tests()
    {
        ImmutableArray<string> input = ["--format=json", "--env=a=b", "--empty="];

        var tokenizer = new Tokenizer();

        var result = tokenizer.Tokenlize(input);

        await Assert.That(result.Length).IsEqualTo(3);
        await Assert.That(result[0]).IsTypeOf<LongOptionToken>().And.EqualTo(new LongOptionToken("format", "json"));
        await Assert.That(result[1]).IsTypeOf<LongOptionToken>().And.EqualTo(new LongOptionToken("env", "a=b"));
        await Assert.That(result[2]).IsTypeOf<LongOptionToken>().And.EqualTo(new LongOptionToken("empty", string.Empty));
    }

    [Test]
    public async Task DashPrefixedOptionValues_Tokenize_As_OptionShaped_Tokens()
    {
        ImmutableArray<string> input = ["--count", "-1", "--linker-opts", "-L/bin/foo.a"];

        var tokenizer = new Tokenizer();

        var result = tokenizer.Tokenlize(input);

        await Assert.That(result.Length).IsEqualTo(4);
        await Assert.That(result[0]).IsTypeOf<LongOptionToken>().And.EqualTo(new LongOptionToken("count"));
        await Assert.That(result[1]).IsTypeOf<ShortOptionToken>().And.EqualTo(new ShortOptionToken("1"));
        await Assert.That(result[2]).IsTypeOf<LongOptionToken>().And.EqualTo(new LongOptionToken("linker-opts"));
        await Assert.That(result[3]).IsTypeOf<ShortOptionToken>().And.EqualTo(new ShortOptionToken("L/bin/foo.a"));
    }
}
