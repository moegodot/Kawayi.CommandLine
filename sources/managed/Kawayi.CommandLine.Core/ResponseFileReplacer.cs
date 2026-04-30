// Copyright (c) 2026 MoeGodot<me@kawayi.moe>.
// Licensed under the GNU Affero General Public License v3-or-later license.

using System.Collections.Immutable;
using Kawayi.CommandLine.Abstractions;

namespace Kawayi.CommandLine.Core;

/// <summary>
/// Expands response-file references into the tokens contained in those files.
/// </summary>
public sealed class ResponseFileReplacer : IResponseFileReplacer
{
    /// <summary>
    /// A global response file replacer using <see cref="Tokenizer"/>.
    /// </summary>
    public static ResponseFileReplacer Instance { get; } = new(Tokenizer.Instance);

    private readonly ITokenizer tokenizer;

    /// <summary>
    /// Initializes a new response-file replacer.
    /// </summary>
    /// <param name="tokenizer">The tokenizer used for response-file contents.</param>
    public ResponseFileReplacer(ITokenizer tokenizer)
    {
        this.tokenizer = tokenizer;
    }

    /// <summary>
    /// Replaces response-file tokens with the tokens loaded from the referenced files.
    /// </summary>
    /// <param name="tokens">The tokens to inspect and expand.</param>
    /// <returns>The expanded token sequence.</returns>
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
