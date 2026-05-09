// Copyright (c) 2026 GodotAsync<me@kawayi.moe>.
// Licensed under the GNU Affero General Public License v3-or-later license.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Kawayi.CommandLine.Abstractions;
using Kawayi.CommandLine.Core;
using Kawayi.CommandLine.Core.Attributes;
using Kawayi.CommandLine.Extensions;
using CliProperty = Kawayi.CommandLine.Core.Attributes.PropertyAttribute;

namespace Kawayi.CommandLine.Core.Tests;

public sealed class ReflectionBindingTests
{
    [Test]
    public async Task GenerateFor_Reflects_Command_Metadata_Including_Inheritance_And_Promoted_Global_Subcommands()
    {
        var genericSchema = CliSchemaGenerator.GenerateFor<ReflectionDerivedCommand>();
        var runtimeSchema = CliSchemaGenerator.GenerateFor(typeof(ReflectionDerivedCommand));

        await Assert.That(genericSchema.GeneratedFrom).IsEqualTo(typeof(ReflectionDerivedCommand));
        await Assert.That(runtimeSchema.GeneratedFrom).IsEqualTo(typeof(ReflectionDerivedCommand));
        await Assert.That(genericSchema.Argument[0].Information.Name.Value).IsEqualTo("input-date");
        await Assert.That(genericSchema.Argument[0].Format).IsEqualTo("yyyyMMdd");
        await Assert.That(genericSchema.Argument[0].Information.Document.ConciseDescription).IsEqualTo("Input date summary");
        await Assert.That(genericSchema.Properties[new LongOptionToken("mode")].ValueName).IsEqualTo("mode");
        await Assert.That(genericSchema.Properties[new ShortOptionToken("m")].Information.Name.Value).IsEqualTo("mode");
        await Assert.That(genericSchema.Properties[new LongOptionToken("tag")].Validation).IsNotNull();
        await Assert.That(genericSchema.Properties[new LongOptionToken("mode")].PossibleValues).IsTypeOf<CountablePossibleValues<string>>();
        await Assert.That(genericSchema.Properties.ContainsKey(new LongOptionToken("base-option"))).IsTrue();
        await Assert.That(genericSchema.Properties.ContainsKey(new LongOptionToken("force"))).IsTrue();
        await Assert.That(genericSchema.SubcommandDefinitions.ContainsKey(new ArgumentOrCommandToken("child"))).IsTrue();
        await Assert.That(genericSchema.SubcommandDefinitions.ContainsKey(new ArgumentOrCommandToken("run"))).IsTrue();
        await Assert.That(genericSchema.SubcommandDefinitions.ContainsKey(new ArgumentOrCommandToken("global"))).IsFalse();
    }

    [Test]
    public async Task GenerateFor_Uses_Empty_Documents_When_No_Document_Exporter_Is_Present()
    {
        var schema = CliSchemaGenerator.GenerateFor<PlainReflectionCommand>();

        await Assert.That(schema.Properties[new LongOptionToken("count")].Information.Document.ConciseDescription).IsEqualTo(string.Empty);
        await Assert.That(schema.Properties[new LongOptionToken("count")].Information.Document.HelpText).IsEqualTo(string.Empty);
    }

    [Test]
    public async Task GenerateFor_Uses_Static_Symbols_Fallback_When_Command_Attribute_Is_Absent()
    {
        var schema = CliSchemaGenerator.GenerateFor<SymbolOnlyCommand>();

        await Assert.That(schema.GeneratedFrom).IsEqualTo(typeof(SymbolOnlyCommand));
        await Assert.That(schema.Argument.Count).IsEqualTo(1);
        await Assert.That(schema.Argument[0].Information.Name.Value).IsEqualTo("name");
        await Assert.That(schema.Argument[0].Information.Document.ConciseDescription).IsEqualTo("Name summary");
    }

    [Test]
    public async Task Binder_Binds_Reflection_Generated_Cli_And_Preserves_Initializers()
    {
        var schema = CliSchemaGenerator.GenerateFor<BindingRootCommand>();
        var cli = AssertFinished(schema.Parse(Tokenize("payload", "--force", "--base-option", "7"), CreateOptions()));

        var command = Binder.Bind(new BindingRootCommand(), cli, new BindingOptions());

        await Assert.That(command.Input).IsEqualTo("payload");
        await Assert.That(command.Count).IsEqualTo(41);
        await Assert.That(command.BaseOption).IsEqualTo(7);
        await Assert.That(command.Global).IsNotNull();
        await Assert.That(command.Global.Force).IsTrue();
        await Assert.That(command.Child).IsNull();
    }

