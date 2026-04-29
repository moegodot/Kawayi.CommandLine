// Copyright (c) 2026 MoeGodot<me@kawayi.moe>.
// Licensed under the GNU Affero General Public License v3-or-later license.

using System.Collections.Immutable;

namespace Kawayi.CommandLine.Core.Attributes;

/// <summary>
/// Provides shared metadata for attributes that declare command-line symbols.
/// </summary>
public abstract class SymbolAttribute : Attribute
{
    /// <summary>
    /// Initializes a new symbol attribute.
    /// </summary>
    /// <param name="require">Whether the symbol is required.</param>
    /// <param name="visible">Whether the symbol should be visible in help output.</param>
    public SymbolAttribute(bool require = false, bool visible = true)
    {
        Visible = visible;
        Require = require;
    }

    /// <summary>
    /// Gets whether the symbol should be visible in help output.
    /// </summary>
    public bool Visible { get; }

    /// <summary>
    /// Gets whether the symbol is required.
    /// </summary>
    public bool Require { get; }
}
