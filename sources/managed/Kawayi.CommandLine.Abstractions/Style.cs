// Copyright (c) 2026 MoeGodot<me@kawayi.moe>.
// Licensed under the GNU Affero General Public License v3-or-later license.

namespace Kawayi.CommandLine.Abstractions;

public record Style(byte R, byte G, byte B, byte A, bool Bold, bool Underline, bool Italic)
{
    public static string ClearStyle { get; } = "TODO";

    public string ToAnsiCode()
    {
        throw new NotImplementedException("TODO");
    }
}
