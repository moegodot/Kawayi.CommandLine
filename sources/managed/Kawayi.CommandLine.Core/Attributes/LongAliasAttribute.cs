// Copyright (c) 2026 MoeGodot<me@kawayi.moe>.
// Licensed under the GNU Affero General Public License v3-or-later license.

namespace Kawayi.CommandLine.Core.Attributes;

/// <summary>
/// Declares a long-form alias for a property-backed option.
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = true)]
public sealed class LongAliasAttribute : AliasAttribute
{
    /// <summary>
    /// Initializes a new long-alias attribute.
    /// </summary>
    /// <param name="alias">The long alias text without the leading dashes.</param>
    /// <param name="visible">Whether the alias should be visible in help output.</param>
    public LongAliasAttribute(string alias, bool visible = true) : base(alias, visible)
    {
    }
}
