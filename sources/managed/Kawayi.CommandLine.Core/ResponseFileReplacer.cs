// Copyright (c) 2026 MoeGodot<me@kawayi.moe>.
// Licensed under the GNU Affero General Public License v3-or-later license.

using System.Collections.Immutable;
using Kawayi.CommandLine.Abstractions;

namespace Kawayi.CommandLine.Core;

public sealed class ResponseFileReplacer : IResponseFileReplacer
{
    private readonly ITokenizer tokenizer;

    public ResponseFileReplacer(ITokenizer tokenizer)
    {
        this.tokenizer = tokenizer;
    }

    public ImmutableArray<Token> Replace(ImmutableArray<Token> tokens)
    {
        var builder = ImmutableArray.CreateBuilder<Token>();

        foreach (var token in tokens)
        {
            if (token is ArgumentOrCommandToken argumentOrCommandToken &&
                argumentOrCommandToken.Value.StartsWith('@'))
            {
                var lines = File.ReadAllLines(argumentOrCommandToken.Value[1..]);

                foreach (var line in lines)
                {
                    builder.AddRange(tokenizer.Tokenlize([line]));
                }

                continue;
            }

            builder.Add(token);
        }

        return builder.ToImmutable();
    }
}
