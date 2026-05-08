// Copyright (c) 2026 MoeGodot<me@kawayi.moe>.
// Licensed under the GNU Affero General Public License v3-or-later license.

using System.Collections.Immutable;
using Kawayi.CommandLine.Abstractions;
using Kawayi.CommandLine.Extensions;

namespace Kawayi.CommandLine.Core.Tests;

public sealed class CliSchemaExtensionsTests
{
    [Test]
    public async Task TryMerge_Merges_NonConflicting_Schema_Definitions()
    {
        var baseInput = CreateParameter("base-input");
        var baseOption = CreateProperty("base-option");
        var derivedInput = CreateParameter("derived-input");
        var derivedOption = CreateProperty("derived-option");
        var baseCommand = CreateCommand("base-run");
        var derivedCommand = CreateCommand("derived-run");
        var baseSchema = CreateSchema(
            typeof(BaseSchemaMarker),
            arguments: [baseInput],
            properties: [baseOption],
            subcommands: [baseCommand]);
        var derivedSchema = CreateSchema(
            typeof(DerivedSchemaMarker),
            arguments: [derivedInput],
            properties: [derivedOption],
            subcommands: [derivedCommand]);

        var merged = baseSchema.Merge(derivedSchema, allowOverride: false);

        await Assert.That(merged.GeneratedFrom).IsEqualTo(typeof(DerivedSchemaMarker));
        await Assert.That(merged.Argument.Select(static item => item.Information.Name.Value)).IsEquivalentTo(["base-input", "derived-input"]);
        await Assert.That(merged.Properties[new LongOptionToken("base-option")]).IsEqualTo(baseOption);
        await Assert.That(merged.Properties[new LongOptionToken("derived-option")]).IsEqualTo(derivedOption);
        await Assert.That(merged.SubcommandDefinitions[new ArgumentOrCommandToken("base-run")]).IsEqualTo(baseCommand);
        await Assert.That(merged.SubcommandDefinitions[new ArgumentOrCommandToken("derived-run")]).IsEqualTo(derivedCommand);
        await Assert.That(merged.Subcommands.ContainsKey(new ArgumentOrCommandToken("base-run"))).IsTrue();
        await Assert.That(merged.Subcommands.ContainsKey(new ArgumentOrCommandToken("derived-run"))).IsTrue();
    }

    [Test]
    public async Task TryMerge_ReturnsFalse_For_Duplicate_Definitions_When_Override_Is_Disabled()
    {
        var baseSchema = CreateSchema(
            null,
            arguments: [CreateParameter("input")],
            properties: [CreateProperty("format")]);
        var derivedSchema = CreateSchema(
            null,
            arguments: [CreateParameter("input")],
            properties: [CreateProperty("format")]);

        var success = baseSchema.TryMerge(derivedSchema, allowOverride: false, out var merged);

        await Assert.That(success).IsFalse();
        await Assert.That(merged).IsNull();
        await Assert.That(() => baseSchema.Merge(derivedSchema, allowOverride: false)).Throws<InvalidOperationException>();
    }

    [Test]
    public async Task Merge_Allows_Another_Schema_To_Override_Duplicate_Definitions()
    {
        var baseInput = CreateParameter("input", typeof(string));
        var derivedInput = CreateParameter("input", typeof(int));
        var baseFormat = CreateProperty("format", typeof(string));
        var derivedFormat = CreateProperty("format", typeof(bool));
        var baseSchema = CreateSchema(null, arguments: [baseInput], properties: [baseFormat]);
        var derivedSchema = CreateSchema(null, arguments: [derivedInput], properties: [derivedFormat]);

        var merged = baseSchema.Merge(derivedSchema, allowOverride: true);

        await Assert.That(merged.Argument.Count).IsEqualTo(1);
        await Assert.That(merged.Argument[0]).IsEqualTo(derivedInput);
        await Assert.That(merged.Properties[new LongOptionToken("format")]).IsEqualTo(derivedFormat);
    }

    [Test]
    public async Task TryMerge_Detects_Subcommand_Alias_Token_Conflicts()
    {
        var baseCommand = CreateCommand("serve", aliases: ["run"]);
        var derivedCommand = CreateCommand("run");
        var baseSchema = CreateSchema(null, subcommands: [baseCommand]);
        var derivedSchema = CreateSchema(null, subcommands: [derivedCommand]);

        var success = baseSchema.TryMerge(derivedSchema, allowOverride: false, out var merged);

        await Assert.That(success).IsFalse();
        await Assert.That(merged).IsNull();
    }

    private static CliSchema CreateSchema(
        Type? generatedFrom,
        ImmutableArray<ParameterDefinition> arguments = default,
        ImmutableArray<PropertyDefinition> properties = default,
        ImmutableArray<CommandDefinition> subcommands = default)
    {
        var builder = new CliSchemaBuilder(
            ImmutableDictionary.CreateBuilder<string, CommandDefinition>(),
            ImmutableDictionary.CreateBuilder<string, CliSchemaBuilder>(),
            ImmutableDictionary.CreateBuilder<string, PropertyDefinition>(),
            ImmutableList.CreateBuilder<ParameterDefinition>())
        {
            GeneratedFrom = generatedFrom
        };

        if (!arguments.IsDefault)
        {
            foreach (var argument in arguments)
            {
                builder.Argument.Add(argument);
            }
        }

        if (!properties.IsDefault)
        {
            foreach (var property in properties)
            {
                builder.Properties[property.Information.Name.Value] = property;
            }
        }

        if (!subcommands.IsDefault)
        {
            foreach (var command in subcommands)
            {
                builder.SubcommandDefinitions[command.Information.Name.Value] = command;
                builder.Subcommands[command.Information.Name.Value] = CreateSchemaBuilder();
            }
        }

        return builder.Build();
    }

    private static CliSchemaBuilder CreateSchemaBuilder()
    {
        return new CliSchemaBuilder(
            ImmutableDictionary.CreateBuilder<string, CommandDefinition>(),
            ImmutableDictionary.CreateBuilder<string, CliSchemaBuilder>(),
            ImmutableDictionary.CreateBuilder<string, PropertyDefinition>(),
            ImmutableList.CreateBuilder<ParameterDefinition>());
    }

    private static ParameterDefinition CreateParameter(string name, Type? type = null)
    {
        return new(CreateInformation(name), null, ValueRange.One, type ?? typeof(string), true);
    }

    private static PropertyDefinition CreateProperty(string name, Type? type = null)
    {
        return new(
            CreateInformation(name),
            ImmutableDictionary<string, NameWithVisibility>.Empty,
            ImmutableDictionary<string, NameWithVisibility>.Empty,
            null,
            type ?? typeof(string),
            false);
    }

    private static CommandDefinition CreateCommand(string name, ImmutableArray<string> aliases = default)
    {
        return new(
            CreateInformation(name),
            aliases.IsDefault
                ? ImmutableDictionary<string, NameWithVisibility>.Empty
                : aliases.ToImmutableDictionary(static item => item, static item => new NameWithVisibility(item, true)),
            null);
    }

    private static DefinitionInformation CreateInformation(string name)
    {
        return new(new NameWithVisibility(name, true), new Document($"{name} summary", $"{name} help"));
    }

    private sealed class BaseSchemaMarker
    {
    }

    private sealed class DerivedSchemaMarker
    {
    }
}
