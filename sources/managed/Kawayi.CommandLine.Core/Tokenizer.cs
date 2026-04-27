// Copyright (c) 2026 MoeGodot<me@kawayi.moe>.
// Licensed under the GNU Affero General Public License v3-or-later license.

using System.Collections.Immutable;
using Kawayi.CommandLine.Abstractions;

namespace Kawayi.CommandLine.Core;

public sealed class Tokenizer : ITokenizer
{
    public ImmutableArray<Token> Tokenlize(ImmutableArray<string> inputs)
    {
        var builder = ImmutableArray.CreateBuilder<Token>(inputs.Length);

        for (var index = 0; index < inputs.Length; index++)
        {
            var input = inputs[index];

            if (input.StartsWith("--", StringComparison.Ordinal))
            {
                builder.Add(new LongOptionToken(input[2..]));
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
