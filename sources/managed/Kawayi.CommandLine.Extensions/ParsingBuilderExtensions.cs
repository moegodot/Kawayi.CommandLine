// Copyright (c) 2026 GodotAsync<me@kawayi.moe>.
// Licensed under the GNU Affero General Public License v3-or-later license.

using System.Collections.Immutable;
using Kawayi.CommandLine.Abstractions;
using Kawayi.CommandLine.Core;

namespace Kawayi.CommandLine.Extensions;

/// <summary>
/// convenience helpers for <see cref="ParsingBuilder"/> and <see cref="IParsingBuilder"/>
/// </summary>
public static class ParsingBuilderExtensions
{
    extension(IParsingBuilder parsingBuilder)
    {
        /// <summary>
        /// merge two parsing builders
        /// </summary>
        /// <param name="other">the second parsing builder to merge</param>
        /// <param name="override">whether to override the premier with the second</param>
        /// <returns>the merged parsing builder</returns>
        /// <exception cref="ArgumentException">
        /// if the item exists premier or second parsing builder,
        /// but the item don't equal,and <paramref name="override"/> is false,
        /// then the exception will be thrown.
        /// </exception>
        public ParsingBuilder Merge(IParsingBuilder other, bool @override = true)
        {
            if (parsingBuilder.ParsingOptions != other.ParsingOptions && !@override)
            {
                throw new ArgumentException("parsing options should be same when override is disabled");
            }

            var options = @override ? other.ParsingOptions : parsingBuilder.ParsingOptions;
            var subcommandDefinitions = MergeDictionary(parsingBuilder.SubcommandDefinitions, other.SubcommandDefinitions, @override);
            var properties = MergeDictionary(parsingBuilder.Properties, other.Properties, @override);
            var argument = MergeArguments(parsingBuilder.Argument, other.Argument, @override);
            var subcommands = MergeSubcommandBuilders(parsingBuilder.Subcommands, other.Subcommands, @override);

            return new ParsingBuilder(options, subcommandDefinitions, properties, argument, subcommands);
        }

        private static ImmutableDictionary<string, T>? MergeDictionary<T>(
            ImmutableDictionary<string, T>.Builder first,
            ImmutableDictionary<string, T>.Builder second,
            bool @override)
        {
            if (first.Count == 0 && second.Count == 0)
                return null;

            var result = ImmutableDictionary.CreateBuilder<string, T>(StringComparer.Ordinal);

            foreach (var (key, value) in first)
                result[key] = value;

            foreach (var (key, secondValue) in second)
            {
                if (result.TryGetValue(key, out var firstValue))
                {
                    if (!EqualityComparer<T>.Default.Equals(firstValue, secondValue))
                    {
                        if (!@override)
                            throw new ArgumentException($"conflict in key '{key}': values differ and override is disabled");
                        result[key] = secondValue;
                    }
                }
                else
                {
                    result[key] = secondValue;
                }
            }

            return result.ToImmutable();
        }

        private static IList<ArgumentDefinition>? MergeArguments(
            ImmutableList<ArgumentDefinition>.Builder first,
            ImmutableList<ArgumentDefinition>.Builder second,
            bool @override)
        {
            if (first.Count == 0 && second.Count == 0)
                return null;

            if (first.Count == 0)
                return second.ToImmutable();

            if (second.Count == 0)
                return first.ToImmutable();

            if (first.SequenceEqual(second))
                return first.ToImmutable();

            if (!@override)
                throw new ArgumentException("arguments differ and override is disabled");

            return second.ToImmutable();
        }

        private static ImmutableDictionary<string, IParsingBuilder>? MergeSubcommandBuilders(
            ImmutableDictionary<string, IParsingBuilder>.Builder first,
            ImmutableDictionary<string, IParsingBuilder>.Builder second,
            bool @override)
        {
            if (first.Count == 0 && second.Count == 0)
                return null;

            var result = ImmutableDictionary.CreateBuilder<string, IParsingBuilder>(StringComparer.Ordinal);

            foreach (var (key, value) in first)
                result[key] = value;

            foreach (var (key, secondValue) in second)
            {
                if (result.TryGetValue(key, out var firstValue))
                {
                    result[key] = firstValue.Merge(secondValue, @override);
                }
                else
                {
                    result[key] = secondValue;
                }
            }

            return result.ToImmutable();
        }
    }
}
