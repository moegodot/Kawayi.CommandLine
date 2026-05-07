// Copyright (c) 2026 MoeGodot<me@kawayi.moe>.
// Licensed under the GNU Affero General Public License v3-or-later license.

using System.Diagnostics.CodeAnalysis;
using Kawayi.CommandLine.Abstractions;
using Kawayi.CommandLine.Core;

namespace Kawayi.CommandLine.Extensions;

/// <summary>
/// Adds convenience helpers for working with parsed command result trees.
/// </summary>
public static class CliExtensions
{
    extension(Cli result)
    {
        /// <summary>
        /// Creates a new bindable command object and populates it from the parsing result collection.
        /// </summary>
        /// <returns>The populated command object.</returns>
        public T Bind<T>()
            where T : IBindable, new()
        {
            ArgumentNullException.ThrowIfNull(result);

            var obj = new T();
            obj.Bind(result);
            return obj;
        }

        /// <summary>
        /// get the root command of a subcommand's <see cref="Cli"/>
        /// </summary>
        /// <returns>the <see cref="Cli"/> that <see cref="Cli.Parent"/> is <see langword="null"/></returns>
        public Cli GetRootCommand()
        {
            while (result.ParentCommand != null)
            {
                result = result.ParentCommand;
            }

            return result;
        }
    }
}
