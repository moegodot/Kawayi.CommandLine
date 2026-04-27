// Copyright (c) 2026 MoeGodot<me@kawayi.moe>.
// Licensed under the GNU Affero General Public License v3-or-later license.

using Kawayi.CommandLine.Abstractions;
using Kawayi.CommandLine.Core;

namespace Kawayi.CommandLine.Extensions;

public static class ParsingResultExtensions
{
    extension(ParsingResult result)
    {
        public T Expect<T>()
        {
            return result is ParsingFinished { UntypedResult: T v }
                ? v
                : throw new ArgumentException($"expect {typeof(T).FullName}, get {result}");
        }
    }
}
