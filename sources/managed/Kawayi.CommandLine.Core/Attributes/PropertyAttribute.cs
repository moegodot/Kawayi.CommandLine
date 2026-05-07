// Copyright (c) 2026 MoeGodot<me@kawayi.moe>.
// Licensed under the GNU Affero General Public License v3-or-later license.

using System.Collections.Immutable;

namespace Kawayi.CommandLine.Core.Attributes;

/// <summary>
/// Marks a property as a named option or switch.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class PropertyAttribute : SymbolAttribute
{
    /// <summary>
    /// Initializes a new property attribute.
    /// </summary>
    /// <param name="require">Whether the property is required.</param>
    /// <param name="visible">Whether the property should be visible in help output.</param>
    /// <param name="valueName">The metavar name shown for option values.</param>
    /// <param name="requirementIfNull">Whether the property is required when its effective value is null.</param>
    public PropertyAttribute(bool require = false, bool visible = true, string? valueName = null, bool requirementIfNull = false) : base(require, visible, requirementIfNull)
    {
        ValueName = valueName;
    }

    /// <summary>
    /// Gets the metavar name shown for option values.
    /// </summary>
    public string? ValueName { get; }
}
