// Copyright (c) 2026 MoeGodot<me@kawayi.moe>.
// Licensed under the GNU Affero General Public License v3-or-later license.

using System.Collections;
using System.Collections.Immutable;

namespace Kawayi.CommandLine.Abstractions;

/// <summary>
/// Represents metadata describing the possible values for a definition.
/// </summary>
public abstract record PossibleValues;

/// <summary>
/// Exposes a possible-values source that can be enumerated and counted.
/// </summary>
public interface ICountablePossibleValues
{
    /// <summary>
    /// Gets the candidate values.
    /// </summary>
    IEnumerable Candidates { get; }
}

/// <summary>
/// Represents a concrete finite set of candidate values.
/// </summary>
/// <typeparam name="T">The candidate value type.</typeparam>
/// <param name="Candidates">The candidate values.</param>
public sealed record CountablePossibleValues<T>(ImmutableArray<T> Candidates) : PossibleValues, ICountablePossibleValues
{
    IEnumerable ICountablePossibleValues.Candidates => Candidates;
}

/// <summary>
/// Represents possible values described only by text.
/// </summary>
/// <param name="Description">The textual description of the possible values.</param>
public sealed record DescripablePossibleValues(string Description) : PossibleValues();
