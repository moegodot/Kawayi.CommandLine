// Copyright (c) 2026 MoeGodot<me@kawayi.moe>.
// Licensed under the GNU Affero General Public License v3-or-later license.

namespace Kawayi.CommandLine.Abstractions;

public readonly record struct Color(byte R, byte G, byte B, byte A)
{
    public static Color None { get; } = new(0, 0, 0, 0);
    public static Color White { get; } = new(255, 255, 255, 255);
    public static Color Slate { get; } = new(148, 163, 184, 255);
    public static Color Sky { get; } = new(56, 189, 248, 255);
    public static Color Emerald { get; } = new(16, 185, 129, 255);
    public static Color Amber { get; } = new(245, 158, 11, 255);
    public static Color Rose { get; } = new(244, 63, 94, 255);
}

public record Style(Color Foreground,Color Background, bool Bold, bool Underline, bool Italic)
{
    public static string ClearStyle { get; } = "\u001b[0m";

    public string ToAnsiCode()
    {
        var codes = new List<string>(5);

        if (Bold)
        {
            codes.Add("1");
        }

        if (Italic)
        {
            codes.Add("3");
        }

        if (Underline)
        {
            codes.Add("4");
        }

        if (Foreground.A > 0)
        {
            codes.Add($"38;2;{Foreground.R};{Foreground.G};{Foreground.B}");
        }

        if (Background.A > 0)
        {
            codes.Add($"48;2;{Background.R};{Background.G};{Background.B}");
        }

        return codes.Count == 0
            ? string.Empty
            : $"\u001b[{string.Join(';', codes)}m";
    }
}
