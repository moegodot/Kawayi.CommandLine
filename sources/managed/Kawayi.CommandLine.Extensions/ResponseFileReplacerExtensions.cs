// Copyright (c) 2026 GodotAsync<me@kawayi.moe>.
// Licensed under the GNU Affero General Public License v3-or-later license.

using System.Collections.Immutable;
using System.Diagnostics.Contracts;
using Kawayi.CommandLine.Abstractions;
using Kawayi.CommandLine.Core;

namespace Kawayi.CommandLine.Extensions;

/// <summary>
/// convenience helpers for <see cref="ResponseFileReplacer"/> and <see cref="IResponseFileReplacer"/>
/// </summary>
public static class ResponseFileReplacerExtensions
{
    extension(ImmutableArray<Token> tokens)
    {
        /// <summary>
        /// Replaces response-file tokens by using the default response-file replacer.
        /// </summary>
        /// <returns>The expanded token sequence.</returns>
        [Pure]
        public ImmutableArray<Token> UseResponseFile()
        {
            return ResponseFileReplacer.Instance.Replace(tokens);
        }
    }
}
