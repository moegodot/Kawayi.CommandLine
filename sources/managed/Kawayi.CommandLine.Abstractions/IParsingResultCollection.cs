// Copyright (c) 2026 MoeGodot<me@kawayi.moe>.
// Licensed under the GNU Affero General Public License v3-or-later license.

namespace Kawayi.CommandLine.Abstractions;

/// <summary>
/// Represents the parsing result for a single command scope in the selected command tree.
/// </summary>
public interface IParsingResultCollection
{
    /// <summary>
    /// Gets the command represented by this result node. The root node has a null command.
    /// </summary>
    CommandDefinition? Command { get; }

    /// <summary>
    /// Gets the parent result node, or null when this node is the root.
    /// </summary>
    IParsingResultCollection? Parent { get; }

    /// <summary>
    /// Tries to get the explicit value parsed for the specified definition in the current scope only.
    /// This method does not traverse the parent chain and does not apply default values.
    /// </summary>
    bool TryGetValue(TypedDefinition definition, out object? value);

    /// <summary>
    /// Tries to get a directly selected subcommand result from the current scope.
    /// </summary>
    bool TryGetSubcommand(CommandDefinition definition, out IParsingResultCollection result);

    /// <summary>
    /// Gets the schema metadata for the current scope only.
    /// </summary>
    IParsingScopeMetadata Scope { get; }
}