    [Test]
    public async Task Binder_Binds_Regular_Subcommands_And_Respects_CheckGeneratedType()
    {
        var schema = CliSchemaGenerator.GenerateFor<BindingRootCommand>();
        var cli = AssertFinished(schema.Parse(Tokenize("payload", "--base-option", "9", "child", "leaf"), CreateOptions()));

        var command = Binder.Bind(new BindingRootCommand(), cli, new BindingOptions());

        await Assert.That(command.Child).IsNotNull();
        await Assert.That(command.Child!.Name).IsEqualTo("leaf");

        await Assert.That(() => Binder.Bind(new BindingRootCommand(), typeof(BindingBaseCommand), cli, new BindingOptions()))
            .Throws<ArgumentException>();

        var baseBinding = new BindingRootCommand();
        Binder.Bind(baseBinding, typeof(BindingBaseCommand), cli, new BindingOptions(CheckGeneratedType: false));
        await Assert.That(baseBinding.BaseOption).IsEqualTo(9);
    }

    [Test]
    public async Task GenerateFor_Throws_When_Subcommand_Type_Misses_Dynamically_Accessed_Members_Contract()
    {
        await Assert.That(() => CliSchemaGenerator.GenerateFor<MissingContractRootCommand>())
            .Throws<InvalidOperationException>()
            .WithMessageContaining(nameof(MissingContractChildCommand));
    }

    [Test]
    public async Task Binder_Throws_When_Subcommand_Type_Misses_Dynamically_Accessed_Members_Contract()
    {
        var schema = CreateBindingSchemaWithSubcommand("child");
        var cli = AssertFinished(schema.Parse(Tokenize("child"), CreateOptions()));

        await Assert.That(() => Binder.Bind(new MissingContractRootCommand(), cli, new BindingOptions()))
            .Throws<InvalidOperationException>()
            .WithMessageContaining(nameof(MissingContractChildCommand));
    }

    private static ImmutableArray<Token> Tokenize(params string[] arguments)
    {
        return Tokenizer.Instance.Tokenize([.. arguments]);
    }

    private static ParsingOptions CreateOptions()
    {
        return new ParsingOptions(
            new ProgramInformation("reflection", new Document("summary", "help"), new Version(1, 0, 0), "https://example.com"),
            ParsingOptions.DefaultVersionFlags,
            ParsingOptions.DefaultHelpFlags,
            TextWriter.Null,
            TextWriter.Null,
            false,
            false,
            false,
            StyleTable.Default,
            TypeProviders.Empty);
    }

    private static Cli AssertFinished(ParsingResult result)
    {
        return result is ParsingFinished { UntypedResult: Cli cli }
            ? cli
            : throw new InvalidOperationException($"Expected {nameof(ParsingFinished)}, got {result.GetType().FullName}.");
    }

    private static CliSchema CreateBindingSchemaWithSubcommand(string name)
    {
        var childBuilder = new CliSchemaBuilder(
            ImmutableDictionary.CreateBuilder<string, CommandDefinition>(),
            ImmutableDictionary.CreateBuilder<string, CliSchemaBuilder>(),
            ImmutableDictionary.CreateBuilder<string, PropertyDefinition>(),
            ImmutableList.CreateBuilder<ParameterDefinition>());

        var builder = new CliSchemaBuilder(
            ImmutableDictionary.CreateBuilder<string, CommandDefinition>(),
            ImmutableDictionary.CreateBuilder<string, CliSchemaBuilder>(),
            ImmutableDictionary.CreateBuilder<string, PropertyDefinition>(),
            ImmutableList.CreateBuilder<ParameterDefinition>());

        var definition = new CommandDefinition(
            new DefinitionInformation(new NameWithVisibility(name, true), new Document($"{name} summary", $"{name} help")),
            ImmutableDictionary<string, NameWithVisibility>.Empty,
            null);

        builder.SubcommandDefinitions[name] = definition;
        builder.Subcommands[name] = childBuilder;
        return builder.Build();
    }
}

public enum ReflectionMode
{
    Basic = 0,
    Advanced = 1
}

[Command]
public class ReflectionBaseCommand
{
    [CliProperty]
    [LongAlias("base-option")]
    public int BaseOption { get; protected set; } = -1;
}

[Command]
public class ReflectionDerivedCommand : ReflectionBaseCommand, IDocumentExporter
{
    public static ImmutableDictionary<string, Document> Documents { get; } =
        ImmutableDictionary<string, Document>.Empty
            .Add(nameof(InputDate), new Document("Input date summary", "Input date help"))
            .Add(nameof(Mode), new Document("Mode summary", "Mode help"))
            .Add(nameof(Child), new Document("Child summary", "Child help"));

