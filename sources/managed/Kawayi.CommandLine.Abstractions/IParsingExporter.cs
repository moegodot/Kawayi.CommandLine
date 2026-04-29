// Copyright (c) 2026 MoeGodot<me@kawayi.moe>.
// Licensed under the GNU Affero General Public License v3-or-later license.

namespace Kawayi.CommandLine.Abstractions;

public interface IParsingExporter
{
    static abstract IParsingBuilder ExportParsing(ParsingOptions parsingOptions);
}
