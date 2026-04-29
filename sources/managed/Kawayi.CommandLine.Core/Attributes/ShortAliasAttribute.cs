// Copyright (c) 2026 MoeGodot<me@kawayi.moe>.
// Licensed under the GNU Affero General Public License v3-or-later license.

namespace Kawayi.CommandLine.Core.Attributes;

/// <summary>
/// Declares a short-form alias for a property-backed option.
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = true)]
public sealed class ShortAliasAttribute : AliasAttribute
{
    /// <summary>
    /// Initializes a new short-alias attribute.
    /// </summary>
    /// <param name="alias">The short alias text without the leading dash.</param>
    /// <param name="visible">Whether the alias should be visible in help output.</param>
    public ShortAliasAttribute(string alias, bool visible = true) : base(alias, visible)
    {
    }
}
