// Copyright (c) 2026 MoeGodot<me@kawayi.moe>.
// Licensed under the GNU Affero General Public License v3-or-later license.

namespace Kawayi.CommandLine.Core.Attributes;

/// <summary>
/// Marks a type as eligible for generated binding support.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class BindableAttribute : Attribute
{
}
