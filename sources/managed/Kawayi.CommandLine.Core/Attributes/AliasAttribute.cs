// Copyright (c) 2026 MoeGodot<me@kawayi.moe>.
// Licensed under the GNU Affero General Public License v3-or-later license.

namespace Kawayi.CommandLine.Core.Attributes;

[AttributeUsage(AttributeTargets.Property, AllowMultiple = true)]
public class AliasAttribute : Attribute
{
    public AliasAttribute(string alias, bool visible = true)
    {
        Alias = alias;
        Visible = visible;
    }

    public string Alias { get; }

    public bool Visible { get; }
}
