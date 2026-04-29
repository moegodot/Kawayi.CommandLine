// Copyright (c) 2026 MoeGodot<me@kawayi.moe>.
// Licensed under the GNU Affero General Public License v3-or-later license.

namespace Kawayi.Escapes;

/// <summary>
/// Defines a reversible escaping strategy for command-line text.
/// </summary>
public interface IEscapeRule
{
    /// <summary>
    /// Escapes the supplied text.
    /// </summary>
    /// <param name="original">The raw text to escape.</param>
    /// <returns>The escaped representation.</returns>
    string Escape(string original);

    /// <summary>
    /// Reverts escaped text back to its original representation.
    /// </summary>
    /// <param name="escaped">The escaped text to unescape.</param>
    /// <returns>The unescaped representation.</returns>
    string Unescape(string escaped);
}
