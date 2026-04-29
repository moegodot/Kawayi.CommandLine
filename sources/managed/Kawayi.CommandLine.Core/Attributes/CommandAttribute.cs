// Copyright (c) 2026 MoeGodot<me@kawayi.moe>.
// Licensed under the GNU Affero General Public License v3-or-later license.

namespace Kawayi.CommandLine.Core.Attributes;

/// <summary>
/// Marks a type as a command and enables the default command generators.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = true, AllowMultiple = false)]
public sealed class CommandAttribute : Attribute
{
    /// <summary>
    /// Initializes a new command attribute.
    /// </summary>
    public CommandAttribute()
    {
    }
}
