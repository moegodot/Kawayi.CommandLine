// Copyright (c) 2026 MoeGodot<me@kawayi.moe>.
// Licensed under the GNU Affero General Public License v3-or-later license.

using System.Collections.Immutable;
using Kawayi.CommandLine.Abstractions;

namespace Kawayi.CommandLine.Core.Tests;

public sealed class CliSchemaParserTests
{
    [Test]
    public async Task CreateParsing_Detects_Version_And_Help_Flags()
    {
        using var output = new StringWriter();
        var options = CreateOptions(output);
        var schema = CreateBuilder().Build();

        var versionResult = CliSchemaParser.CreateParsing(options, [new LongOptionToken("version")], schema);
        var helpResult = CliSchemaParser.CreateParsing(options, [new LongOptionToken("help")], schema);

        if (versionResult is not VersionFlagsDetected version)
        {
            throw new InvalidOperationException($"Expected {nameof(VersionFlagsDetected)}, got {versionResult.GetType().FullName}.");
        }

        if (helpResult is not HelpFlagsDetected help)
        {
            throw new InvalidOperationException($"Expected {nameof(HelpFlagsDetected)}, got {helpResult.GetType().FullName}.");
        }

        version.FlagAction();
        help.FlagAction();

        await Assert.That(output.ToString()).Contains("test");
        await Assert.That(output.ToString()).Contains("1.2.3");
        await Assert.That(output.ToString()).Contains("Usage");
    }

    [Test]
    public async Task CreateParsing_Parses_Bool_Property_As_Implicit_True()
    {
        var verbose = CreateProperty("verbose", typeof(bool));
        var schema = CreateBuilder(properties: [verbose]).Build();

        var result = CliSchemaParser.CreateParsing(CreateOptions(), [new LongOptionToken("verbose")], schema);
        var command = AssertFinished(result);

        await Assert.That((bool)command.Properties[verbose]).IsTrue();
    }

    [Test]
    public async Task CreateParsing_Parses_Bool_Property_Explicit_False()
    {
        var verbose = CreateProperty("verbose", typeof(bool));
        var schema = CreateBuilder(properties: [verbose]).Build();

        var result = CliSchemaParser.CreateParsing(
            CreateOptions(),
            [new LongOptionToken("verbose"), new ArgumentOrCommandToken("false")],
            schema);
        var command = AssertFinished(result);

        await Assert.That((bool)command.Properties[verbose]).IsFalse();
    }

    [Test]
    public async Task CreateParsing_Does_Not_Consume_Subcommand_As_Bool_Value()
    {
        var verbose = CreateProperty("verbose", typeof(bool));
        var subcommand = CreateCommand("run");
        var schema = CreateBuilder(properties: [verbose], subcommands: [subcommand]).Build();

        var result = CliSchemaParser.CreateParsing(
            CreateOptions(),
            [new LongOptionToken("verbose"), new ArgumentOrCommandToken("run")],
            schema);
        var deferred = AssertSubcommand(result, "run");
        var parent = AssertFinished(deferred.ParentCommand);

        await Assert.That((bool)parent.Properties[verbose]).IsTrue();
    }

    [Test]
    public async Task CreateParsing_Prefers_Subcommand_Over_Positional_Argument()
    {
        var input = CreateParameter("input", typeof(string), ValueRange.ZeroOrOne);
        var subcommand = CreateCommand("serve");
        var schema = CreateBuilder(arguments: [input], subcommands: [subcommand]).Build();

        var result = CliSchemaParser.CreateParsing(CreateOptions(), [new ArgumentOrCommandToken("serve")], schema);

        AssertSubcommand(result, "serve");
        await Assert.That(result.GetType()).IsEqualTo(typeof(Subcommand));
    }

    [Test]
    public async Task CreateParsing_Assigns_Positional_Arguments_Greedily_While_Reserving_Later_Minimums()
    {
        var extras = CreateParameter("extras", typeof(ImmutableArray<string>), ValueRange.ZeroOrMore);
        var tail = CreateParameter("tail", typeof(string), ValueRange.One);
        var schema = CreateBuilder(arguments: [extras, tail]).Build();

        var result = CliSchemaParser.CreateParsing(
            CreateOptions(),
            [new ArgumentOrCommandToken("a"), new ArgumentOrCommandToken("b"), new ArgumentOrCommandToken("c")],
            schema);
        var command = AssertFinished(result);
        var parsedExtras = (ImmutableArray<string>)command.Arguments[extras];

        await Assert.That(parsedExtras).IsEquivalentTo(["a", "b"]);
        await Assert.That(command.Arguments[tail]).IsEqualTo("c");
    }

