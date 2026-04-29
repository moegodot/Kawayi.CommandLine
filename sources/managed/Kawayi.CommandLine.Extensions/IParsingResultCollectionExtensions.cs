// Copyright (c) 2026 MoeGodot<me@kawayi.moe>.
// Licensed under the GNU Affero General Public License v3-or-later license.

using Kawayi.CommandLine.Abstractions;

namespace Kawayi.CommandLine.Extensions;

public static class IParsingResultCollectionExtensions
{
    extension(IParsingResultCollection collection)
    {
        /// <summary>
        /// Creates a new bindable command object and populates it from the parsing result collection.
        /// </summary>
        public T Bind<T>()
            where T : IBindable, new()
        {
            ArgumentNullException.ThrowIfNull(collection);

            var result = new T();
            result.Bind(collection);
            return result;
        }
    }
}
