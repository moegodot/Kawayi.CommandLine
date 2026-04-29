// Copyright (c) 2026 MoeGodot<me@kawayi.moe>.
// Licensed under the GNU Affero General Public License v3-or-later license.

using System.Collections.Immutable;
using Kawayi.CommandLine.Abstractions;

namespace Kawayi.CommandLine.Core;

/// <summary>
/// Tokenizes raw command-line arguments into typed token objects.
/// </summary>
public sealed class Tokenizer : ITokenizer
{
    /// <summary>
    /// Tokenizes the supplied command-line inputs.
    /// </summary>
    /// <param name="inputs">The raw command-line inputs.</param>
    /// <returns>The tokenized inputs.</returns>
    public ImmutableArray<Token> Tokenlize(ImmutableArray<string> inputs)
    {
        var builder = ImmutableArray.CreateBuilder<Token>(inputs.Length);

        for (var index = 0; index < inputs.Length; index++)
        {
            var input = inputs[index];

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
                builder.Add(new ShortOptionToken(input[1..]));
                continue;
            }

            builder.Add(new ArgumentOrCommandToken(input));
        }

        return builder.ToImmutable();
    }
}
