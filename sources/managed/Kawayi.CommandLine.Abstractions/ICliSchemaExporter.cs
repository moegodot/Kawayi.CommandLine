// Copyright (c) 2026 MoeGodot<me@kawayi.moe>.
// Licensed under the GNU Affero General Public License v3-or-later license.

namespace Kawayi.CommandLine.Abstractions;

/// <summary>
/// Exposes a generated schema exporter for a command type.
/// </summary>
public interface ICliSchemaExporter
{
    /// <summary>
    /// Exports an immutable parsing schema for the current command type.
    /// </summary>
    /// <param name="parsingOptions">The parsing options used while exporting the schema.</param>
    /// <returns>The exported parsing schema.</returns>
    static abstract CliSchema ExportSchema(ParsingOptions parsingOptions);
}
