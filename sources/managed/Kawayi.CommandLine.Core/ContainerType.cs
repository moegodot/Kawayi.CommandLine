// Copyright (c) 2026 MoeGodot<me@kawayi.moe>.
// Licensed under the GNU Affero General Public License v3-or-later license.

using System.Collections.Immutable;

namespace Kawayi.CommandLine.Core;

/// <summary>
/// Describes a supported collection or dictionary container target.
/// </summary>
/// <param name="Container">The concrete container type.</param>
/// <param name="KeyType">The key type for dictionary-like containers, or null for sequence containers.</param>
/// <param name="ValueType">The element or value type stored by the container.</param>
public sealed record ContainerType(
    Type Container,
    Type? KeyType,
    Type ValueType)
{
    /// <summary>
    /// Attempts to create a supported immutable container descriptor for the specified runtime type.
    /// </summary>
    /// <param name="targetType">The runtime type to inspect.</param>
    /// <param name="containerType">The created container descriptor when successful.</param>
    /// <returns><see langword="true"/> when the target type is a supported container.</returns>
    public static bool TryCreate(Type targetType, out ContainerType containerType)
    {
        ArgumentNullException.ThrowIfNull(targetType);

        containerType = null!;

        if (!targetType.IsConstructedGenericType)
        {
            return false;
        }

        var genericDefinition = targetType.GetGenericTypeDefinition();
        var genericArguments = targetType.GetGenericArguments();

        if (genericDefinition == typeof(ImmutableDictionary<,>) || genericDefinition == typeof(ImmutableSortedDictionary<,>))
        {
            containerType = new ContainerType(targetType, genericArguments[0], genericArguments[1]);
            return true;
        }

        if (genericDefinition == typeof(ImmutableArray<>)
            || genericDefinition == typeof(ImmutableList<>)
            || genericDefinition == typeof(ImmutableQueue<>)
            || genericDefinition == typeof(ImmutableStack<>)
            || genericDefinition == typeof(ImmutableSortedSet<>)
            || genericDefinition == typeof(ImmutableHashSet<>))
        {
            containerType = new ContainerType(targetType, null, genericArguments[0]);
            return true;
        }

        return false;
    }
}
