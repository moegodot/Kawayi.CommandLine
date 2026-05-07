// Copyright (c) 2026 MoeGodot<me@kawayi.moe>.
// Licensed under the GNU Affero General Public License v3-or-later license.

namespace Kawayi.CommandLine.Core.Attributes;

/// <summary>
/// Marks a property as a nested subcommand.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class SubcommandAttribute : SymbolAttribute
{
    /// <summary>
    /// Initializes a new subcommand attribute.
    /// </summary>
    /// <param name="require">Whether the subcommand is required.</param>
    /// <param name="visible">Whether the subcommand should be visible in help output.</param>
    /// <param name="global">Whether the subcommand should be present as parent command,
    /// this making the subcommand instantiated.
    /// </param>
    public SubcommandAttribute(bool require = false, bool visible = true, bool global = false) : base(require, visible)
    {
        if (require)
        {
            throw new NotSupportedException("Required subcommands are not supported.");
        }
    }
}