    [Argument(0, require: true, format: "yyyyMMdd")]
    [ValueRange(1, 1)]
    public DateOnly InputDate { get; private set; }

    [CliProperty(valueName: "mode")]
    [LongAlias("mode")]
    [ShortAlias("m")]
    public ReflectionMode Mode { get; private set; }

    [CliProperty]
    [LongAlias("tag")]
    [Validator(nameof(ValidateTag))]
    public string? Tag { get; private set; }

    [Subcommand(global: true)]
    public ReflectionGlobalCommand Global { get; private set; } = new();

    [Subcommand]
    [Alias("run")]
    public ReflectionChildCommand? Child { get; private set; }

    public static string? ValidateTag(string? value)
    {
        return value is "bad" ? "bad tag" : null;
    }
}

[Command]
[DynamicallyAccessedMembers(
    DynamicallyAccessedMemberTypes.Interfaces |
    DynamicallyAccessedMemberTypes.PublicProperties |
    DynamicallyAccessedMemberTypes.NonPublicProperties |
    DynamicallyAccessedMemberTypes.PublicMethods |
    DynamicallyAccessedMemberTypes.NonPublicMethods |
    DynamicallyAccessedMemberTypes.PublicConstructors)]
public class ReflectionGlobalCommand
{
    [CliProperty]
    [LongAlias("force")]
    public bool Force { get; set; }
}

[Command]
[DynamicallyAccessedMembers(
    DynamicallyAccessedMemberTypes.Interfaces |
    DynamicallyAccessedMemberTypes.PublicProperties |
    DynamicallyAccessedMemberTypes.NonPublicProperties |
    DynamicallyAccessedMemberTypes.PublicMethods |
    DynamicallyAccessedMemberTypes.NonPublicMethods |
    DynamicallyAccessedMemberTypes.PublicConstructors)]
public class ReflectionChildCommand
{
    [Argument(0, require: true)]
    [ValueRange(1, 1)]
    public string Name { get; set; } = string.Empty;
}

[Command]
public class PlainReflectionCommand
{
    [CliProperty]
    [LongAlias("count")]
    public int Count { get; set; }
}

public class SymbolOnlyCommand : ISymbolExporter
{
    public static ImmutableArray<Symbol> Symbols { get; } =
        ImmutableArray.Create<Symbol>(
            new ParameterDefinition(
                new DefinitionInformation(
                    new NameWithVisibility("name", true),
                    new Document("Name summary", "Name help")),
                null,
                ValueRange.One,
                typeof(string),
                true));
}

[Command]
public class BindingBaseCommand
{
    [CliProperty]
    [LongAlias("base-option")]
    public int BaseOption { get; protected set; } = -1;
}

[Command]
public class BindingRootCommand : BindingBaseCommand
{
    [Argument(0, require: true)]
    [ValueRange(1, 1)]
    public string Input { get; private set; } = string.Empty;

    [CliProperty]
    [LongAlias("count")]
    public int Count { get; private set; } = 41;

    [Subcommand(global: true)]
    public BindingGlobalCommand Global { get; private set; } = new();

    [Subcommand]
    public BindingChildCommand? Child { get; private set; } = new();
}

[Command]
[DynamicallyAccessedMembers(
    DynamicallyAccessedMemberTypes.Interfaces |
    DynamicallyAccessedMemberTypes.PublicProperties |
    DynamicallyAccessedMemberTypes.NonPublicProperties |
    DynamicallyAccessedMemberTypes.PublicMethods |
    DynamicallyAccessedMemberTypes.NonPublicMethods |
    DynamicallyAccessedMemberTypes.PublicConstructors)]
public class BindingGlobalCommand
{
    [CliProperty]
    [LongAlias("force")]
    public bool Force { get; private set; }
}

[Command]
[DynamicallyAccessedMembers(
    DynamicallyAccessedMemberTypes.Interfaces |
    DynamicallyAccessedMemberTypes.PublicProperties |
    DynamicallyAccessedMemberTypes.NonPublicProperties |
    DynamicallyAccessedMemberTypes.PublicMethods |
    DynamicallyAccessedMemberTypes.NonPublicMethods |
    DynamicallyAccessedMemberTypes.PublicConstructors)]
public class BindingChildCommand
{
    public BindingChildCommand()
    {
        Name = "seed";
    }

    [Argument(0, require: true)]
    [ValueRange(1, 1)]
    public string Name { get; protected set; } = string.Empty;
}

[Command]
public class MissingContractRootCommand
{
    [Subcommand]
    public MissingContractChildCommand? Child { get; private set; }
}

[Command]
public class MissingContractChildCommand
{
}
