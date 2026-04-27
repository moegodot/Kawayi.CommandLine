// Copyright (c) 2026 MoeGodot<me@kawayi.moe>.
// Licensed under the GNU Affero General Public License v3-or-later license.

namespace Kawayi.CommandLine.Core.Attributes;

[AttributeUsage(AttributeTargets.Property)]
public sealed class ArgumentAttribute: SymbolAttribute
{
    public ArgumentAttribute(bool visible, bool require) : base(visible, require)
    {
    }
}
