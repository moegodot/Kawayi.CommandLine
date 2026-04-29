// Copyright (c) 2026 MoeGodot<me@kawayi.moe>.
// Licensed under the GNU Affero General Public License v3-or-later license.

using System.Collections.Immutable;

namespace Kawayi.CommandLine.Abstractions;

/// <summary>
/// Exposes generated documentation metadata for a command type.
/// </summary>
public interface IDocumentExporter
{
    /// <summary>
    /// Gets the documentation entries keyed by member name.
    /// </summary>
    static abstract ImmutableDictionary<string, Document> Documents { get; }
}
