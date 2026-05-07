// Copyright (c) 2026 MoeGodot<me@kawayi.moe>.
// Licensed under the GNU Affero General Public License v3-or-later license.

using System.Collections.Immutable;

namespace Kawayi.CommandLine.Abstractions;

/// <summary>
/// Converts raw command-line strings into typed tokens.
/// </summary>
public interface ITokenizer
{
    /// <summary>
    /// Tokenizes the supplied raw command-line inputs.
    /// </summary>
    /// <param name="input">The raw command-line values.</param>
    /// <returns>The parsed token sequence.</returns>
    ImmutableArray<Token> Tokenize(ImmutableArray<string> input);
}
