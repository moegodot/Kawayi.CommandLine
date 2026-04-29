// Copyright (c) 2026 MoeGodot<me@kawayi.moe>.
// Licensed under the GNU Affero General Public License v3-or-later license.

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
    Type ValueType);
