// Copyright (c) 2026 MoeGodot<me@kawayi.moe>.
// Licensed under the GNU Affero General Public License v3-or-later license.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Kawayi.CommandLine.Abstractions;
using Kawayi.CommandLine.Core;

namespace Kawayi.CommandLine.Extensions;

/// <summary>
/// Adds convenience helpers for working with parsed command result trees.
/// </summary>
public static class CliExtensions
{
    extension(CliSchema schema)
    {
        /// <summary>
        /// Parses the supplied tokens by using the current schema snapshot.
        /// This will parse all <see cref="Subcommand"/>, if you want to get subcommand,
        /// use <see cref="CliSchemaParser.CreateParsing"/>.
        /// </summary>
        /// <param name="arguments">The tokens to parse.</param>
        /// <param name="options">The parsing options for this operation.</param>
        /// <returns>The terminal parsing result.</returns>
        public ParsingResult Parse(ImmutableArray<Token> arguments, ParsingOptions options)
        {
            ArgumentNullException.ThrowIfNull(options);

            var current = CliSchemaParser.CreateParsing(options, arguments, schema);

            while (current is Subcommand subcommand)
            {
                current = subcommand.ContinueParseAction();
            }

            return current;
        }
    }

    extension(Cli result)
    {
        /// <summary>
        /// Creates a new command object and populates it from the parsing result collection.
        /// </summary>
        /// <returns>The populated command object.</returns>
        public T Bind<
            [DynamicallyAccessedMembers(
                DynamicallyAccessedMemberTypes.Interfaces |
                DynamicallyAccessedMemberTypes.PublicProperties |
                DynamicallyAccessedMemberTypes.NonPublicProperties |
                DynamicallyAccessedMemberTypes.PublicConstructors)]
            T>()
            where T : new()
        {
            ArgumentNullException.ThrowIfNull(result);

            return Binder.Bind(new T(), result, new BindingOptions());
        }

        /// <summary>
        /// Gets the root command for a parsed command or subcommand.
        /// </summary>
        /// <returns>The <see cref="Cli"/> whose <see cref="Cli.ParentCommand"/> is <see langword="null"/>.</returns>
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
