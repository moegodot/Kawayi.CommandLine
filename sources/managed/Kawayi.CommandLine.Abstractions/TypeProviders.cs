// Copyright (c) 2026 GodotAsync<me@kawayi.moe>.
// Licensed under the GNU Affero General Public License v3-or-later license.

using System.Collections.Immutable;

namespace Kawayi.CommandLine.Abstractions;

/// <summary>
/// Represents the exact and extended runtime type providers available to a parsing operation.
/// </summary>
/// <param name="Providers">The exact providers keyed by the CLR target type.</param>
/// <param name="ExtendedProviders">The ordered runtime providers consulted when no exact provider applies.</param>
public readonly record struct TypeProviders(
    ImmutableDictionary<Type, ITypeProvider> Providers,
    ImmutableArray<IExtendedTypeProvider> ExtendedProviders)
{
    /// <summary>
    /// Gets an empty provider set with no custom exact or extended providers.
    /// </summary>
    public static TypeProviders Empty { get; } = new(
        ImmutableDictionary<Type, ITypeProvider>.Empty,
        ImmutableArray<IExtendedTypeProvider>.Empty);
}
