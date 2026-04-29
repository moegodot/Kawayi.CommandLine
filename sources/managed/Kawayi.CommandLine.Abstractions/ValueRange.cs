// Copyright (c) 2026 MoeGodot<me@kawayi.moe>.
// Licensed under the GNU Affero General Public License v3-or-later license.

namespace Kawayi.CommandLine.Abstractions;

/// <summary>
/// Describes a minimum and maximum count of received values.
/// </summary>
public readonly record struct ValueRange
{
    /// <summary>
    /// Initializes a new value-count range.
    /// </summary>
    /// <param name="minimum">The minimum number of values that must be received.</param>
    /// <param name="maximum">The maximum number of values that can be received.</param>
    public ValueRange(int minimum, int maximum)
    {
        if (minimum < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(minimum), minimum, "Minimum cannot be negative.");
        }

        if (maximum < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maximum), maximum, "Maximum cannot be negative.");
        }

        if (minimum > maximum)
        {
            throw new ArgumentException("Minimum cannot be greater than maximum.");
        }

        Minimum = minimum;
        Maximum = maximum;
    }

    /// <summary>
    /// Gets the minimum number of values that must be received.
    /// </summary>
    public int Minimum { get; }

    /// <summary>
    /// Gets the maximum number of values that can be received.
    /// </summary>
    public int Maximum { get; }

    /// <summary>
    /// Gets a range that accepts zero or one value.
    /// </summary>
    public static ValueRange ZeroOrOne { get; } = new(0,1);

    /// <summary>
    /// Gets a range that accepts any number of values.
    /// </summary>
    public static ValueRange ZeroOrMore { get; } = new(0,int.MaxValue);

    /// <summary>
    /// Gets a range that requires exactly one value.
    /// </summary>
    public static ValueRange One { get; } = new(1,1);

    /// <summary>
    /// Gets a range that requires at least one value.
    /// </summary>
    public static ValueRange OneOrMore { get; } = new(1,int.MaxValue);
}
