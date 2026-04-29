// Copyright (c) 2026 MoeGodot<me@kawayi.moe>.
// Licensed under the GNU Affero General Public License v3-or-later license.

using System.Text;

namespace Kawayi.CommandLine.Abstractions;

/// <summary>
/// Builds styled terminal text while optionally emitting ANSI escape sequences.
/// </summary>
public sealed class StyledStringBuilder
{
    /// <summary>
    /// Gets the newline sequence used by the builder.
    /// </summary>
    public const string NewLine = "\n";

    private StringBuilder Builder { get; } = new();

    /// <summary>
    /// Gets whether styling is enabled.
    /// </summary>
    public bool EnableStyle { get; }

    /// <summary>
    /// Initializes a new styled string builder.
    /// </summary>
    /// <param name="enableStyle">Whether styling should be emitted.</param>
    public StyledStringBuilder(bool enableStyle)
    {
        EnableStyle = enableStyle;
    }

    /// <summary>
    /// Appends a line of plain text.
    /// </summary>
    /// <param name="text">The text to append.</param>
    /// <returns>The current builder.</returns>
    public StyledStringBuilder AppendLine(string text = "")
    {
        Builder.Append($"{text}{NewLine}");
        return this;
    }

    /// <summary>
    /// Appends plain text.
    /// </summary>
    /// <param name="text">The text to append.</param>
    /// <returns>The current builder.</returns>
    public StyledStringBuilder Append(string text)
    {
        Builder.Append(text);
        return this;
    }

    /// <summary>
    /// Appends styled text.
    /// </summary>
    /// <param name="style">The style to apply.</param>
    /// <param name="text">The text to append.</param>
    /// <returns>The current builder.</returns>
    public StyledStringBuilder Append(Style style, string text)
    {
        if (!EnableStyle)
        {
            return Append(text);
        }

        var ansiCode = style.ToAnsiCode();

        Builder.Append(ansiCode.Length == 0
            ? text
            : $"{ansiCode}{text}{Style.ClearStyle}");
        return this;
    }

    /// <summary>
    /// Appends a styled line of text.
    /// </summary>
    /// <param name="style">The style to apply.</param>
    /// <param name="text">The text to append.</param>
    /// <returns>The current builder.</returns>
    public StyledStringBuilder AppendLine(Style style, string text)
    {
        if (!EnableStyle)
        {
            return AppendLine(text);
        }

        var ansiCode = style.ToAnsiCode();

        Builder.Append(ansiCode.Length == 0
            ? $"{text}{NewLine}"
            : $"{ansiCode}{text}{Style.ClearStyle}{NewLine}");
        return this;
    }

    /// <summary>
    /// Returns the accumulated text.
    /// </summary>
    /// <returns>The accumulated text.</returns>
    public override string ToString() => Builder.ToString();
}
