// Copyright (c) 2026 MoeGodot<me@kawayi.moe>.
// Licensed under the GNU Affero General Public License v3-or-later license.

using System.Collections.Immutable;
using Kawayi.CommandLine.Abstractions;

namespace Kawayi.CommandLine.Core;

public sealed class ParsingResultCollection : IParsingResultCollection
{
    private readonly ImmutableDictionary<TypedDefinition, object?> _values;

    public ParsingResultCollection(ImmutableDictionary<string, CommandDefinition>? commands = null,
                                   ImmutableDictionary<TypedDefinition, object?>? values = null)
    {
        Commands = commands ?? ImmutableDictionary<string, CommandDefinition>.Empty;
        _values = values ?? ImmutableDictionary<TypedDefinition, object?>.Empty;
    }

    public ImmutableDictionary<string, CommandDefinition> Commands { get; }

    public object GetValue(TypedDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        if (_values.TryGetValue(definition, out var explicitValue))
        {
            return explicitValue!;
        }

        if (definition.DefaultValueFactory is not null)
        {
            return definition.DefaultValueFactory(this);
        }

        if (definition.Requirement)
        {
            throw new InvalidOperationException(
                $"Required definition '{definition.Information.Name.Value}' does not have an explicit value or default factory.");
        }

        return GetClrDefault(definition.Type)!;
    }

    private static object? GetClrDefault(Type type)
    {
        return type.IsValueType ? Activator.CreateInstance(type) : null;
    }
}
