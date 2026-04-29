// Copyright (c) 2026 MoeGodot<me@kawayi.moe>.
// Licensed under the GNU Affero General Public License v3-or-later license.

namespace Kawayi.CommandLine.Core.Attributes;

/// <summary>
/// Declares an alternate name for a command-line member.
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = true)]
public class AliasAttribute : Attribute
{
    /// <summary>
    /// Initializes a new alias attribute.
    /// </summary>
    /// <param name="alias">The alias text.</param>
    /// <param name="visible">Whether the alias should be visible in help output.</param>
    public AliasAttribute(string alias, bool visible = true)
    {
        Alias = alias;
        Visible = visible;
    }

    /// <summary>
    /// Gets the alias text.
    /// </summary>
    public string Alias { get; }

    /// <summary>
    /// Gets whether the alias should be visible in help output.
    /// </summary>
    public bool Visible { get; }
}
