// Copyright (c) 2026 MoeGodot<me@kawayi.moe>.
// Licensed under the GNU Affero General Public License v3-or-later license.

using System.Collections.Immutable;

namespace Kawayi.Escapes.Tests;

public sealed class SimpleEscapeRuleTests
{
    [Test]
    public async Task Escape_Returns_Original_String_When_No_Rules_Match()
    {
        var rule = CreateRule(("a", "x"));

        var result = rule.Escape("hello");

        await Assert.That(result).IsEqualTo("hello");
    }

    [Test]
    public async Task Escape_Replaces_Single_Token_Matches()
    {
        var rule = CreateRule(("\n", "\\n"), ("\t", "\\t"));

        var result = rule.Escape("line1\nline2\tend");

        await Assert.That(result).IsEqualTo("line1\\nline2\\tend");
    }

    [Test]
    public async Task Escape_Prefers_The_Longest_Source_Token()
    {
        var rule = CreateRule(("a", "x"), ("ab", "y"));

        var result = rule.Escape("abac");

        await Assert.That(result).IsEqualTo("yxc");
    }

    [Test]
    public async Task Escape_Does_Not_Double_Escape_Previously_Produced_Output()
    {
        var rule = CreateRule(("a", "ab"), ("ab", "x"));

        var result = rule.Escape("a");

        await Assert.That(result).IsEqualTo("ab");
    }

    [Test]
    public async Task Unescape_Returns_Original_String_When_No_Rules_Match()
    {
        var rule = CreateRule(("a", "x"));

        var result = rule.Unescape("hello");

        await Assert.That(result).IsEqualTo("hello");
    }

    [Test]
    public async Task Unescape_Reverses_Normal_Escaped_Sequences()
    {
        var rule = CreateRule(("\n", "\\n"), ("\t", "\\t"));

        var result = rule.Unescape("line1\\nline2\\tend");

        await Assert.That(result).IsEqualTo("line1\nline2\tend");
    }

    [Test]
    public async Task Unescape_Prefers_The_Longest_Escaped_Token()
    {
        var rule = CreateRule(("a", "x"), ("b", "xy"));

        var result = rule.Unescape("xyx");

        await Assert.That(result).IsEqualTo("ba");
    }

    [Test]
    public async Task Unescape_Uses_A_Deterministic_Winner_For_Duplicate_Escaped_Values()
    {
        var rule = CreateRule(("b", "x"), ("a", "x"));

        var result = rule.Unescape("x");

        await Assert.That(result).IsEqualTo("a");
    }

    [Test]
    public async Task Empty_Input_Returns_Empty_Output()
    {
        var rule = CreateRule(("a", "x"));

        await Assert.That(rule.Escape(string.Empty)).IsEqualTo(string.Empty);
        await Assert.That(rule.Unescape(string.Empty)).IsEqualTo(string.Empty);
    }

    [Test]
    public async Task Construction_Fails_For_An_Empty_Source_Key()
    {
        try
        {
            _ = CreateRule((string.Empty, "\\0"));
            throw new InvalidOperationException("Expected an ArgumentException to be thrown.");
        }
        catch (ArgumentException exception)
        {
            await Assert.That(exception.ParamName).IsEqualTo("ruleSet");
        }
    }

    [Test]
    public async Task Construction_Fails_For_An_Empty_Escaped_Value()
    {
        try
        {
            _ = CreateRule(("a", string.Empty));
            throw new InvalidOperationException("Expected an ArgumentException to be thrown.");
        }
        catch (ArgumentException exception)
        {
            await Assert.That(exception.ParamName).IsEqualTo("ruleSet");
        }
    }

    private static SimpleEscapeRule CreateRule(params (string Original, string Escaped)[] entries)
    {
        var builder = ImmutableDictionary.CreateBuilder<string, string>(StringComparer.Ordinal);

        foreach (var (original, escaped) in entries)
        {
            builder.Add(original, escaped);
        }

        return new(builder.ToImmutable());
    }
}
