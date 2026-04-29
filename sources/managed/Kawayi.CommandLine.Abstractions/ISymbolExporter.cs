// Copyright (c) 2026 MoeGodot<me@kawayi.moe>.
// Licensed under the GNU Affero General Public License v3-or-later license.

using System.Collections.Immutable;

namespace Kawayi.CommandLine.Abstractions;

/// <summary>
/// Exposes generated symbol definitions for a command type.
/// </summary>
public interface ISymbolExporter
{
    /// <summary>
    /// Gets the exported symbols declared by the command type.
    /// </summary>
    static abstract ImmutableArray<Symbol> Symbols { get; }
}
