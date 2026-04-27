// Copyright (c) 2026 MoeGodot<me@kawayi.moe>.
// Licensed under the GNU Affero General Public License v3-or-later license.

namespace Kawayi.Escapes;

public interface IEscapeRule
{
    string Escape(string original);
    string Unescape(string escaped);
}
