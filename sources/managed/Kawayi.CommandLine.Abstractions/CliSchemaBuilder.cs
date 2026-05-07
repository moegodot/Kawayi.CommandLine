// Copyright (c) 2026 GodotAsync<me@kawayi.moe>.
// Licensed under the GNU Affero General Public License v3-or-later license.

using System.Collections.Immutable;

namespace Kawayi.CommandLine.Abstractions;

/// <summary>
/// Provides a mutable schema builder used before parsing metadata is frozen.
/// </summary>
/// <param name="SubcommandDefinitions">the mutable subcommand definition registry.</param>
/// <param name="Subcommands">the mutable child parser registry keyed by subcommand name</param>
/// <param name="Properties">the mutable property definition registry</param>
/// <param name="Argument">the mutable ordered positional argument definitions</param>
public sealed record CliSchemaBuilder(
    ImmutableDictionary<string, CommandDefinition>.Builder SubcommandDefinitions,
    ImmutableDictionary<string, CliSchemaBuilder>.Builder Subcommands,
    ImmutableDictionary<string, PropertyDefinition>.Builder Properties,
    ImmutableList<ParameterDefinition>.Builder Argument)
{
    private bool _built = false;

    /// <summary>
    /// see <see cref="CliSchema.GeneratedFrom"/>
    /// </summary>
    public Type? GeneratedFrom { get; set; }


    /// <summary>
    /// Builds an immutable schema snapshot from the current builder state.
    /// </summary>
    /// <returns>The immutable schema snapshot.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the builder has already been built or when schema metadata is inconsistent.
    /// </exception>
    public CliSchema Build()
    {
        if (_built)
        {
            throw new InvalidOperationException("Schema builders can only be built once.");
        }

        _built = true;

        ValidateRegistryKeys(SubcommandDefinitions, nameof(SubcommandDefinitions));
        ValidateRegistryKeys(Properties, nameof(Properties));
        ValidateSubcommandBuilders();
        ValidateArgumentRanges();

        var subcommandDefinitions = ImmutableDictionary.CreateBuilder<ArgumentOrCommandToken, CommandDefinition>();
        foreach (var (_, definition) in SubcommandDefinitions)
        {
            AddUniqueDefinition(subcommandDefinitions, new ArgumentOrCommandToken(definition.Information.Name.Value), definition);

            foreach (var alias in definition.Alias.Values)
            {
                AddUniqueDefinition(subcommandDefinitions, new ArgumentOrCommandToken(alias.Value), definition);
            }
        }

        var subcommands = ImmutableDictionary.CreateBuilder<ArgumentOrCommandToken, CliSchema>();
        foreach (var (key, childBuilder) in Subcommands)
        {
            subcommands[new ArgumentOrCommandToken(key)] = childBuilder.Build();
        }

        var properties = ImmutableDictionary.CreateBuilder<OptionToken, PropertyDefinition>();
        foreach (var (_, definition) in Properties)
        {
            AddUniqueDefinition(properties, new LongOptionToken(definition.Information.Name.Value), definition);

            foreach (var alias in definition.LongName.Values)
            {
                AddUniqueDefinition(properties, new LongOptionToken(alias.Value), definition);
            }

            foreach (var alias in definition.ShortName.Values)
            {
                AddUniqueDefinition(properties, new ShortOptionToken(alias.Value), definition);
            }
        }

        return new CliSchema(
            GeneratedFrom,
            subcommandDefinitions.ToImmutable(),
            subcommands.ToImmutable(),
            properties.ToImmutable(),
            Argument.ToImmutable());
    }

    private static void ValidateRegistryKeys<TDefinition>(
        IEnumerable<KeyValuePair<string, TDefinition>> registry,
        string registryName)
        where TDefinition : Symbol
    {
        foreach (var (key, definition) in registry)
        {
            if (key != definition.Information.Name.Value)
            {
                throw new InvalidOperationException(
                    $"{registryName} key '{key}' must match definition name '{definition.Information.Name.Value}'.");
            }
        }
    }

    private void ValidateSubcommandBuilders()
    {
        foreach (var key in SubcommandDefinitions.Keys)
        {
            if (!Subcommands.ContainsKey(key))
            {
                throw new InvalidOperationException($"Subcommand '{key}' has a definition but no child schema builder.");
            }
        }

        foreach (var key in Subcommands.Keys)
        {
            if (!SubcommandDefinitions.ContainsKey(key))
            {
                throw new InvalidOperationException($"Subcommand '{key}' has a child schema builder but no definition.");
            }
        }
    }

    private void ValidateArgumentRanges()
    {
        long minimumCount = 0;

        foreach (var argument in Argument)
        {
            minimumCount = checked(minimumCount + argument.ValueRange.Minimum);

            if (minimumCount > int.MaxValue)
            {
                throw new InvalidOperationException("The positional argument minimum value count exceeds the supported command line length.");
            }
        }
    }

    private static void AddUniqueDefinition<TKey, TValue>(
        ImmutableDictionary<TKey, TValue>.Builder builder,
        TKey key,
        TValue value)
        where TKey : notnull
        where TValue : class
    {
        if (builder.TryGetValue(key, out var existingValue))
        {
            if (!ReferenceEquals(existingValue, value))
            {
                throw new InvalidOperationException($"Token '{key}' is mapped to more than one definition.");
            }

            return;
        }

        builder[key] = value;
    }
}
