// Copyright (c) 2026 MoeGodot<me@kawayi.moe>.
// Licensed under the GNU Affero General Public License v3-or-later license.

using System.Collections.Immutable;

namespace Kawayi.CommandLine.Abstractions;

public interface IParsable<T>
{
    static abstract ParsingResult CreateParsing(ParsingOptions options, ImmutableArray<Token> arguments, T initialState);
}
