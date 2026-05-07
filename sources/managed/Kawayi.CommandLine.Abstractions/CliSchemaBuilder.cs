// Copyright (c) 2026 GodotAsync<me@kawayi.moe>.
// Licensed under the GNU Affero General Public License v3-or-later license.

using System.Collections.Immutable;

namespace Kawayi.CommandLine.Abstractions;

/// <summary>
///
/// </summary>
/// <param name="SubcommandDefinitions">the mutable subcommand definition registry.</param>
/// <param name="Subcommands">the mutable child parser registry keyed by subcommand name</param>
/// <param name="Properties">the mutable property definition registry</param>
/// <param name="Argument">the mutable ordered positional argument definitions</param>
public sealed record CliSchemaBuilder(
    ImmutableDictionary<string, CommandDefinition>.Builder SubcommandDefinitions,
    ImmutableDictionary<string, CliSchemaBuilder>.Builder Subcommands,
    ImmutableDictionary<string, PropertyDefinition>.Builder Properties,
    ImmutableList<ParameterDefinition>.Builder Argument)
{
    private bool _built = false;

    /// <summary>
    /// see <see cref="CliSchema.GeneratedFrom"/>
    /// </summary>
    public Type? GeneratedFrom { get; set; }


    public CliSchema Build()
    {
        if (_built)
        {
            throw new InvalidOperationException("try to build schema for multiple time");
        }

        _built = true;

        throw new NotImplementedException("""
                                          implement build()
                                          检查SubcommandDefinitions,Subcommands,Properties: key == value.Information.Name
                                          检查Argument: ValueRange合法,这里的合法指的是贪心算法能够解析
                                          """);
    }
}
