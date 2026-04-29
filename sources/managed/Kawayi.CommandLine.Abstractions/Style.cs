// Copyright (c) 2026 MoeGodot<me@kawayi.moe>.
// Licensed under the GNU Affero General Public License v3-or-later license.

namespace Kawayi.CommandLine.Abstractions;

/// <summary>
/// Represents an RGBA color used by terminal styles.
/// </summary>
/// <param name="R">The red channel.</param>
/// <param name="G">The green channel.</param>
/// <param name="B">The blue channel.</param>
/// <param name="A">The alpha channel.</param>
public readonly record struct Color(byte R, byte G, byte B, byte A)
{
    /// <summary>
    /// Gets a fully transparent color.
    /// </summary>
    public static Color None { get; } = new(0, 0, 0, 0);

    /// <summary>
    /// Gets a white color.
    /// </summary>
    public static Color White { get; } = new(255, 255, 255, 255);

    /// <summary>
    /// Gets a slate color.
    /// </summary>
    public static Color Slate { get; } = new(148, 163, 184, 255);

    /// <summary>
    /// Gets a sky color.
    /// </summary>
    public static Color Sky { get; } = new(56, 189, 248, 255);

    /// <summary>
    /// Gets an emerald color.
    /// </summary>
    public static Color Emerald { get; } = new(16, 185, 129, 255);

    /// <summary>
    /// Gets an amber color.
    /// </summary>
    public static Color Amber { get; } = new(245, 158, 11, 255);

    /// <summary>
    /// Gets a rose color.
    /// </summary>
    public static Color Rose { get; } = new(244, 63, 94, 255);
}

/// <summary>
/// Represents ANSI styling information for terminal output.
/// </summary>
/// <param name="Foreground">The foreground color.</param>
/// <param name="Background">The background color.</param>
/// <param name="Bold">Whether bold formatting is enabled.</param>
/// <param name="Underline">Whether underline formatting is enabled.</param>
/// <param name="Italic">Whether italic formatting is enabled.</param>
public record Style(Color Foreground, Color Background, bool Bold, bool Underline, bool Italic)
{
    /// <summary>
    /// Gets the ANSI sequence that clears all styling.
    /// </summary>
    public static string ClearStyle { get; } = "\u001b[0m";

    /// <summary>
    /// Converts the style to an ANSI escape sequence.
    /// </summary>
    /// <returns>The ANSI escape sequence, or an empty string when no styling is required.</returns>
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
