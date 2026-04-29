// Copyright (c) 2026 MoeGodot<me@kawayi.moe>.
// Licensed under the GNU Affero General Public License v3-or-later license.

using System.Collections;
using System.Collections.Immutable;

namespace Kawayi.CommandLine.Abstractions;

public abstract record PossibleValues;

public interface ICountablePossibleValues
{
    IEnumerable Candidates { get; }
}

public sealed record CountablePossibleValues<T>(ImmutableArray<T> Candidates) : PossibleValues, ICountablePossibleValues
{
    IEnumerable ICountablePossibleValues.Candidates => Candidates;
}

public sealed record DescripablePossibleValues(string Description) : PossibleValues();
