// Copyright (c) 2026 MoeGodot<me@kawayi.moe>.
// Licensed under the GNU Affero General Public License v3-or-later license.

using System.Collections.Immutable;

namespace Kawayi.CommandLine.Abstractions;

/// <summary>
/// Builds a mutable command-line schema before producing an immutable parsing snapshot.
/// </summary>
public interface IParsingBuilder
{
    /// <summary>
    /// Gets the parsing options associated with the builder.
    /// </summary>
    ParsingOptions ParsingOptions { get; }

    /// <summary>
    /// Gets the mutable subcommand definition registry.
    /// </summary>
    ImmutableDictionary<string, CommandDefinition>.Builder SubcommandDefinitions { get; }

    /// <summary>
    /// Gets the mutable child parser registry keyed by subcommand name.
    /// </summary>
    ImmutableDictionary<string, IParsingBuilder>.Builder Subcommands { get; }

    /// <summary>
    /// Gets the mutable property definition registry.
    /// </summary>
    ImmutableDictionary<string, PropertyDefinition>.Builder Properties { get; }

    /// <summary>
    /// Gets the mutable ordered positional argument definitions.
    /// </summary>
    ImmutableList<ArgumentDefinition>.Builder Argument { get; }

    /// <summary>
    /// Produces an immutable parsing snapshot from the current builder state.
    /// </summary>
    /// <returns>The immutable parsing snapshot.</returns>
    ParsingInput Build();
}
