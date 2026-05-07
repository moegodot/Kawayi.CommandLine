// Copyright (c) 2026 MoeGodot<me@kawayi.moe>.
// Licensed under the GNU Affero General Public License v3-or-later license.

namespace Kawayi.CommandLine.Abstractions;

/// <summary>
/// Exposes a generated schema exporter for a command type.
/// </summary>
public interface ICliSchemaExporter
{
    /// <summary>
    /// Exports a mutable parsing builder for the current command type.
    /// </summary>
    /// <param name="parsingOptions">The parsing options to attach to the exported builder.</param>
    /// <returns>The exported parsing builder.</returns>
    static abstract CliSchemaBuilder ExportParsing(ParsingOptions parsingOptions);
}
