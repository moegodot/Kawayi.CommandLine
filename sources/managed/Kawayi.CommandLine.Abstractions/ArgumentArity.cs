// Copyright (c) 2026 MoeGodot<me@kawayi.moe>.
// Licensed under the GNU Affero General Public License v3-or-later license.

namespace Kawayi.CommandLine.Abstractions;

/// <summary>
/// Describes how many values a positional argument can consume.
/// When multiple positional arguments are declared together, values are assigned greedily
/// from left to right while preserving the minimum value requirements of later arguments.
/// </summary>
public readonly record struct ArgumentArity
{
    /// <summary>
    /// Initializes a new positional argument arity.
    /// </summary>
    /// <param name="minimum">The minimum number of values the argument must receive.</param>
    /// <param name="maximum">The maximum number of values the argument can receive.</param>
    public ArgumentArity(int minimum, int maximum)
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
    /// Gets the minimum number of values the argument must receive.
    /// </summary>
    public int Minimum { get; }

    /// <summary>
    /// Gets the maximum number of values the argument can receive.
    /// </summary>
    public int Maximum { get; }

    /// <summary>
    /// Gets an arity that accepts zero or one value.
    /// </summary>
    public static ArgumentArity ZeroOrOne { get; } = new(0,1);

    /// <summary>
    /// Gets an arity that accepts any number of values, while still allowing later positional
    /// arguments to claim the values required by their own minimum arity.
    /// </summary>
    public static ArgumentArity ZeroOrMore { get; } = new(0,int.MaxValue);

    /// <summary>
    /// Gets an arity that requires exactly one value.
    /// </summary>
    public static ArgumentArity One { get; } = new(1,1);

    /// <summary>
    /// Gets an arity that requires at least one value.
    /// </summary>
    public static ArgumentArity OneOrMore { get; } = new(1,int.MaxValue);
}
