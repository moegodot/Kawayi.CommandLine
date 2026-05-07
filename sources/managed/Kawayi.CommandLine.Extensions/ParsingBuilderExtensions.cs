// Copyright (c) 2026 GodotAsync<me@kawayi.moe>.
// Licensed under the GNU Affero General Public License v3-or-later license.

using System.Collections;
using System.Collections.Immutable;
using Kawayi.CommandLine.Abstractions;
using Kawayi.CommandLine.Core;

namespace Kawayi.CommandLine.Extensions;

/// <summary>
/// convenience helpers for <see cref="ParsingBuilder"/> and <see cref="CliSchemaBuilder"/>
/// </summary>
public static class ParsingBuilderExtensions
{
    extension(CliSchemaBuilder cliSchemaBuilder)
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
        public ParsingBuilder Merge(CliSchemaBuilder other, bool @override = true)
        {
            if (cliSchemaBuilder.ParsingOptions != other.ParsingOptions && !@override)
            {
                throw new ArgumentException("parsing options should be same when override is disabled");
            }

            var options = @override ? other.ParsingOptions : cliSchemaBuilder.ParsingOptions;
            var subcommandDefinitions = MergeDictionary(cliSchemaBuilder.SubcommandDefinitions, other.SubcommandDefinitions, @override);
            var properties = MergeDictionary(cliSchemaBuilder.Properties, other.Properties, @override);
            var argument = MergeArguments(cliSchemaBuilder.Argument, other.Argument, @override);
            var subcommands = MergeSubcommandBuilders(cliSchemaBuilder.Subcommands, other.Subcommands, @override);

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
                    if (!AreEquivalent(firstValue, secondValue))
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

        private static IList<ParameterDefinition>? MergeArguments(
            ImmutableList<ParameterDefinition>.Builder first,
            ImmutableList<ParameterDefinition>.Builder second,
            bool @override)
        {
            if (first.Count == 0 && second.Count == 0)
                return null;

            if (first.Count == 0)
                return second.ToImmutable();

            if (second.Count == 0)
                return first.ToImmutable();

            if (AreArgumentListsEquivalent(first, second))
                return first.ToImmutable();

            if (!@override)
                throw new ArgumentException("arguments differ and override is disabled");

            return second.ToImmutable();
        }

        private static ImmutableDictionary<string, CliSchemaBuilder>? MergeSubcommandBuilders(
            ImmutableDictionary<string, CliSchemaBuilder>.Builder first,
            ImmutableDictionary<string, CliSchemaBuilder>.Builder second,
            bool @override)
        {
            if (first.Count == 0 && second.Count == 0)
                return null;

            var result = ImmutableDictionary.CreateBuilder<string, CliSchemaBuilder>(StringComparer.Ordinal);

            foreach (var (key, value) in first)
                result[key] = CloneBuilder(value);

            foreach (var (key, secondValue) in second)
            {
                if (result.TryGetValue(key, out var firstValue))
                {
                    result[key] = firstValue.Merge(secondValue, @override);
                }
                else
                {
                    result[key] = CloneBuilder(secondValue);
                }
            }

            return result.ToImmutable();
        }

        private static ParsingBuilder CloneBuilder(CliSchemaBuilder source)
        {
            var subcommands = ImmutableDictionary.CreateBuilder<string, CliSchemaBuilder>(StringComparer.Ordinal);

            foreach (var (key, childBuilder) in source.Subcommands)
                subcommands[key] = CloneBuilder(childBuilder);

            return new ParsingBuilder(
                source.ParsingOptions,
                source.SubcommandDefinitions.ToImmutable(),
                source.Properties.ToImmutable(),
                source.Argument.ToImmutable(),
                subcommands.ToImmutable());
        }

        private static bool AreEquivalent<T>(T first, T second)
        {
            return first switch
            {
                CommandDefinition firstCommand when second is CommandDefinition secondCommand =>
                    AreCommandsEquivalent(firstCommand, secondCommand),
                PropertyDefinition firstProperty when second is PropertyDefinition secondProperty =>
                    ArePropertiesEquivalent(firstProperty, secondProperty),
                ParameterDefinition firstArgument when second is ParameterDefinition secondArgument =>
                    AreArgumentsEquivalent(firstArgument, secondArgument),
                _ => EqualityComparer<T>.Default.Equals(first, second)
            };
        }

        private static bool AreArgumentListsEquivalent(
            ImmutableList<ParameterDefinition>.Builder first,
            ImmutableList<ParameterDefinition>.Builder second)
        {
            if (first.Count != second.Count)
                return false;

            for (var i = 0; i < first.Count; i++)
            {
                if (!AreArgumentsEquivalent(first[i], second[i]))
                    return false;
            }

            return true;
        }

        private static bool AreSymbolsEquivalent(Symbol? first, Symbol? second)
        {
            return first switch
            {
                null => second is null,
                CommandDefinition firstCommand when second is CommandDefinition secondCommand =>
                    AreCommandsEquivalent(firstCommand, secondCommand),
                PropertyDefinition firstProperty when second is PropertyDefinition secondProperty =>
                    ArePropertiesEquivalent(firstProperty, secondProperty),
                ParameterDefinition firstArgument when second is ParameterDefinition secondArgument =>
                    AreArgumentsEquivalent(firstArgument, secondArgument),
                _ => EqualityComparer<Symbol>.Default.Equals(first, second)
            };
        }

        private static bool AreCommandsEquivalent(CommandDefinition first, CommandDefinition second)
        {
            return first.Information == second.Information &&
                   AreDictionariesEquivalent(first.Alias, second.Alias) &&
                   AreNullableCommandsEquivalent(first.ParentCommand, second.ParentCommand);
        }

        private static bool AreNullableCommandsEquivalent(CommandDefinition? first, CommandDefinition? second)
        {
            return first is null || second is null
                ? first is null && second is null
                : AreCommandsEquivalent(first, second);
        }

        private static bool ArePropertiesEquivalent(PropertyDefinition first, PropertyDefinition second)
        {
            return first.Information == second.Information &&
                   AreSymbolsEquivalent(first.ParentSymbol, second.ParentSymbol) &&
                   first.Type == second.Type &&
                   first.Requirement == second.Requirement &&
                   first.RequirementIfNull == second.RequirementIfNull &&
                   first.DefaultValueFactory == second.DefaultValueFactory &&
                   first.Validation == second.Validation &&
                   AreDictionariesEquivalent(first.LongName, second.LongName) &&
                   AreDictionariesEquivalent(first.ShortName, second.ShortName) &&
                   first.NumArgs == second.NumArgs &&
                   first.ValueName == second.ValueName &&
                   ArePossibleValuesEquivalent(first.PossibleValues, second.PossibleValues);
        }

        private static bool AreArgumentsEquivalent(ParameterDefinition first, ParameterDefinition second)
        {
            return first.Information == second.Information &&
                   AreSymbolsEquivalent(first.ParentSymbol, second.ParentSymbol) &&
                   first.Type == second.Type &&
                   first.Requirement == second.Requirement &&
                   first.RequirementIfNull == second.RequirementIfNull &&
                   first.DefaultValueFactory == second.DefaultValueFactory &&
                   first.Validation == second.Validation &&
                   first.ValueRange == second.ValueRange;
        }

        private static bool AreDictionariesEquivalent<TValue>(
            ImmutableDictionary<string, TValue> first,
            ImmutableDictionary<string, TValue> second)
        {
            if (first.Count != second.Count)
                return false;

            foreach (var (key, firstValue) in first)
            {
                if (!second.TryGetValue(key, out var secondValue) ||
                    !EqualityComparer<TValue>.Default.Equals(firstValue, secondValue))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool ArePossibleValuesEquivalent(PossibleValues? first, PossibleValues? second)
        {
            if (ReferenceEquals(first, second))
                return true;

            if (first is null || second is null || first.GetType() != second.GetType())
                return false;

            return first switch
            {
                DescripablePossibleValues firstDescription when second is DescripablePossibleValues secondDescription =>
                    firstDescription.Description == secondDescription.Description,
                ICountablePossibleValues firstCountable when second is ICountablePossibleValues secondCountable =>
                    AreSequencesEquivalent(firstCountable.Candidates, secondCountable.Candidates),
                _ => EqualityComparer<PossibleValues>.Default.Equals(first, second)
            };
        }

        private static bool AreSequencesEquivalent(IEnumerable first, IEnumerable second)
        {
            return first.Cast<object?>().SequenceEqual(second.Cast<object?>());
        }
    }
}
