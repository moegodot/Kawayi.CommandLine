// Copyright (c) 2026 MoeGodot<me@kawayi.moe>.
// Licensed under the GNU Affero General Public License v3-or-later license.

using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Kawayi.CommandLine.Abstractions;
using Kawayi.CommandLine.Core;

namespace Kawayi.CommandLine.Extensions;

/// <summary>
/// Provides helper APIs for working with parsing results and typed parsing outputs.
/// </summary>
public static class ParsingResultExtensions
{
    extension(ParsingResult result)
    {
        /// <summary>
        /// Extracts a successful parsing result as the requested type.
        /// </summary>
        public T Expect<T>()
        {
            return result is ParsingFinished { UntypedResult: T v }
                ? v
                : throw new ArgumentException($"expect {typeof(T).FullName}, get {result}");
        }

        /// <summary>
        /// parse <see cref="Subcommand"/> until it return other type's result.
        /// </summary>
        /// <returns>the parsing result, guarantee it's not <see cref="Subcommand"/></returns>
        public ParsingResult ParseRecursively()
        {
            while (result is Subcommand subcommand)
            {
                result = subcommand.ContinueParseAction();
            }

            return result;
        }
    }
}
