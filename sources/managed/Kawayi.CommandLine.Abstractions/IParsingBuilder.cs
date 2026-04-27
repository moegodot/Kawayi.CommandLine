// Copyright (c) 2026 MoeGodot<me@kawayi.moe>.
// Licensed under the GNU Affero General Public License v3-or-later license.

namespace Kawayi.CommandLine.Abstractions;

public interface IParsingBuilder : IParsable<IParsingResultCollection>
{
    IList<CommandDefinition> Commands { get; }
    IList<PropertyDefinition> Properties { get; }
    IList<ArgumentDefinition> Argument { get; }
}
