// Copyright (c) 2026 MoeGodot<me@kawayi.moe>.
// Licensed under the GNU Affero General Public License v3-or-later license.

using System.Collections.Immutable;
using Kawayi.CommandLine.Abstractions;
using Kawayi.Escapes;

namespace Kawayi.CommandLine.Core;

/// <summary>
/// Tokenizes raw command-line arguments into typed token objects.
/// </summary>
public sealed class Tokenizer : ITokenizer
{
    /// <summary>
    /// Gets the default escape rule used to force dash-prefixed inputs to parse as arguments.
    /// </summary>
    public static IEscapeRule DefaultArgumentEscapeRule { get; } = new SimpleEscapeRule(
        ImmutableDictionary.CreateRange(StringComparer.Ordinal, [new KeyValuePair<string, string>("-", @"\-")]));

    /// <summary>
    /// A global tokenizer
    /// </summary>
    public static Tokenizer Instance { get; } = new();

    /// <summary>
    /// Tokenizes the supplied command-line inputs.
    /// </summary>
    /// <param name="inputs">The raw command-line inputs.</param>
    /// <returns>The tokenized inputs.</returns>
    public ImmutableArray<Token> Tokenize(ImmutableArray<string> inputs) => TokenizeCore(inputs);

    private static ImmutableArray<Token> TokenizeCore(ImmutableArray<string> inputs)
    {
        var builder = ImmutableArray.CreateBuilder<Token>(inputs.Length);
        var forceProgramArgument = false;

        for (var index = 0; index < inputs.Length; index++)
        {
            var input = inputs[index];

            if (forceProgramArgument)
            {
                builder.Add(new ArgumentToken(input));
                continue;
            }

            if (input.StartsWith(@"\-", StringComparison.Ordinal))
            {
                builder.Add(new ArgumentToken(DefaultArgumentEscapeRule.Unescape(input)));
                continue;
            }

            if (string.Equals(input, "--", StringComparison.Ordinal))
            {
                builder.Add(new OptionTerminatorToken());
                forceProgramArgument = true;
                continue;
            }

            if (input.StartsWith("--", StringComparison.Ordinal))
            {
                var optionText = input[2..];
                var separatorIndex = optionText.IndexOf('=');

                if (separatorIndex >= 0)
                {
                    builder.Add(new LongOptionToken(optionText[..separatorIndex], optionText[(separatorIndex + 1)..]));
                    continue;
                }

                builder.Add(new LongOptionToken(optionText));
                continue;
            }

            if (input.StartsWith("-", StringComparison.Ordinal))
            {
                var optionText = input[1..];
                var optionName = optionText.Length == 0 ? string.Empty : optionText[..1];
                var inlineValue = optionText.Length > 1 ? optionText[1..] : null;
                builder.Add(new ShortOptionToken(optionName, inlineValue));
                continue;
            }

            builder.Add(new ArgumentOrCommandToken(input));
        }

        return builder.ToImmutable();
    }
}
