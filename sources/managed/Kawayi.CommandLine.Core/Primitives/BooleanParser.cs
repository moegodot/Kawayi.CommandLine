// Copyright (c) 2026 MoeGodot<me@kawayi.moe>.
// Licensed under the GNU Affero General Public License v3-or-later license.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Kawayi.CommandLine.Abstractions;

namespace Kawayi.CommandLine.Core.Primitives;

/// <summary>
/// Parses boolean values from command-line tokens.
/// </summary>
public sealed class BooleanParser : IBuiltInTypeProvider
{
    /// <summary>
    /// Parses a boolean value from the supplied tokens.
    /// </summary>
    public bool TryParse(ImmutableArray<Token> input,
                         TypeProviders typeProviders,
                         string? format,
                         [NotNullWhen(true)] out object? result,
                         [NotNullWhen(false)] out string? error)
    {
        if (input.IsDefaultOrEmpty)
        {
            result = false;
            error = null;
            return true;
        }

        var token = input[^1];

        if (bool.TryParse(token.Value, out var parsedValue))
        {
            result = parsedValue;
            error = null;
            return true;
        }

        result = null;
        error = "bool";
        return false;
    }
}
