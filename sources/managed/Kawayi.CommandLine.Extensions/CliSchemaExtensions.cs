// Copyright (c) 2026 GodotAsync<me@kawayi.moe>.
// Licensed under the GNU Affero General Public License v3-or-later license.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Kawayi.CommandLine.Abstractions;

namespace Kawayi.CommandLine.Extensions;

/// <summary>
/// Provides merge helpers for immutable CLI schema snapshots.
/// </summary>
public static class CliSchemaExtensions
{
    extension(CliSchema schema)
    {
        /// <summary>
        /// Attempts to merge two schema snapshots.
        /// </summary>
        /// <param name="another">The schema whose definitions are appended after the current schema.</param>
        /// <param name="allowOverride">Whether conflicting definitions from <paramref name="another"/> replace current definitions.</param>
        /// <param name="result">The merged schema when the merge succeeds.</param>
        /// <returns><see langword="true"/> when the schemas were merged; otherwise, <see langword="false"/>.</returns>
        public bool TryMerge(CliSchema another, bool allowOverride, [NotNullWhen(true)] out CliSchema? result)
        {
            if (TryMergeCore(schema, another, allowOverride, out var merged, out _))
            {
                result = merged;
                return true;
            }

            result = null;
            return false;
        }

        /// <summary>
        /// Merges two schema snapshots.
        /// </summary>
        /// <param name="another">The schema whose definitions are appended after the current schema.</param>
        /// <param name="allowOverride">Whether conflicting definitions from <paramref name="another"/> replace current definitions.</param>
        /// <returns>The merged schema.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the schemas conflict and overriding is disabled.</exception>
        public CliSchema Merge(CliSchema another, bool allowOverride)
        {
            if (TryMergeCore(schema, another, allowOverride, out var merged, out var reason))
            {
                return merged;
            }

            throw new InvalidOperationException(reason ?? "The CLI schemas could not be merged.");
        }
    }

    private static bool TryMergeCore(
        CliSchema current,
        CliSchema another,
        bool allowOverride,
        out CliSchema result,
        [NotNullWhen(false)] out string? reason)
    {
        if (!TryMergeArguments(current.Argument, another.Argument, allowOverride, out var arguments, out reason) ||
            !TryMergeDictionary(current.Properties, another.Properties, allowOverride, "property token", out var properties, out reason) ||
            !TryMergeDictionary(current.SubcommandDefinitions, another.SubcommandDefinitions, allowOverride, "subcommand token", out var subcommandDefinitions, out reason) ||
            !TryMergeDictionary(current.Subcommands, another.Subcommands, allowOverride, "subcommand schema", out var subcommands, out reason))
        {
            result = default;
            return false;
        }

        result = new CliSchema(
            another.GeneratedFrom ?? current.GeneratedFrom,
            subcommandDefinitions,
            subcommands,
            properties,
            arguments);
        reason = null;
        return true;
    }

    private static bool TryMergeArguments(
        ImmutableList<ParameterDefinition> current,
        ImmutableList<ParameterDefinition> another,
        bool allowOverride,
        out ImmutableList<ParameterDefinition> result,
        [NotNullWhen(false)] out string? reason)
    {
        var builder = current.ToBuilder();
        var indexes = new Dictionary<string, int>(StringComparer.Ordinal);

        for (var index = 0; index < builder.Count; index++)
        {
            indexes[builder[index].Information.Name.Value] = index;
        }

        foreach (var argument in another)
        {
            var name = argument.Information.Name.Value;
            if (indexes.TryGetValue(name, out var existingIndex))
            {
                if (!allowOverride)
                {
                    result = default!;
                    reason = $"Argument '{name}' is defined by more than one schema.";
                    return false;
                }

                builder[existingIndex] = argument;
                continue;
            }

            indexes[name] = builder.Count;
            builder.Add(argument);
        }

        result = builder.ToImmutable();
        reason = null;
        return true;
    }

    private static bool TryMergeDictionary<TKey, TValue>(
        ImmutableDictionary<TKey, TValue> current,
        ImmutableDictionary<TKey, TValue> another,
        bool allowOverride,
        string itemKind,
        out ImmutableDictionary<TKey, TValue> result,
        [NotNullWhen(false)] out string? reason)
        where TKey : notnull
    {
        var builder = current.ToBuilder();

        foreach (var (key, value) in another)
        {
            if (builder.ContainsKey(key) && !allowOverride)
            {
                result = default!;
                reason = $"The {itemKind} '{key}' is defined by more than one schema.";
                return false;
            }

            builder[key] = value;
        }

        result = builder.ToImmutable();
        reason = null;
        return true;
    }
}