    [Test]
    public async Task CreateParsing_Uses_ContainerParser_For_Container_Properties()
    {
        var tags = CreateProperty("tag", typeof(ImmutableArray<int>)) with
        {
            NumArgs = ValueRange.OneOrMore
        };
        var schema = CreateBuilder(properties: [tags]).Build();

        var result = CliSchemaParser.CreateParsing(
            CreateOptions(),
            [new LongOptionToken("tag"), new ArgumentOrCommandToken("1"), new ArgumentOrCommandToken("2")],
            schema);
        var command = AssertFinished(result);
        var parsedTags = (ImmutableArray<int>)command.Properties[tags];

        await Assert.That(parsedTags).IsEquivalentTo([1, 2]);
    }

    [Test]
    public async Task CreateParsing_Applies_Defaults_And_Validation()
    {
        var retries = CreateProperty("retries", typeof(int)) with
        {
            DefaultValueFactory = static () => 3,
            Validation = static value => (int)value <= 0 ? "retries must be greater than zero." : null
        };
        var schema = CreateBuilder(properties: [retries]).Build();

        var defaultResult = CliSchemaParser.CreateParsing(CreateOptions(), [], schema);
        var invalidResult = CliSchemaParser.CreateParsing(
            CreateOptions(),
            [new LongOptionToken("retries"), new ArgumentOrCommandToken("-1")],
            schema);
        var command = AssertFinished(defaultResult);

        await Assert.That(command.Properties[retries]).IsEqualTo(3);
        await Assert.That(invalidResult).IsTypeOf<FailedValidation>();
    }

    [Test]
    public async Task CreateParsing_Rejects_Missing_Scalar_Property_Value()
    {
        var threshold = CreateProperty("threshold", typeof(decimal));
        var schema = CreateBuilder(properties: [threshold]).Build();

        var result = CliSchemaParser.CreateParsing(CreateOptions(), [new LongOptionToken("threshold")], schema);

        await Assert.That(result).IsTypeOf<InvalidArgumentDetected>();
    }

    [Test]
    public async Task CreateParsing_Omits_Absent_Optional_Property_Without_DefaultFactory()
    {
        var endpoint = CreateProperty("endpoint", typeof(Uri));
        var schema = CreateBuilder(properties: [endpoint]).Build();

        var result = CliSchemaParser.CreateParsing(CreateOptions(), [], schema);
        var command = AssertFinished(result);

        await Assert.That(command.Properties.ContainsKey(endpoint)).IsFalse();
    }

    [Test]
    public async Task CreateParsing_Stores_Tokens_After_OptionTerminator_As_ToProgramArguments()
    {
        var input = CreateParameter("input", typeof(string), ValueRange.One);
        var schema = CreateBuilder(arguments: [input]).Build();

        var result = CliSchemaParser.CreateParsing(
            CreateOptions(),
            [new ArgumentOrCommandToken("payload"), new OptionTerminatorToken(), new ArgumentToken("--child"), new ArgumentToken("-x")],
            schema);
        var command = AssertFinished(result);

        await Assert.That(command.Arguments[input]).IsEqualTo("payload");
        await Assert.That(command.ToProgramArguments.Length).IsEqualTo(2);
        await Assert.That(command.ToProgramArguments[0]).IsEqualTo(new ArgumentToken("--child"));
        await Assert.That(command.ToProgramArguments[1]).IsEqualTo(new ArgumentToken("-x"));
    }

    [Test]
    public async Task CreateParsing_Does_Not_Match_ArgumentToken_As_Subcommand()
    {
        var input = CreateParameter("input", typeof(string), ValueRange.One);
        var subcommand = CreateCommand("serve");
        var schema = CreateBuilder(arguments: [input], subcommands: [subcommand]).Build();

        var result = CliSchemaParser.CreateParsing(CreateOptions(), [new ArgumentToken("serve")], schema);
        var command = AssertFinished(result);

        await Assert.That(command.Arguments[input]).IsEqualTo("serve");
    }

    [Test]
    public async Task CreateParsing_Parses_DashPrefixed_Numeric_Arguments_And_Properties()
    {
        var count = CreateParameter("count", typeof(int), ValueRange.One);
        var threshold = CreateProperty("threshold", typeof(decimal));
        var schema = CreateBuilder(arguments: [count], properties: [threshold]).Build();

        var result = CliSchemaParser.CreateParsing(
            CreateOptions(),
            [new ShortOptionToken("1"), new LongOptionToken("threshold"), new ShortOptionToken("1", ".5")],
            schema);
        var command = AssertFinished(result);

        await Assert.That(command.Arguments[count]).IsEqualTo(-1);
        await Assert.That(command.Properties[threshold]).IsEqualTo(-1.5m);
    }

