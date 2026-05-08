// Copyright (c) 2026 GodotAsync<me@kawayi.moe>.
// Licensed under the GNU Affero General Public License v3-or-later license.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;

namespace Kawayi.CommandLine.Abstractions;

/// <summary>
/// Defines a runtime type parsing extension point that may choose whether it applies.
/// </summary>
public interface IExtendedTypeProvider
{
    /// <summary>
    /// Attempts to parse the supplied tokens for the specified runtime type.
    /// </summary>
    /// <param name="input">The tokens to parse.</param>
    /// <param name="typeProviders">The exact and extended providers visible to this parsing operation.</param>
    /// <param name="symbolType">The runtime type requested by the caller.</param>
    /// <param name="format">An optional format hint supplied by the caller.</param>
    /// <param name="result">The parsed value when parsing succeeds.</param>
    /// <param name="error">
    /// The expected value description when this provider handles the type but rejects the input;
    /// or <see langword="null"/> when this provider does not handle <paramref name="symbolType"/>.
    /// </param>
    /// <returns><see langword="true"/> when parsing succeeds; otherwise, <see langword="false"/>.</returns>
    bool TryParse(ImmutableArray<Token> input,
                  TypeProviders typeProviders,
                  Type symbolType,
                  string? format,
                  [NotNullWhen(true)]out object? result,
                  out string? error);
}
