// Copyright (c) 2026 MoeGodot<me@kawayi.moe>.
// Licensed under the GNU Affero General Public License v3-or-later license.

using System.Collections.Immutable;
using System.Globalization;
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
    public async Task Help_Flag_Writes_Unstyled_Output_When_Output_Style_Is_Disabled()
    {
        using var output = new StringWriter();
        var options = CreateOptions(output, enableStyledOutput: false);
        var input = CreateParameter("input", typeof(string), ValueRange.One);
        var verbose = CreateProperty("verbose", typeof(bool));
        var run = CreateCommand("run");
        var schema = CreateBuilder(arguments: [input], properties: [verbose], subcommands: [run]).Build();

        var result = CliSchemaParser.CreateParsing(options, [new LongOptionToken("help")], schema);
        var help = AssertHelp(result);

        help.FlagAction();
        var text = output.ToString();

        await Assert.That(text).Contains("test");
        await Assert.That(text).Contains("Usage");
        await Assert.That(text).Contains("Arguments");
        await Assert.That(text).Contains("Options");
        await Assert.That(text).Contains("Commands");
        await Assert.That(ContainsAnsi(text)).IsFalse();
    }

    [Test]
    public async Task Help_Flag_Writes_Styled_Output_When_Output_Style_Is_Enabled()
    {
        using var output = new StringWriter();
        var options = CreateOptions(output, enableStyledOutput: true);
        var input = CreateParameter("input", typeof(string), ValueRange.One);
        var schema = CreateBuilder(arguments: [input]).Build();

        var result = CliSchemaParser.CreateParsing(options, [new LongOptionToken("help")], schema);
        var help = AssertHelp(result);

        help.FlagAction();
        var text = output.ToString();

        await Assert.That(text).Contains("Usage");
        await Assert.That(text).Contains(StyleTable.Default.HelpTitleStyle.ToAnsiCode());
        await Assert.That(text).Contains(Style.ClearStyle);
        await Assert.That(ContainsAnsi(text)).IsTrue();
    }

    [Test]
    public async Task Version_Flag_Writes_Unstyled_Output_When_Output_Style_Is_Disabled()
    {
        using var output = new StringWriter();
        var options = CreateOptions(output, enableStyledOutput: false);
        var schema = CreateBuilder().Build();

        var result = CliSchemaParser.CreateParsing(options, [new LongOptionToken("version")], schema);
        var version = AssertVersion(result);

        version.FlagAction();
        var text = output.ToString();

        await Assert.That(text).Contains("test");
        await Assert.That(text).Contains("1.2.3");
        await Assert.That(ContainsAnsi(text)).IsFalse();
    }

    [Test]
    public async Task Version_Flag_Writes_Styled_Output_When_Output_Style_Is_Enabled()
    {
        using var output = new StringWriter();
        var options = CreateOptions(output, enableStyledOutput: true);
        var schema = CreateBuilder().Build();

        var result = CliSchemaParser.CreateParsing(options, [new LongOptionToken("version")], schema);
        var version = AssertVersion(result);

        version.FlagAction();
        var text = output.ToString();

        await Assert.That(text).Contains("1.2.3");
        await Assert.That(text).Contains(StyleTable.Default.ProgramNameStyle.ToAnsiCode());
        await Assert.That(text).Contains(StyleTable.Default.SecondaryTextStyle.ToAnsiCode());
        await Assert.That(text).Contains(Style.ClearStyle);
        await Assert.That(ContainsAnsi(text)).IsTrue();
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
    public async Task CreateParsing_Parses_Bool_Inline_Value_And_Does_Not_Consume_Next_Positional()
    {
        var input = CreateParameter("input", typeof(string), ValueRange.One);
        var verbose = CreateProperty("verbose", typeof(bool));
        var schema = CreateBuilder(arguments: [input], properties: [verbose]).Build();

        var result = CliSchemaParser.CreateParsing(
            CreateOptions(),
            [new LongOptionToken("verbose", "false"), new ArgumentOrCommandToken("payload")],
            schema);
        var command = AssertFinished(result);

        await Assert.That((bool)command.Properties[verbose]).IsFalse();
        await Assert.That(command.Arguments[input]).IsEqualTo("payload");
    }

    [Test]
    public async Task CreateParsing_Bool_Property_Does_Not_Consume_NonBoolean_Argument()
    {
        var verbose = CreateProperty("verbose", typeof(bool));
        var schema = CreateBuilder(properties: [verbose]).Build();

        var result = CliSchemaParser.CreateParsing(
            CreateOptions(),
            [new LongOptionToken("verbose"), new ArgumentOrCommandToken("maybe")],
            schema);

        await Assert.That(result).IsTypeOf<UnknownArgumentDetected>();
    }

    [Test]
    public async Task CreateParsing_Rejects_Invalid_Bool_Inline_Value()
    {
        var verbose = CreateProperty("verbose", typeof(bool));
        var schema = CreateBuilder(properties: [verbose]).Build();

        var result = CliSchemaParser.CreateParsing(
            CreateOptions(),
            [new LongOptionToken("verbose", "maybe")],
            schema);

        await Assert.That(result).IsTypeOf<InvalidArgumentDetected>();
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
    public async Task CreateParsing_Consumes_DashPrefixed_Decimal_Values_Using_Invariant_Culture()
    {
        var previousCulture = CultureInfo.CurrentCulture;
        var previousUiCulture = CultureInfo.CurrentUICulture;

        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("fr-FR");
            var threshold = CreateProperty("threshold", typeof(decimal));
            var schema = CreateBuilder(properties: [threshold]).Build();

            var result = CliSchemaParser.CreateParsing(
                CreateOptions(),
                [new LongOptionToken("threshold"), new ShortOptionToken("1", ".5")],
                schema);
            var command = AssertFinished(result);

            await Assert.That(command.Properties[threshold]).IsEqualTo(-1.5m);
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
            CultureInfo.CurrentUICulture = previousUiCulture;
        }
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
    public async Task CreateParsing_Parses_Empty_And_Short_Inline_String_Values()
    {
        var name = CreateProperty("name", typeof(string)) with
        {
            ShortName = ImmutableDictionary<string, NameWithVisibility>.Empty.Add("n", new NameWithVisibility("n", true))
        };
        var label = CreateProperty("label", typeof(string));
        var schema = CreateBuilder(properties: [name, label]).Build();

        var result = CliSchemaParser.CreateParsing(
            CreateOptions(),
            [new LongOptionToken("label", string.Empty), new ShortOptionToken("n", "alpha")],
            schema);
        var command = AssertFinished(result);

        await Assert.That(command.Properties[label]).IsEqualTo(string.Empty);
        await Assert.That(command.Properties[name]).IsEqualTo("alpha");
    }

    [Test]
    public async Task CreateParsing_Rejects_Repeated_Scalar_Property_Values()
    {
        var name = CreateProperty("name", typeof(string));
        var schema = CreateBuilder(properties: [name]).Build();

        var result = CliSchemaParser.CreateParsing(
            CreateOptions(),
            [
                new LongOptionToken("name"),
                new ArgumentOrCommandToken("alpha"),
                new LongOptionToken("name"),
                new ArgumentOrCommandToken("beta")
            ],
            schema);

        await Assert.That(result).IsTypeOf<InvalidArgumentDetected>();
    }

    [Test]
    public async Task CreateParsing_Does_Not_Consume_Unknown_Option_As_String_Property_Value()
    {
        var name = CreateProperty("name", typeof(string));
        var schema = CreateBuilder(properties: [name]).Build();

        var result = CliSchemaParser.CreateParsing(
            CreateOptions(),
            [new LongOptionToken("name"), new LongOptionToken("unknown")],
            schema);

        await Assert.That(result).IsTypeOf<UnknownArgumentDetected>();
    }

    [Test]
    public async Task CreateParsing_Consumes_Escaped_DashPrefixed_String_Property_Value()
    {
        var name = CreateProperty("name", typeof(string));
        var schema = CreateBuilder(properties: [name]).Build();

        var result = CliSchemaParser.CreateParsing(
            CreateOptions(),
            [new LongOptionToken("name"), new ArgumentToken("--literal")],
            schema);
        var command = AssertFinished(result);

        await Assert.That(command.Properties[name]).IsEqualTo("--literal");
    }

    [Test]
    public async Task CreateParsing_OptionTerminator_Stops_Property_Value_Capture()
    {
        var tag = CreateProperty("tag", typeof(ImmutableArray<string>));
        var schema = CreateBuilder(properties: [tag]).Build();

        var result = CliSchemaParser.CreateParsing(
            CreateOptions(),
            [new LongOptionToken("tag"), new ArgumentOrCommandToken("before"), new OptionTerminatorToken(), new ArgumentToken("after")],
            schema);
        var command = AssertFinished(result);
        var parsedTags = (ImmutableArray<string>)command.Properties[tag];

        await Assert.That(parsedTags).IsEquivalentTo(["before"]);
        await Assert.That(command.ToProgramArguments.Length).IsEqualTo(1);
        await Assert.That(command.ToProgramArguments[0]).IsEqualTo(new ArgumentToken("after"));
    }

    [Test]
    public async Task CreateParsing_Consumes_DashPrefixed_Numeric_Container_Property_Values()
    {
        var numbers = CreateProperty("number", typeof(ImmutableArray<int>)) with
        {
            NumArgs = ValueRange.OneOrMore
        };
        var mapping = CreateProperty("mapping", typeof(ImmutableDictionary<int, string>)) with
        {
            NumArgs = ValueRange.OneOrMore
        };
        var schema = CreateBuilder(properties: [numbers, mapping]).Build();

        var result = CliSchemaParser.CreateParsing(
            CreateOptions(),
            [
                new LongOptionToken("number"),
                new ShortOptionToken("1"),
                new ShortOptionToken("2"),
                new LongOptionToken("mapping"),
                new ShortOptionToken("1", "=one")
            ],
            schema);
        var command = AssertFinished(result);
        var parsedNumbers = (ImmutableArray<int>)command.Properties[numbers];
        var parsedMapping = (ImmutableDictionary<int, string>)command.Properties[mapping];

        await Assert.That(parsedNumbers).IsEquivalentTo([-1, -2]);
        await Assert.That(parsedMapping[-1]).IsEqualTo("one");
    }

    [Test]
    public async Task CreateParsing_Parses_Container_Property_Zero_One_And_Multiple_Values()
    {
        var tags = CreateProperty("tag", typeof(ImmutableArray<string>));
        var numbers = CreateProperty("number", typeof(ImmutableList<int>));
        var schema = CreateBuilder(properties: [tags, numbers]).Build();

        var result = CliSchemaParser.CreateParsing(
            CreateOptions(),
            [
                new LongOptionToken("tag"),
                new LongOptionToken("number"),
                new ArgumentOrCommandToken("1"),
                new LongOptionToken("number"),
                new ArgumentOrCommandToken("2"),
                new ArgumentOrCommandToken("3")
            ],
            schema);
        var command = AssertFinished(result);
        var parsedTags = (ImmutableArray<string>)command.Properties[tags];
        var parsedNumbers = (ImmutableList<int>)command.Properties[numbers];

        await Assert.That(parsedTags).IsEmpty();
        await Assert.That(parsedNumbers).IsEquivalentTo([1, 2, 3]);
    }

    [Test]
    public async Task CreateParsing_Parses_Repeated_Dictionary_Property_Values()
    {
        var env = CreateProperty("env", typeof(ImmutableDictionary<string, int>));
        var schema = CreateBuilder(properties: [env]).Build();

        var result = CliSchemaParser.CreateParsing(
            CreateOptions(),
            [
                new LongOptionToken("env"),
                new ArgumentOrCommandToken("alpha=1"),
                new LongOptionToken("env"),
                new ArgumentOrCommandToken("beta=2"),
                new ArgumentOrCommandToken("alpha=3")
            ],
            schema);
        var command = AssertFinished(result);
        var parsedEnv = (ImmutableDictionary<string, int>)command.Properties[env];

        await Assert.That(parsedEnv.Count).IsEqualTo(2);
        await Assert.That(parsedEnv["alpha"]).IsEqualTo(3);
        await Assert.That(parsedEnv["beta"]).IsEqualTo(2);
    }

    [Test]
    public async Task CreateParsing_Parses_Common_And_Enum_Property_Types()
    {
        var id = CreateProperty("id", typeof(Guid));
        var endpoint = CreateProperty("endpoint", typeof(Uri));
        var date = CreateProperty("date", typeof(DateOnly));
        var time = CreateProperty("time", typeof(TimeOnly));
        var mode = CreateProperty("mode", typeof(ParserTestMode));
        var schema = CreateBuilder(properties: [id, endpoint, date, time, mode]).Build();
        var guid = Guid.Parse("f4602e26-b7b4-4735-b931-d58d8f6f74c2");

        var result = CliSchemaParser.CreateParsing(
            CreateOptions(),
            [
                new LongOptionToken("id"),
                new ArgumentOrCommandToken(guid.ToString()),
                new LongOptionToken("endpoint"),
                new ArgumentOrCommandToken("https://example.com/api"),
                new LongOptionToken("date"),
                new ArgumentOrCommandToken("2026-05-08"),
                new LongOptionToken("time"),
                new ArgumentOrCommandToken("12:34:56"),
                new LongOptionToken("mode"),
                new ArgumentOrCommandToken("advanced")
            ],
            schema);
        var command = AssertFinished(result);

        await Assert.That(command.Properties[id]).IsEqualTo(guid);
        await Assert.That(command.Properties[endpoint]).IsEqualTo(new Uri("https://example.com/api"));
        await Assert.That(command.Properties[date]).IsEqualTo(new DateOnly(2026, 5, 8));
        await Assert.That(command.Properties[time]).IsEqualTo(new TimeOnly(12, 34, 56));
        await Assert.That(command.Properties[mode]).IsEqualTo(ParserTestMode.Advanced);
    }

    [Test]
    public async Task CreateParsing_Reports_Subcommand_Scope_Help_And_Version_Flags()
    {
        using var output = new StringWriter();
        var options = CreateOptions(output);
        var run = CreateCommand("run");
        var schema = CreateBuilder(subcommands: [run]).Build();

        var helpResult = CliSchemaParser.CreateParsing(
            options,
            [new ArgumentOrCommandToken("run"), new LongOptionToken("help")],
            schema);
        var versionResult = CliSchemaParser.CreateParsing(
            options,
            [new ArgumentOrCommandToken("run"), new LongOptionToken("version")],
            schema);
        var helpSubcommand = AssertSubcommand(helpResult, "run");
        var versionSubcommand = AssertSubcommand(versionResult, "run");

        await Assert.That(helpSubcommand.ContinueParseAction()).IsTypeOf<HelpFlagsDetected>();
        await Assert.That(versionSubcommand.ContinueParseAction()).IsTypeOf<VersionFlagsDetected>();
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

    [Test]
    public async Task CreateParsing_Writes_Unstyled_Debug_Output_When_Debug_Style_Is_Disabled()
    {
        using var debugOutput = new StringWriter();
        var options = CreateOptions(debugOutput: debugOutput, debug: true, enableStyledDebugOutput: false);
        var input = CreateParameter("input", typeof(string), ValueRange.One);
        var schema = CreateBuilder(arguments: [input]).Build();

        var result = CliSchemaParser.CreateParsing(options, [new ArgumentOrCommandToken("payload")], schema);

        AssertFinished(result);
        var text = debugOutput.ToString();

        await Assert.That(text).Contains("Debug Parse Trace");
        await Assert.That(text).Contains("Debug Parse Result");
        await Assert.That(text).Contains("State: success");
        await Assert.That(text).Contains("payload");
        await Assert.That(ContainsAnsi(text)).IsFalse();
    }

    [Test]
    public async Task CreateParsing_Writes_Styled_Debug_Output_When_Debug_Style_Is_Enabled()
    {
        using var debugOutput = new StringWriter();
        var options = CreateOptions(debugOutput: debugOutput, debug: true, enableStyledDebugOutput: true);
        var input = CreateParameter("input", typeof(string), ValueRange.One);
        var schema = CreateBuilder(arguments: [input]).Build();

        var result = CliSchemaParser.CreateParsing(options, [new ArgumentOrCommandToken("payload")], schema);

        AssertFinished(result);
        var text = debugOutput.ToString();

        await Assert.That(text).Contains("Debug Parse Trace");
        await Assert.That(text).Contains("Debug Parse Result");
        await Assert.That(text).Contains(StyleTable.Default.DebugTitleStyle.ToAnsiCode());
        await Assert.That(text).Contains(StyleTable.Default.DebugSuccessStyle.ToAnsiCode());
        await Assert.That(text).Contains(Style.ClearStyle);
        await Assert.That(ContainsAnsi(text)).IsTrue();
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

    private enum ParserTestMode
    {
        Basic,
        Advanced
    }

    private static ParsingOptions CreateOptions(
        TextWriter? output = null,
        TextWriter? debugOutput = null,
        bool debug = false,
        bool enableStyledOutput = false,
        bool enableStyledDebugOutput = false)
    {
        return new ParsingOptions(
            new ProgramInformation("test", new Document("summary", "help"), new Version(1, 2, 3), "https://example.com"),
            ParsingOptions.DefaultVersionFlags,
            ParsingOptions.DefaultHelpFlags,
            output ?? TextWriter.Null,
            debugOutput ?? TextWriter.Null,
            enableStyledDebugOutput,
            enableStyledOutput,
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

    private static HelpFlagsDetected AssertHelp(ParsingResult result)
    {
        return result is HelpFlagsDetected help
            ? help
            : throw new InvalidOperationException($"Expected {nameof(HelpFlagsDetected)}, got {result.GetType().FullName}.");
    }

    private static VersionFlagsDetected AssertVersion(ParsingResult result)
    {
        return result is VersionFlagsDetected version
            ? version
            : throw new InvalidOperationException($"Expected {nameof(VersionFlagsDetected)}, got {result.GetType().FullName}.");
    }

    private static bool ContainsAnsi(string text)
    {
        return text.Contains("\u001b[", StringComparison.Ordinal);
    }
}
