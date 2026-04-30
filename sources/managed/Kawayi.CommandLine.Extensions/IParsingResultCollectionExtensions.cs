// Copyright (c) 2026 MoeGodot<me@kawayi.moe>.
// Licensed under the GNU Affero General Public License v3-or-later license.

using Kawayi.CommandLine.Abstractions;

namespace Kawayi.CommandLine.Extensions;

/// <summary>
/// Adds convenience helpers for working with parsed command result trees.
/// </summary>
public static class IParsingResultCollectionExtensions
{
    extension(IParsingResultCollection collection)
    {
        /// <summary>
        /// Creates a new bindable command object and populates it from the parsing result collection.
        /// </summary>
        /// <returns>The populated command object.</returns>
        public T Bind<T>()
            where T : IBindable, new()
        {
            ArgumentNullException.ThrowIfNull(collection);

            var result = new T();
            result.Bind(collection);
            return result;
        }

        /// <summary>
        /// get the root command of a subcommand's <see cref="IParsingResultCollection"/>
        /// </summary>
        /// <returns>the <see cref="IParsingResultCollection"/> that <see cref="IParsingResultCollection.Parent"/> is <see langword="null"/></returns>
        public IParsingResultCollection GetRootCommand()
        {
            while (collection.Parent != null)
            {
                collection = collection.Parent;
            }

            return collection;
        }
    }
}
