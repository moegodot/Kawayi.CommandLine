// Copyright (c) 2026 MoeGodot<me@kawayi.moe>.
// Licensed under the GNU Affero General Public License v3-or-later license.

using System.Collections.Immutable;
using Kawayi.CommandLine.Abstractions;

namespace Kawayi.CommandLine.Core;

/// <summary>
/// Represents a single node in the parsed command result tree.
/// </summary>
public sealed class ParsingResultCollection : IParsingResultCollection
{
    private readonly ImmutableDictionary<TypedDefinition, object?> _values;
    private readonly Dictionary<CommandDefinition, IParsingResultCollection> _subcommands;

    /// <summary>
    /// Initializes a new tree node for a parsed command scope.
    /// </summary>
    public ParsingResultCollection(CommandDefinition? command,
                                   IParsingResultCollection? parent,
                                   ParsingScopeMetadata scope,
                                   ImmutableDictionary<TypedDefinition, object?>? values = null,
                                   ImmutableDictionary<CommandDefinition, IParsingResultCollection>? subcommands = null)
    {
        Command = command;
        Parent = parent;
        Scope = scope ?? throw new ArgumentNullException(nameof(scope));
        _values = values ?? ImmutableDictionary<TypedDefinition, object?>.Empty;
        _subcommands = subcommands?.ToDictionary(static pair => pair.Key, static pair => pair.Value)
            ?? [];
    }

    /// <inheritdoc />
    public CommandDefinition? Command { get; }

    /// <inheritdoc />
    public IParsingResultCollection? Parent { get; }

    /// <inheritdoc />
    public ParsingScopeMetadata Scope { get; }

    /// <inheritdoc />
    public bool TryGetValue(TypedDefinition definition, out object? value)
    {
        ArgumentNullException.ThrowIfNull(definition);
        return _values.TryGetValue(definition, out value);
    }

    /// <inheritdoc />
    public bool TryGetSubcommand(CommandDefinition definition, out IParsingResultCollection result)
    {
        ArgumentNullException.ThrowIfNull(definition);

        if (_subcommands.TryGetValue(definition, out result!))
        {
            return true;
        }

        result = null!;
        return false;
    }

    internal void SetDirectSubcommand(CommandDefinition definition, IParsingResultCollection result)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(result);
        _subcommands[definition] = result;
    }
}
