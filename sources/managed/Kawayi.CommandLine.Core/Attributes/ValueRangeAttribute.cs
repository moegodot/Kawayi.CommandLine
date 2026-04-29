// Copyright (c) 2026 MoeGodot<me@kawayi.moe>.
// Licensed under the GNU Affero General Public License v3-or-later license.

using Kawayi.CommandLine.Abstractions;

namespace Kawayi.CommandLine.Core.Attributes;

[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public sealed class ValueRangeAttribute : Attribute
{
    /// <summary>
    /// Gets the value-count range declared for the member.
    /// </summary>
    public ValueRange ValueRange { get; }

    /// <summary>
    /// Marks the minimum and maximum number of values a member can receive.
    /// </summary>
    public ValueRangeAttribute(int minimum, int maximum)
    {
        ValueRange = new ValueRange(minimum, maximum);
    }
}