    [Test]
    public async Task CreateParsing_Does_Not_Consume_Known_Option_As_Numeric_Value()
    {
        var threshold = CreateProperty("threshold", typeof(decimal));
        var verbose = CreateProperty("verbose", typeof(bool)) with
        {
            ShortName = ImmutableDictionary<string, NameWithVisibility>.Empty.Add("v", new NameWithVisibility("v", true))
        };
        var schema = CreateBuilder(properties: [threshold, verbose]).Build();

        var result = CliSchemaParser.CreateParsing(
            CreateOptions(),
            [new LongOptionToken("threshold"), new ShortOptionToken("v")],
            schema);

        await Assert.That(result).IsTypeOf<InvalidArgumentDetected>();
    }

    [Test]
    public async Task CreateParsing_InlineValue_Does_Not_Consume_Next_Token()
    {
        var input = CreateParameter("input", typeof(string), ValueRange.One);
        var name = CreateProperty("name", typeof(string));
        var schema = CreateBuilder(arguments: [input], properties: [name]).Build();

        var result = CliSchemaParser.CreateParsing(
            CreateOptions(),
            [new LongOptionToken("name", "alpha"), new ArgumentOrCommandToken("payload")],
            schema);
        var command = AssertFinished(result);

        await Assert.That(command.Properties[name]).IsEqualTo("alpha");
        await Assert.That(command.Arguments[input]).IsEqualTo("payload");
    }

    [Test]
    public async Task CreateParsing_Writes_Debug_Trace_When_Debug_Is_Enabled()
    {
        using var debugOutput = new StringWriter();
        var options = CreateOptions(debugOutput: debugOutput, debug: true);
        var input = CreateParameter("input", typeof(string), ValueRange.One);
        var schema = CreateBuilder(arguments: [input]).Build();

        var result = CliSchemaParser.CreateParsing(options, [new ArgumentOrCommandToken("payload")], schema);

        AssertFinished(result);
        await Assert.That(debugOutput.ToString()).Contains("Debug Parse Trace");
        await Assert.That(debugOutput.ToString()).Contains("Complete scope");
        await Assert.That(debugOutput.ToString()).Contains("payload");
    }

    private static CliSchemaBuilder CreateBuilder(
        ImmutableArray<ParameterDefinition> arguments = default,
        ImmutableArray<PropertyDefinition> properties = default,
        ImmutableArray<CommandDefinition> subcommands = default)
    {
        var builder = new CliSchemaBuilder(
            ImmutableDictionary.CreateBuilder<string, CommandDefinition>(),
            ImmutableDictionary.CreateBuilder<string, CliSchemaBuilder>(),
            ImmutableDictionary.CreateBuilder<string, PropertyDefinition>(),
            ImmutableList.CreateBuilder<ParameterDefinition>());

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
            foreach (var subcommand in subcommands)
            {
                builder.SubcommandDefinitions[subcommand.Information.Name.Value] = subcommand;
                builder.Subcommands[subcommand.Information.Name.Value] = CreateBuilder();
            }
        }

        return builder;
    }

    private static ParameterDefinition CreateParameter(string name, Type type, ValueRange range)
    {
        return new(CreateInformation(name), null, range, type, range.Minimum > 0);
    }

    private static PropertyDefinition CreateProperty(string name, Type type)
    {
        return new(CreateInformation(name),
                   ImmutableDictionary<string, NameWithVisibility>.Empty,
                   ImmutableDictionary<string, NameWithVisibility>.Empty,
                   null,
                   type,
                   false);
    }

    private static CommandDefinition CreateCommand(string name)
    {
        return new(CreateInformation(name), ImmutableDictionary<string, NameWithVisibility>.Empty, null);
    }

    private static DefinitionInformation CreateInformation(string name)
    {
        return new(new NameWithVisibility(name, true), new Document($"{name} summary", $"{name} help"));
    }

    private static ParsingOptions CreateOptions(
        TextWriter? output = null,
        TextWriter? debugOutput = null,
        bool debug = false)
    {
        return new ParsingOptions(
            new ProgramInformation("test", new Document("summary", "help"), new Version(1, 2, 3), "https://example.com"),
            ParsingOptions.DefaultVersionFlags,
            ParsingOptions.DefaultHelpFlags,
            output ?? TextWriter.Null,
            debugOutput ?? TextWriter.Null,
            false,
            false,
            debug,
            StyleTable.Default);
    }

    private static Cli AssertFinished(ParsingResult result)
    {
        return result is ParsingFinished { UntypedResult: Cli command }
            ? command
            : throw new InvalidOperationException($"Expected {nameof(ParsingFinished)}, got {result.GetType().FullName}.");
    }

    private static Subcommand AssertSubcommand(ParsingResult result, string name)
    {
        if (result is not Subcommand subcommand)
        {
            throw new InvalidOperationException($"Expected {nameof(Subcommand)}, got {result.GetType().FullName}.");
        }

        if (!string.Equals(subcommand.Definition.Information.Name.Value, name, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Expected subcommand '{name}', got '{subcommand.Definition.Information.Name.Value}'.");
        }

        return subcommand;
    }
}
