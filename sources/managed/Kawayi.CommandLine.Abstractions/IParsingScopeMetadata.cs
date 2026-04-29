// Copyright (c) 2026 MoeGodot<me@kawayi.moe>.
// Licensed under the GNU Affero General Public License v3-or-later license.

using System.Collections.Immutable;

namespace Kawayi.CommandLine.Abstractions;

/// <summary>
/// Describes the available schema surface for a single parsing scope.
/// </summary>
public interface IParsingScopeMetadata
{
    /// <summary>
    /// Gets the subcommands that can be selected directly from the current scope.
    /// </summary>
    ImmutableArray<CommandDefinition> AvailableSubcommands { get; }

    /// <summary>
    /// Gets the typed definitions declared directly on the current scope.
    /// </summary>
    ImmutableArray<TypedDefinition> AvailableTypedDefinitions { get; }
}
