// Copyright (c) 2026 MoeGodot<me@kawayi.moe>.
// Licensed under the GNU Affero General Public License v3-or-later license.

using System.Text;

namespace Kawayi.CommandLine.Abstractions;

public sealed class StyledStringBuilder
{
    public const string NewLine = "\n";

    private StringBuilder Builder { get; } = new();

    public bool EnableStyle { get; }

    public StyledStringBuilder(bool enableStyle)
    {
        EnableStyle = enableStyle;
    }

    public StyledStringBuilder AppendLine(string text = "")
    {
        Builder.Append($"{text}{NewLine}");
        return this;
    }

    public StyledStringBuilder Append(string text)
    {
        Builder.Append(text);
        return this;
    }

    public StyledStringBuilder Append(Style style,string text)
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

    public StyledStringBuilder AppendLine(Style style,string text)
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

    public override string ToString() => Builder.ToString();
}
