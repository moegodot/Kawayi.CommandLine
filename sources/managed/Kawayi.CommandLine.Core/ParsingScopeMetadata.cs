// Copyright (c) 2026 MoeGodot<me@kawayi.moe>.
// Licensed under the GNU Affero General Public License v3-or-later license.

using System.Collections.Immutable;
using Kawayi.CommandLine.Abstractions;

namespace Kawayi.CommandLine.Core;

/// <summary>
/// Provides schema metadata for a single parsing scope.
/// </summary>
public sealed class ParsingScopeMetadata : IParsingScopeMetadata
{
    /// <summary>
    /// Initializes a new metadata snapshot for a parsing scope.
    /// </summary>
    public ParsingScopeMetadata(ImmutableArray<CommandDefinition> availableSubcommands,
                                ImmutableArray<TypedDefinition> availableTypedDefinitions)
    {
        AvailableSubcommands = availableSubcommands;
        AvailableTypedDefinitions = availableTypedDefinitions;
    }

    /// <inheritdoc />
    public ImmutableArray<CommandDefinition> AvailableSubcommands { get; }

    /// <inheritdoc />
    public ImmutableArray<TypedDefinition> AvailableTypedDefinitions { get; }
}
