// Copyright (c) 2026 MoeGodot<me@kawayi.moe>.
// Licensed under the GNU Affero General Public License v3-or-later license.

namespace Kawayi.CommandLine.Core.Attributes;

[AttributeUsage(AttributeTargets.Property)]
public sealed class ArgumentAttribute : SymbolAttribute
{
    /// <summary>
    /// mark the position of the argument in the class
    /// </summary>
    public int Position { get; }

    public ArgumentAttribute(int position, bool require = false, bool visible = true) : base(require, visible)
    {
        Position = position;
    }
}
