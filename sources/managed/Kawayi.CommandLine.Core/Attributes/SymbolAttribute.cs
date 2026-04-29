// Copyright (c) 2026 MoeGodot<me@kawayi.moe>.
// Licensed under the GNU Affero General Public License v3-or-later license.

using System.Collections.Immutable;

namespace Kawayi.CommandLine.Core.Attributes;

public abstract class SymbolAttribute : Attribute
{
    public SymbolAttribute(bool require = false,bool visible = true)
    {
        Visible = visible;
        Require = require;
    }
    public bool Visible { get; }
    public bool Require { get; }
}
