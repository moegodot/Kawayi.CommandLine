// Copyright (c) 2026 MoeGodot<me@kawayi.moe>.
// Licensed under the GNU Affero General Public License v3-or-later license.

namespace Kawayi.CommandLine.Core.Attributes;

/// <summary>
/// Marks a property as a positional argument.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class ArgumentAttribute : SymbolAttribute
{
    /// <summary>
    /// Gets the zero-based argument position.
    /// </summary>
    public int Position { get; }

    /// <summary>
    /// Initializes a new argument attribute.
    /// </summary>
    /// <param name="position">The zero-based argument position.</param>
    /// <param name="require">Whether the argument is required.</param>
    /// <param name="visible">Whether the argument should be visible in help output.</param>
    public ArgumentAttribute(int position, bool require = false, bool visible = true) : base(require, visible)
    {
        Position = position;
    }
}
