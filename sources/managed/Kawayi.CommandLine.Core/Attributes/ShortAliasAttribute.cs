// Copyright (c) 2026 MoeGodot<me@kawayi.moe>.
// Licensed under the GNU Affero General Public License v3-or-later license.

namespace Kawayi.CommandLine.Core.Attributes;

[AttributeUsage(AttributeTargets.Property, AllowMultiple = true)]
public sealed class ShortAliasAttribute : Attribute
{
    public string Alias { get; }

    public ShortAliasAttribute(string alias)
    {
        Alias = alias;
    }
}
