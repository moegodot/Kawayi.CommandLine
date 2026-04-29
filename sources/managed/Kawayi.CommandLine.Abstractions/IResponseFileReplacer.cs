// Copyright (c) 2026 MoeGodot<me@kawayi.moe>.
// Licensed under the GNU Affero General Public License v3-or-later license.

using System.Collections.Immutable;

namespace Kawayi.CommandLine.Abstractions;

/// <summary>
/// Replaces response-file references with the tokens contained in those files.
/// </summary>
public interface IResponseFileReplacer
{
    /// <summary>
    /// Expands response-file tokens inside the supplied token sequence.
    /// </summary>
    /// <param name="tokens">The tokens to inspect and expand.</param>
    /// <returns>The expanded token sequence.</returns>
    ImmutableArray<Token> Replace(ImmutableArray<Token> tokens);
}
