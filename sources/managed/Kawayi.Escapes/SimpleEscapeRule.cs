// Copyright (c) 2026 MoeGodot<me@kawayi.moe>.
// Licensed under the GNU Affero General Public License v3-or-later license.

using System.Collections.Immutable;
using System.Text;

namespace Kawayi.Escapes;

/// <summary>
/// Applies exact string replacements to escape and unescape text.
/// </summary>
/// <param name="RuleSet">The mapping from raw substrings to escaped substrings.</param>
public sealed record SimpleEscapeRule(ImmutableDictionary<string, string> RuleSet) : IEscapeRule
{
    private readonly PreparedRules preparedRules = PreparedRules.Create(RuleSet);

    /// <summary>
    /// Escapes the supplied text by applying the configured rule set.
    /// </summary>
    /// <param name="original">The raw text to escape.</param>
    /// <returns>The escaped text.</returns>
    public string Escape(string original)
    {
        ArgumentNullException.ThrowIfNull(original);

        if (original.Length == 0 || preparedRules.EscapeEntries.IsDefaultOrEmpty)
        {
            return original;
        }

        var builder = new StringBuilder(original.Length);

        for (var index = 0; index < original.Length;)
        {
            if (TryMatchEscape(original, index, out var entry))
            {
                builder.Append(entry.Escaped);
                index += entry.Original.Length;
                continue;
            }

            builder.Append(original[index]);
            index++;
        }

        return builder.ToString();
    }

    /// <summary>
    /// Unescapes the supplied text by reversing the configured rule set.
    /// </summary>
    /// <param name="escaped">The escaped text to unescape.</param>
    /// <returns>The unescaped text.</returns>
    public string Unescape(string escaped)
    {
        ArgumentNullException.ThrowIfNull(escaped);

        if (escaped.Length == 0 || preparedRules.UnescapeEntries.IsDefaultOrEmpty)
        {
            return escaped;
        }

        var builder = new StringBuilder(escaped.Length);

        for (var index = 0; index < escaped.Length;)
        {
            if (TryMatchUnescape(escaped, index, out var entry))
            {
                builder.Append(entry.Original);
                index += entry.Escaped.Length;
                continue;
            }

            builder.Append(escaped[index]);
            index++;
        }

        return builder.ToString();
    }

    private bool TryMatchEscape(string input, int index, out RuleEntry entry)
    {
        foreach (var rule in preparedRules.EscapeEntries)
        {
            if (input.AsSpan(index).StartsWith(rule.Original, StringComparison.Ordinal))
            {
                entry = rule;
                return true;
            }
        }

        entry = default;
        return false;
    }

    private bool TryMatchUnescape(string input, int index, out RuleEntry entry)
    {
        foreach (var rule in preparedRules.UnescapeEntries)
        {
            if (input.AsSpan(index).StartsWith(rule.Escaped, StringComparison.Ordinal))
            {
                entry = rule;
                return true;
            }
        }

        entry = default;
        return false;
    }

    private readonly record struct RuleEntry(string Original, string Escaped);

    private sealed record PreparedRules(
        ImmutableArray<RuleEntry> EscapeEntries,
        ImmutableArray<RuleEntry> UnescapeEntries)
    {
        public static PreparedRules Create(ImmutableDictionary<string, string> ruleSet)
        {
            ArgumentNullException.ThrowIfNull(ruleSet);

            var builder = ImmutableArray.CreateBuilder<RuleEntry>(ruleSet.Count);

            foreach (var (original, escaped) in ruleSet)
            {
                if (string.IsNullOrEmpty(original))
                {
                    throw new ArgumentException("Escape rule keys must not be empty.", nameof(ruleSet));
                }

                if (string.IsNullOrEmpty(escaped))
                {
                    throw new ArgumentException("Escape rule values must not be empty.", nameof(ruleSet));
                }

                builder.Add(new(original, escaped));
            }

            var entries = builder.ToImmutable();

            return new(
                entries
                    .OrderByDescending(static entry => entry.Original.Length)
                    .ThenBy(static entry => entry.Original, StringComparer.Ordinal)
                    .ThenBy(static entry => entry.Escaped, StringComparer.Ordinal)
                    .ToImmutableArray(),
                entries
                    .OrderByDescending(static entry => entry.Escaped.Length)
                    .ThenBy(static entry => entry.Escaped, StringComparer.Ordinal)
                    .ThenBy(static entry => entry.Original, StringComparer.Ordinal)
                    .ToImmutableArray());
        }
    }
}
