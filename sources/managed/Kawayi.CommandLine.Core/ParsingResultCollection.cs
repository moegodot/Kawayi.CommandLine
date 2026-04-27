// Copyright (c) 2026 MoeGodot<me@kawayi.moe>.
// Licensed under the GNU Affero General Public License v3-or-later license.

using System.Collections.Immutable;
using Kawayi.CommandLine.Abstractions;

namespace Kawayi.CommandLine.Core;

public sealed class ParsingResultCollection : IParsingResultCollection
{
    public ImmutableDictionary<string, CommandDefinition> Commands { get; }
    public object GetValue(TypedDefinition definition) => throw new NotImplementedException();
}
