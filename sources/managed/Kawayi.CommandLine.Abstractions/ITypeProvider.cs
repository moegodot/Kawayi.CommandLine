// Copyright (c) 2026 GodotAsync<me@kawayi.moe>.
// Licensed under the GNU Affero General Public License v3-or-later license.

using System.Collections.Immutable;

namespace Kawayi.CommandLine.Abstractions;

public interface ITypeProvider
{
    bool CanBeDictionaryKey { get; }
    bool IsComparable { get; }
    ParsingResult Parse(ImmutableArray<Token> input);
}
