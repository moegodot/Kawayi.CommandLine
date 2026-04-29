// Copyright (c) 2026 MoeGodot<me@kawayi.moe>.
// Licensed under the GNU Affero General Public License v3-or-later license.

namespace Kawayi.CommandLine.Core.Attributes;

[AttributeUsage(AttributeTargets.Property, AllowMultiple = true)]
public sealed class LongAliasAttribute : AliasAttribute
{
    public LongAliasAttribute(string alias,bool visible = true) : base(alias,visible)
    {
    }
}
