// Copyright (c) 2026 MoeGodot<me@kawayi.moe>.
// Licensed under the GNU Affero General Public License v3-or-later license.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Kawayi.CommandLine.Abstractions;

namespace Kawayi.CommandLine.Core.Primitives;

/// <summary>
/// Parses enumeration values when the target enum type is only known at runtime.
/// </summary>
public sealed class EnumParser : IBuiltInExtendedTypeProvider
{
    /// <summary>
    /// Attempts to parse an enum value from the supplied tokens.
    /// </summary>
    public bool TryParse(ImmutableArray<Token> input,
                         TypeProviders typeProviders,
                         Type symbolType,
                         string? format,
                         [NotNullWhen(true)] out object? result,
                         out string? error)
    {
        ArgumentNullException.ThrowIfNull(symbolType);

        if (!symbolType.IsEnum)
        {
            result = null;
            error = null;
            return false;
        }

        if (input.IsDefaultOrEmpty)
        {
            result = Enum.ToObject(symbolType, 0);
            error = null;
            return true;
        }

        var expectation = $"{symbolType.Name} enum";
        var token = input[^1].Value;

        if (Enum.TryParse(symbolType, token, true, out var parsedValue) && parsedValue is not null)
        {
            result = parsedValue;
            error = null;
            return true;
        }

        result = null;
        error = expectation;
        return false;
    }
}
