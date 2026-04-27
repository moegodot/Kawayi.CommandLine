// Copyright (c) 2026 MoeGodot<me@kawayi.moe>.
// Licensed under the GNU Affero General Public License v3-or-later license.

namespace Kawayi.CommandLine.Abstractions;

public record ProgramInformation(string Name,
                                 Document Document,
                                 Version Version,
                                 string Homepage)
{
}
