// Copyright (c) 2026 MoeGodot<me@kawayi.moe>.
// Licensed under the GNU Affero General Public License v3-or-later license.

using System.Collections.Immutable;
using Kawayi.CommandLine.Abstractions;

namespace Kawayi.CommandLine.Extensions;

/// <summary>
/// Provides convenience helpers for types that expose generated parsing entry points.
/// </summary>
public static class IParsableExtension
{
    extension<T, TU>(TU parsable)
        where T : Abstractions.IParsable<TU>
        where TU : new()
    {
        /// <summary>
        /// Creates a parsing result by using a new default instance as the initial state.
        /// </summary>
        /// <param name="options">The parsing options for this operation.</param>
        /// <param name="arguments">The tokens to parse.</param>
        /// <returns>The parsing result.</returns>
        public static ParsingResult CreateParsing(ParsingOptions options, ImmutableArray<Token> arguments)
        {
            return T.CreateParsing(options, arguments, new TU());
        }

        /// <summary>
        /// Creates a parsing result by using the current instance as the initial state.
        /// </summary>
        /// <param name="options">The parsing options for this operation.</param>
        /// <param name="arguments">The tokens to parse.</param>
        /// <returns>The parsing result.</returns>
        public ParsingResult Parse(ParsingOptions options, ImmutableArray<Token> arguments)
        {
            return T.CreateParsing(options, arguments, parsable);
        }
    }
}
