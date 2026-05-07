// Copyright (c) 2026 MoeGodot<me@kawayi.moe>.
// Licensed under the GNU Affero General Public License v3-or-later license.

using System.Collections.Immutable;
using Kawayi.CommandLine.Abstractions;

namespace Kawayi.CommandLine.Abstractions.Tests;

public class CliSchemaBuilderTests
{
    [Test]
    public async Task Build_Returns_Empty_Schema_For_Empty_Builder()
    {
        var builder = CreateBuilder();

        var schema = builder.Build();

        await Assert.That(schema.GeneratedFrom).IsNull();
        await Assert.That(schema.Argument).IsEmpty();
        await Assert.That(schema.Properties).IsEmpty();
        await Assert.That(schema.SubcommandDefinitions).IsEmpty();
        await Assert.That(schema.Subcommands).IsEmpty();
    }

    [Test]
    public async Task Build_Expands_Property_Canonical_Name_And_Aliases_To_Option_Tokens()
    {
        var builder = CreateBuilder();
        var property = CreateProperty(
            "format",
            longNames: ImmutableDictionary<string, NameWithVisibility>.Empty
                .Add("output", new NameWithVisibility("output", true)),
            shortNames: ImmutableDictionary<string, NameWithVisibility>.Empty
                .Add("f", new NameWithVisibility("f", true)));
        builder.Properties["format"] = property;

        var schema = builder.Build();

        await Assert.That(schema.Properties[new LongOptionToken("format")]).IsEqualTo(property);
        await Assert.That(schema.Properties[new LongOptionToken("output")]).IsEqualTo(property);
        await Assert.That(schema.Properties[new ShortOptionToken("f")]).IsEqualTo(property);
    }

    [Test]
    public async Task Build_Expands_Subcommand_Canonical_Name_And_Aliases_To_Command_Tokens()
    {
        var builder = CreateBuilder();
        var childBuilder = CreateBuilder();
        var childProperty = CreateProperty("force");
        var subcommand = CreateCommand(
            "serve",
            ImmutableDictionary<string, NameWithVisibility>.Empty
                .Add("srv", new NameWithVisibility("srv", true)));

        childBuilder.Properties["force"] = childProperty;
        builder.SubcommandDefinitions["serve"] = subcommand;
        builder.Subcommands["serve"] = childBuilder;

        var schema = builder.Build();

        await Assert.That(schema.SubcommandDefinitions[new ArgumentOrCommandToken("serve")]).IsEqualTo(subcommand);
        await Assert.That(schema.SubcommandDefinitions[new ArgumentOrCommandToken("srv")]).IsEqualTo(subcommand);
        await Assert.That(schema.Subcommands.ContainsKey(new ArgumentOrCommandToken("serve"))).IsTrue();
        await Assert.That(schema.Subcommands[new ArgumentOrCommandToken("serve")].Properties[new LongOptionToken("force")]).IsEqualTo(childProperty);
    }

    [Test]
    public async Task Build_Throws_When_Property_Key_Does_Not_Match_Definition_Name()
    {
        var builder = CreateBuilder();
        builder.Properties["wrong"] = CreateProperty("actual");

        await Assert.That(builder.Build).Throws<InvalidOperationException>();
    }

    [Test]
    public async Task Build_Throws_When_Subcommand_Definition_Key_Does_Not_Match_Definition_Name()
    {
        var builder = CreateBuilder();
        builder.SubcommandDefinitions["wrong"] = CreateCommand("actual");
        builder.Subcommands["wrong"] = CreateBuilder();

        await Assert.That(builder.Build).Throws<InvalidOperationException>();
    }

    [Test]
    public async Task Build_Throws_When_Subcommand_Definition_Has_No_Child_Builder()
    {
        var builder = CreateBuilder();
        builder.SubcommandDefinitions["serve"] = CreateCommand("serve");

        await Assert.That(builder.Build).Throws<InvalidOperationException>();
    }

    [Test]
    public async Task Build_Throws_When_Subcommand_Child_Builder_Has_No_Definition()
    {
        var builder = CreateBuilder();
        builder.Subcommands["serve"] = CreateBuilder();

        await Assert.That(builder.Build).Throws<InvalidOperationException>();
    }

    [Test]
    public async Task Build_Throws_When_Property_Token_Maps_To_Multiple_Definitions()
    {
        var builder = CreateBuilder();
        builder.Properties["first"] = CreateProperty(
            "first",
            longNames: ImmutableDictionary<string, NameWithVisibility>.Empty
                .Add("shared", new NameWithVisibility("shared", true)));
        builder.Properties["second"] = CreateProperty(
            "second",
            longNames: ImmutableDictionary<string, NameWithVisibility>.Empty
                .Add("shared", new NameWithVisibility("shared", true)));

        await Assert.That(builder.Build).Throws<InvalidOperationException>();
    }

    [Test]
    public async Task Build_Throws_When_Called_More_Than_Once()
    {
        var builder = CreateBuilder();
        _ = builder.Build();

        await Assert.That(builder.Build).Throws<InvalidOperationException>();
    }

    private static CliSchemaBuilder CreateBuilder() =>
        new(
            ImmutableDictionary.CreateBuilder<string, CommandDefinition>(),
            ImmutableDictionary.CreateBuilder<string, CliSchemaBuilder>(),
            ImmutableDictionary.CreateBuilder<string, PropertyDefinition>(),
            ImmutableList.CreateBuilder<ParameterDefinition>());

    private static CommandDefinition CreateCommand(
        string name,
        ImmutableDictionary<string, NameWithVisibility>? aliases = null) =>
        new(CreateInformation(name), aliases ?? ImmutableDictionary<string, NameWithVisibility>.Empty, null);

    private static PropertyDefinition CreateProperty(
        string name,
        ImmutableDictionary<string, NameWithVisibility>? longNames = null,
        ImmutableDictionary<string, NameWithVisibility>? shortNames = null) =>
        new(
            CreateInformation(name),
            longNames ?? ImmutableDictionary<string, NameWithVisibility>.Empty,
            shortNames ?? ImmutableDictionary<string, NameWithVisibility>.Empty,
            null,
            typeof(string),
            false);

    private static DefinitionInformation CreateInformation(string name) =>
        new(new NameWithVisibility(name, true), new Document($"{name} summary", $"{name} help"));
}
