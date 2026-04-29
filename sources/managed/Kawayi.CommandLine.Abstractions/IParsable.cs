// Copyright (c) 2026 MoeGodot<me@kawayi.moe>.
// Licensed under the GNU Affero General Public License v3-or-later license.

using System.Collections.Immutable;

namespace Kawayi.CommandLine.Abstractions;

/// <summary>
/// Defines a type-specific parsing entry point.
/// </summary>
/// <typeparam name="T">The initial state type consumed by the parser.</typeparam>
public interface IParsable<T>
{
    /// <summary>
    /// Parses the supplied tokens and returns the parsing result.
    /// </summary>
    /// <param name="options">The parsing options for this operation.</param>
    /// <param name="arguments">The tokens to parse.</param>
    /// <param name="initialState">The initial state used by the parser.</param>
    /// <returns>The parsing result.</returns>
    static abstract ParsingResult CreateParsing(ParsingOptions options, ImmutableArray<Token> arguments, T initialState);
}
