// Copyright (c) 2026 MoeGodot<me@kawayi.moe>.
// Licensed under the GNU Affero General Public License v3-or-later license.

namespace Kawayi.CommandLine.Abstractions;

/// <summary>
/// Converts common .NET identifier casing styles into command-line casing styles.
/// </summary>
public static class CaseConverter
{
    /// <summary>
    /// Converts a PascalCase or camelCase identifier to kebab-case.
    /// </summary>
    /// <param name="pascal">The identifier to convert.</param>
    /// <returns>The converted kebab-case identifier.</returns>
    public static string Pascal2Kebab(string pascal)
    {
        ArgumentNullException.ThrowIfNull(pascal);

        if (pascal.Length == 0)
        {
            return string.Empty;
        }

        var builder = new System.Text.StringBuilder(pascal.Length + 8);
        var previousKind = CharacterKind.None;

        for (var i = 0; i < pascal.Length; i++)
        {
            var current = pascal[i];
            if (IsSeparator(current))
            {
                AppendSeparator(builder);
                previousKind = CharacterKind.Separator;
                continue;
            }

            var currentKind = GetCharacterKind(current);
            var nextKind = i + 1 < pascal.Length && !IsSeparator(pascal[i + 1])
                ? GetCharacterKind(pascal[i + 1])
                : CharacterKind.None;

            if (ShouldAppendSeparator(previousKind, currentKind, nextKind))
            {
                AppendSeparator(builder);
            }

            builder.Append(char.ToLowerInvariant(current));
            previousKind = currentKind;
        }

        if (builder.Length > 0 && builder[^1] == '-')
        {
            builder.Length--;
        }

        return builder.ToString();
    }

    private static bool IsSeparator(char value)
    {
        return value is '-' or '_' || char.IsWhiteSpace(value);
    }

    private static bool ShouldAppendSeparator(
        CharacterKind previousKind,
        CharacterKind currentKind,
        CharacterKind nextKind)
    {
        return previousKind switch
        {
            CharacterKind.None or CharacterKind.Separator => false,
            CharacterKind.Lower => currentKind is CharacterKind.Upper or CharacterKind.Digit,
            CharacterKind.Upper => currentKind is CharacterKind.Digit ||
                                   currentKind == CharacterKind.Upper && nextKind == CharacterKind.Lower,
            CharacterKind.Digit => currentKind is CharacterKind.Lower or CharacterKind.Upper,
            _ => false,
        };
    }

    private static void AppendSeparator(System.Text.StringBuilder builder)
    {
        if (builder.Length > 0 && builder[^1] != '-')
        {
            builder.Append('-');
        }
    }

    private static CharacterKind GetCharacterKind(char value)
    {
        if (char.IsUpper(value))
        {
            return CharacterKind.Upper;
        }

        if (char.IsLower(value))
        {
            return CharacterKind.Lower;
        }

        return char.IsDigit(value)
            ? CharacterKind.Digit
            : CharacterKind.Lower;
    }

    private enum CharacterKind
    {
        None,
        Separator,
        Lower,
        Upper,
        Digit,
    }
}
