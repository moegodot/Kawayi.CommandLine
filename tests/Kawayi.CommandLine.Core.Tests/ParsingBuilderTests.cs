// Copyright (c) 2026 MoeGodot<me@kawayi.moe>.
// Licensed under the GNU Affero General Public License v3-or-later license.

using System.Collections.Immutable;
using System.IO;
using System.Reflection;
using Kawayi.CommandLine.Abstractions;

namespace Kawayi.CommandLine.Core.Tests;

public sealed class ParsingBuilderTests
{
    private static readonly ParsingOptions DefaultOptions = CreateOptions();

    [Test]
    public async Task CreateParsing_Parses_Bool_Scalar_And_Container_Options()
    {
        var verbose = CreateProperty("verbose", typeof(bool), shortAliases: ["v"]);
        var count = CreateProperty("count", typeof(int));
        var tags = CreateProperty("tag", typeof(ImmutableArray<int>));
        var builder = new ParsingBuilder(
            DefaultOptions,
            properties: ImmutableDictionary<string, PropertyDefinition>.Empty
                .Add("verbose", verbose)
                .Add("count", count)
                .Add("tag", tags));

        ImmutableArray<Token> arguments =
        [
            new LongOptionToken("verbose"),
            new LongOptionToken("count"),
            new ArgumentOrCommandToken("42"),
            new LongOptionToken("tag"),
            new ArgumentOrCommandToken("1"),
            new LongOptionToken("tag"),
            new ArgumentOrCommandToken("2")
        ];

        var result = Parse(builder, arguments);

        var values = AssertFinishedCollection(result);

        await Assert.That((bool)values.GetValue(verbose)).IsTrue();
        await Assert.That((int)values.GetValue(count)).IsEqualTo(42);

        var actualTags = (ImmutableArray<int>)values.GetValue(tags);

        await Assert.That(actualTags.Length).IsEqualTo(2);
        await Assert.That(actualTags[0]).IsEqualTo(1);
        await Assert.That(actualTags[1]).IsEqualTo(2);
    }

    [Test]
    public async Task CreateParsing_Prefers_Subcommand_Over_Positional_Arguments()
    {
        var path = CreateArgument("path", typeof(string), 0, 1);
        var format = CreateProperty("format", typeof(string));
        var serve = CreateCommand("serve");
        var childBuilder = new ParsingBuilder(
            DefaultOptions,
            properties: ImmutableDictionary<string, PropertyDefinition>.Empty.Add("format", format));
        var rootBuilder = new ParsingBuilder(
            DefaultOptions,
            subcommandDefinitions: ImmutableDictionary<string, CommandDefinition>.Empty.Add("serve", serve),
            argument: [path],
            subcommands: ImmutableDictionary<string, IParsingBuilder>.Empty.Add("serve", childBuilder));

        ImmutableArray<Token> arguments =
        [
            new ArgumentOrCommandToken("serve"),
            new LongOptionToken("format"),
            new ArgumentOrCommandToken("json")
        ];

        var result = Parse(rootBuilder, arguments);
        var subcommand = AssertSubcommand(result, serve);
        var values = AssertFinishedCollection(subcommand.CommandAction());

        await Assert.That(values.Commands.ContainsKey("serve")).IsTrue();
        await Assert.That(values.Commands["serve"]).IsEqualTo(serve);
        await Assert.That((string)values.GetValue(format)).IsEqualTo("json");
        await Assert.That(values.GetValue(path)).IsNull();
    }

    [Test]
    public async Task CreateParsing_Preserves_Parent_Scope_Values_Across_Subcommand_Continuation()
    {
        var verbose = CreateProperty("verbose", typeof(bool), shortAliases: ["v"]);
        var format = CreateProperty("format", typeof(string));
        var serve = CreateCommand("serve");
        var childBuilder = new ParsingBuilder(
            DefaultOptions,
            properties: ImmutableDictionary<string, PropertyDefinition>.Empty.Add("format", format));
        var rootBuilder = new ParsingBuilder(
            DefaultOptions,
            subcommandDefinitions: ImmutableDictionary<string, CommandDefinition>.Empty.Add("serve", serve),
            properties: ImmutableDictionary<string, PropertyDefinition>.Empty.Add("verbose", verbose),
            subcommands: ImmutableDictionary<string, IParsingBuilder>.Empty.Add("serve", childBuilder));

        ImmutableArray<Token> arguments =
        [
            new LongOptionToken("verbose"),
            new ArgumentOrCommandToken("serve"),
            new LongOptionToken("format"),
            new ArgumentOrCommandToken("json")
        ];

        var result = Parse(rootBuilder, arguments);
        var subcommand = AssertSubcommand(result, serve);
        var values = AssertFinishedCollection(subcommand.CommandAction());

        await Assert.That((bool)values.GetValue(verbose)).IsTrue();
        await Assert.That((string)values.GetValue(format)).IsEqualTo("json");
    }

    [Test]
    public async Task ToInput_Captures_Mutable_Builder_State_Recursively()
    {
        var rootBuilder = new ParsingBuilder(DefaultOptions);
        var childBuilder = new ParsingBuilder(DefaultOptions);
        var serve = CreateCommand("serve");
        var verbose = CreateProperty("verbose", typeof(bool), shortAliases: ["v"]);
        var path = CreateArgument("path", typeof(string), 0, 1);

        rootBuilder.SubcommandDefinitions["serve"] = serve;
        rootBuilder.Subcommands["serve"] = childBuilder;
        rootBuilder.Properties["verbose"] = verbose;
        rootBuilder.Argument.Add(path);

        var snapshot = rootBuilder.ToInput();

        rootBuilder.Properties["later"] = CreateProperty("later", typeof(string));
        childBuilder.Properties["format"] = CreateProperty("format", typeof(string));

        await Assert.That(snapshot.SubcommandDefinitions.ContainsKey("serve")).IsTrue();
        await Assert.That(snapshot.Subcommands.ContainsKey("serve")).IsTrue();
        await Assert.That(snapshot.Properties.ContainsKey("verbose")).IsTrue();
        await Assert.That(snapshot.Properties.ContainsKey("later")).IsFalse();
        await Assert.That(snapshot.Subcommands["serve"].Properties.ContainsKey("format")).IsFalse();
        await Assert.That(snapshot.Argument.Count).IsEqualTo(1);
        await Assert.That(snapshot.Argument[0]).IsEqualTo(path);
    }

    [Test]
    public async Task CreateParsing_Parses_Nested_Subcommands_Recursively()
    {
        var interval = CreateProperty("interval", typeof(int));
        var serve = CreateCommand("serve");
        var watch = CreateCommand("watch");
        var watchBuilder = new ParsingBuilder(
            DefaultOptions,
            properties: ImmutableDictionary<string, PropertyDefinition>.Empty.Add("interval", interval));
        var serveBuilder = new ParsingBuilder(
            DefaultOptions,
            subcommandDefinitions: ImmutableDictionary<string, CommandDefinition>.Empty.Add("watch", watch),
            subcommands: ImmutableDictionary<string, IParsingBuilder>.Empty.Add("watch", watchBuilder));
        var rootBuilder = new ParsingBuilder(
            DefaultOptions,
            subcommandDefinitions: ImmutableDictionary<string, CommandDefinition>.Empty.Add("serve", serve),
            subcommands: ImmutableDictionary<string, IParsingBuilder>.Empty.Add("serve", serveBuilder));

        ImmutableArray<Token> arguments =
        [
            new ArgumentOrCommandToken("serve"),
            new ArgumentOrCommandToken("watch"),
            new LongOptionToken("interval"),
            new ArgumentOrCommandToken("5")
        ];

        var result = Parse(rootBuilder, arguments);
        var serveSubcommand = AssertSubcommand(result, serve);
        var watchSubcommand = AssertSubcommand(serveSubcommand.CommandAction(), watch);
        var values = AssertFinishedCollection(watchSubcommand.CommandAction());

        await Assert.That(values.Commands.ContainsKey("serve")).IsTrue();
        await Assert.That(values.Commands.ContainsKey("watch")).IsTrue();
        await Assert.That(values.Commands["serve"]).IsEqualTo(serve);
        await Assert.That(values.Commands["watch"]).IsEqualTo(watch);
        await Assert.That((int)values.GetValue(interval)).IsEqualTo(5);
    }

    [Test]
    public async Task CreateParsing_Returns_GotError_When_Subcommand_Definition_Has_No_Builder()
    {
        var serve = CreateCommand("serve");
        var builder = new ParsingBuilder(
            DefaultOptions,
            subcommandDefinitions: ImmutableDictionary<string, CommandDefinition>.Empty.Add("serve", serve));

        ImmutableArray<Token> arguments = [new ArgumentOrCommandToken("serve")];

        var result = Parse(builder, arguments);

        if (result is not GotError { Exception: InvalidOperationException exception })
        {
            throw new InvalidOperationException($"Expected {nameof(GotError)} with {typeof(InvalidOperationException).FullName}, got {result.GetType().FullName}.");
        }

        await Assert.That(exception.Message.Contains("does not have a builder", StringComparison.Ordinal)).IsTrue();
    }

    [Test]
    public async Task CreateParsing_Returns_InvalidArgument_When_Required_Property_Is_Missing()
    {
        var count = CreateProperty("count", typeof(int), required: true);
        var builder = new ParsingBuilder(
            DefaultOptions,
            properties: ImmutableDictionary<string, PropertyDefinition>.Empty.Add("count", count));

        var result = Parse(builder, []);

        if (result is not InvalidArgumentDetected invalid)
        {
            throw new InvalidOperationException($"Expected {nameof(InvalidArgumentDetected)}, got {result.GetType().FullName}.");
        }

        await Assert.That(invalid.Argument).IsEqualTo("count");
    }

    [Test]
    public async Task CreateParsing_Returns_InvalidArgument_When_Required_Positional_Argument_Is_Missing()
    {
        var path = CreateArgument("path", typeof(string), 1, 1, required: true);
        var builder = new ParsingBuilder(DefaultOptions, argument: [path]);

        var result = Parse(builder, []);

        if (result is not InvalidArgumentDetected invalid)
        {
            throw new InvalidOperationException($"Expected {nameof(InvalidArgumentDetected)}, got {result.GetType().FullName}.");
        }

        await Assert.That(invalid.Argument).IsEqualTo("path");
    }

    [Test]
    public async Task CreateParsing_Greedily_Preserves_Final_Required_Positional_Argument()
    {
        var head = CreateArgument("head", typeof(ImmutableArray<string>), 0, int.MaxValue);
        var tail = CreateArgument("tail", typeof(string), 1, 1, required: true);
        var builder = new ParsingBuilder(DefaultOptions, argument: [head, tail]);

        ImmutableArray<Token> arguments =
        [
            new ArgumentOrCommandToken("a"),
            new ArgumentOrCommandToken("b"),
            new ArgumentOrCommandToken("c"),
            new ArgumentOrCommandToken("d")
        ];

        var result = Parse(builder, arguments);
        var values = AssertFinishedCollection(result);
        var actualHead = (ImmutableArray<string>)values.GetValue(head);

        await Assert.That(actualHead.Length).IsEqualTo(3);
        await Assert.That(actualHead[0]).IsEqualTo("a");
        await Assert.That(actualHead[1]).IsEqualTo("b");
        await Assert.That(actualHead[2]).IsEqualTo("c");
        await Assert.That((string)values.GetValue(tail)).IsEqualTo("d");
    }

    [Test]
    public async Task CreateParsing_Greedily_Assigns_Single_Remaining_Value_To_Final_Required_Positional_Argument()
    {
        var head = CreateArgument("head", typeof(ImmutableArray<string>), 0, int.MaxValue);
        var tail = CreateArgument("tail", typeof(string), 1, 1, required: true);
        var builder = new ParsingBuilder(DefaultOptions, argument: [head, tail]);

        ImmutableArray<Token> arguments = [new ArgumentOrCommandToken("d")];

        var result = Parse(builder, arguments);
        var values = AssertFinishedCollection(result);
        var actualHead = (ImmutableArray<string>)values.GetValue(head);

        await Assert.That(actualHead.IsDefaultOrEmpty).IsTrue();
        await Assert.That((string)values.GetValue(tail)).IsEqualTo("d");
    }

    [Test]
    public async Task CreateParsing_Returns_InvalidArgument_When_Final_Required_Positional_Argument_Has_No_Value()
    {
        var head = CreateArgument("head", typeof(ImmutableArray<string>), 0, int.MaxValue);
        var tail = CreateArgument("tail", typeof(string), 1, 1, required: true);
        var builder = new ParsingBuilder(DefaultOptions, argument: [head, tail]);

        var result = Parse(builder, []);

        if (result is not InvalidArgumentDetected invalid)
        {
            throw new InvalidOperationException($"Expected {nameof(InvalidArgumentDetected)}, got {result.GetType().FullName}.");
        }

        await Assert.That(invalid.Argument).IsEqualTo("tail");
    }

    [Test]
    public async Task CreateParsing_Greedily_Preserves_Final_Required_Positional_Argument_Across_Intermediate_Variadic_Argument()
    {
        var first = CreateArgument("first", typeof(string), 1, 1, required: true);
        var middle = CreateArgument("middle", typeof(ImmutableArray<string>), 0, int.MaxValue);
        var last = CreateArgument("last", typeof(string), 1, 1, required: true);
        var builder = new ParsingBuilder(DefaultOptions, argument: [first, middle, last]);

        ImmutableArray<Token> arguments =
        [
            new ArgumentOrCommandToken("a"),
            new ArgumentOrCommandToken("b"),
            new ArgumentOrCommandToken("c"),
            new ArgumentOrCommandToken("d")
        ];

        var result = Parse(builder, arguments);
        var values = AssertFinishedCollection(result);
        var actualMiddle = (ImmutableArray<string>)values.GetValue(middle);

        await Assert.That((string)values.GetValue(first)).IsEqualTo("a");
        await Assert.That(actualMiddle.Length).IsEqualTo(2);
        await Assert.That(actualMiddle[0]).IsEqualTo("b");
        await Assert.That(actualMiddle[1]).IsEqualTo("c");
        await Assert.That((string)values.GetValue(last)).IsEqualTo("d");
    }

    [Test]
    public async Task CreateParsing_Returns_UnknownArgument_When_Positional_Arguments_Exceed_Maximum_Arity()
    {
        var first = CreateArgument("first", typeof(string), 1, 1, required: true);
        var second = CreateArgument("second", typeof(string), 0, 1);
        var builder = new ParsingBuilder(DefaultOptions, argument: [first, second]);

        ImmutableArray<Token> arguments =
        [
            new ArgumentOrCommandToken("a"),
            new ArgumentOrCommandToken("b"),
            new ArgumentOrCommandToken("c")
        ];

        var result = Parse(builder, arguments);

        if (result is not UnknownArgumentDetected unknown)
        {
            throw new InvalidOperationException($"Expected {nameof(UnknownArgumentDetected)}, got {result.GetType().FullName}.");
        }

        await Assert.That(unknown.UnknownArgument).IsEqualTo("c");
    }

    [Test]
    public async Task CreateParsing_Binds_Parent_Positional_Arguments_Before_Subcommand_Handoff()
    {
        var files = CreateArgument("files", typeof(ImmutableArray<string>), 0, int.MaxValue);
        var target = CreateArgument("target", typeof(string), 1, 1, required: true);
        var format = CreateProperty("format", typeof(string));
        var serve = CreateCommand("serve");
        var childBuilder = new ParsingBuilder(
            DefaultOptions,
            properties: ImmutableDictionary<string, PropertyDefinition>.Empty.Add("format", format));
        var rootBuilder = new ParsingBuilder(
            DefaultOptions,
            subcommandDefinitions: ImmutableDictionary<string, CommandDefinition>.Empty.Add("serve", serve),
            argument: [files, target],
            subcommands: ImmutableDictionary<string, IParsingBuilder>.Empty.Add("serve", childBuilder));

        ImmutableArray<Token> arguments =
        [
            new ArgumentOrCommandToken("a"),
            new ArgumentOrCommandToken("b"),
            new ArgumentOrCommandToken("serve"),
            new LongOptionToken("format"),
            new ArgumentOrCommandToken("json")
        ];

        var result = Parse(rootBuilder, arguments);
        var subcommand = AssertSubcommand(result, serve);
        var values = AssertFinishedCollection(subcommand.CommandAction());
        var actualFiles = (ImmutableArray<string>)values.GetValue(files);

        await Assert.That(actualFiles.Length).IsEqualTo(1);
        await Assert.That(actualFiles[0]).IsEqualTo("a");
        await Assert.That((string)values.GetValue(target)).IsEqualTo("b");
        await Assert.That((string)values.GetValue(format)).IsEqualTo("json");
    }

    [Test]
    public async Task CreateParsing_Returns_FailedValidation_When_Validation_Fails()
    {
        var count = CreateProperty("count", typeof(int)) with
        {
            Validation = static value => (int)value < 0 ? "must be non-negative" : null
        };
        var builder = new ParsingBuilder(
            DefaultOptions,
            properties: ImmutableDictionary<string, PropertyDefinition>.Empty.Add("count", count));

        ImmutableArray<Token> arguments =
        [
            new LongOptionToken("count"),
            new ShortOptionToken("1")
        ];

        var result = Parse(builder, arguments);

        if (result is not FailedValidation failed)
        {
            throw new InvalidOperationException($"Expected {nameof(FailedValidation)}, got {result.GetType().FullName}.");
        }

        await Assert.That(failed.Argument).IsEqualTo("count");
        await Assert.That(failed.Reason).IsEqualTo("must be non-negative");
    }

    [Test]
    public async Task CreateParsing_Returns_UnknownArgument_For_Unmatched_Option()
    {
        var builder = new ParsingBuilder(DefaultOptions);

        ImmutableArray<Token> arguments = [new LongOptionToken("unknown")];

        var result = Parse(builder, arguments);

        if (result is not UnknownArgumentDetected unknown)
        {
            throw new InvalidOperationException($"Expected {nameof(UnknownArgumentDetected)}, got {result.GetType().FullName}.");
        }

        await Assert.That(unknown.UnknownArgument).IsEqualTo("--unknown");
    }

    [Test]
    public async Task CreateParsing_Returns_InvalidArgument_For_Missing_Option_Value()
    {
        var count = CreateProperty("count", typeof(int));
        var builder = new ParsingBuilder(
            DefaultOptions,
            properties: ImmutableDictionary<string, PropertyDefinition>.Empty.Add("count", count));

        ImmutableArray<Token> arguments = [new LongOptionToken("count")];

        var result = Parse(builder, arguments);

        if (result is not InvalidArgumentDetected invalid)
        {
            throw new InvalidOperationException($"Expected {nameof(InvalidArgumentDetected)}, got {result.GetType().FullName}.");
        }

        await Assert.That(invalid.Argument).IsEqualTo("count");
    }

    [Test]
    public async Task CreateParsing_Returns_GotError_For_Unsupported_Target_Type()
    {
        var target = CreateProperty("target", typeof(Version));
        var builder = new ParsingBuilder(
            DefaultOptions,
            properties: ImmutableDictionary<string, PropertyDefinition>.Empty.Add("target", target));

        ImmutableArray<Token> arguments =
        [
            new LongOptionToken("target"),
            new ArgumentOrCommandToken("1.0")
        ];

        var result = Parse(builder, arguments);

        if (result is not GotError { Exception: NotSupportedException exception })
        {
            throw new InvalidOperationException($"Expected {nameof(GotError)} with {typeof(NotSupportedException).FullName}, got {result.GetType().FullName}.");
        }

        await Assert.That(exception.Message.Contains(typeof(Version).FullName!, StringComparison.Ordinal)).IsTrue();
    }

    [Test]
    public async Task ArgumentArity_Throws_When_Minimum_Is_Greater_Than_Maximum()
    {
        await Assert.That(() => new ArgumentArity(2, 1)).Throws<ArgumentException>();
    }

    [Test]
    public async Task ParsingResultCollection_GetValue_Uses_Explicit_Values_Default_Factories_And_Clr_Defaults()
    {
        var explicitProperty = CreateProperty("explicit", typeof(string));
        var defaultedProperty = CreateProperty("defaulted", typeof(int)) with
        {
            DefaultValueFactory = static _ => 7
        };
        var clrDefaultProperty = CreateProperty("plain", typeof(bool));
        var command = CreateCommand("serve");
        var collection = new ParsingResultCollection(
            ImmutableDictionary<string, CommandDefinition>.Empty.Add("serve", command),
            ImmutableDictionary<TypedDefinition, object?>.Empty.Add(explicitProperty, "value"));

        await Assert.That((string)collection.GetValue(explicitProperty)).IsEqualTo("value");
        await Assert.That((int)collection.GetValue(defaultedProperty)).IsEqualTo(7);
        await Assert.That((bool)collection.GetValue(clrDefaultProperty)).IsFalse();
        await Assert.That(collection.Commands["serve"]).IsEqualTo(command);
    }

    [Test]
    public async Task CreateParsing_Returns_HelpFlag_With_Root_Scoped_Output_And_No_Ansi_When_Style_Is_Disabled()
    {
        var writer = new StringWriter();
        var options = CreateOptions(writer, enableStyle: false);
        var input = CreateArgument("input", typeof(string), 0, 1);
        var verbose = CreateProperty("verbose", typeof(bool), shortAliases: ["v"]);
        var format = CreateProperty("format",
                                    typeof(string),
                                    possibleValues: new CountablePossibleValues<string>(["json", "yaml", "toml"]));
        var serve = CreateCommand("serve", summary: "Run the server", helpText: "Serve command help");
        var childBuilder = new ParsingBuilder(
            options,
            properties: ImmutableDictionary<string, PropertyDefinition>.Empty.Add("format", CreateProperty("format", typeof(string))));
        var builder = new ParsingBuilder(
            options,
            subcommandDefinitions: ImmutableDictionary<string, CommandDefinition>.Empty.Add("serve", serve),
            properties: ImmutableDictionary<string, PropertyDefinition>.Empty
                .Add("verbose", verbose)
                .Add("format", format),
            argument: [input],
            subcommands: ImmutableDictionary<string, IParsingBuilder>.Empty.Add("serve", childBuilder));

        var result = Parse(builder, [new LongOptionToken("help")]);

        if (result is not HelpFlagsDetected help)
        {
            throw new InvalidOperationException($"Expected {nameof(HelpFlagsDetected)}, got {result.GetType().FullName}.");
        }

        help.FlagAction();

        var output = writer.ToString();

        await Assert.That(output).Contains("test");
        await Assert.That(output).Contains("Usage");
        await Assert.That(output).Contains("test [options]");
        await Assert.That(output).Contains("test [<input>]");
        await Assert.That(output).Contains("test <subcommand>");
        await Assert.That(output).Contains("Options");
        await Assert.That(output).Contains("-v, --verbose");
        await Assert.That(output).Contains("--format <format>");
        await Assert.That(output).Contains("Possible values:");
        await Assert.That(output).Contains("json, yaml, toml");
        await Assert.That(output).Contains("Arguments");
        await Assert.That(output).Contains("input");
        await Assert.That(output).Contains("Subcommands");
        await Assert.That(output).Contains("serve");
        await Assert.That(output).Contains("Run the server");
        await Assert.That(output.Contains("\u001b[", StringComparison.Ordinal)).IsFalse();
    }

    [Test]
    public async Task CreateParsing_Returns_HelpFlag_With_Current_Subcommand_Output()
    {
        var writer = new StringWriter();
        var options = CreateOptions(writer, enableStyle: false);
        var rootProperty = CreateProperty("verbose", typeof(bool), shortAliases: ["v"]);
        var childProperty = CreateProperty("format",
                                           typeof(string),
                                           shortAliases: ["f"],
                                           possibleValues: new DescripablePossibleValues("json, yaml or any plugin-provided serializer"));
        var serve = CreateCommand("serve", summary: "Run the server", helpText: "Serve command help");
        var childBuilder = new ParsingBuilder(
            options,
            properties: ImmutableDictionary<string, PropertyDefinition>.Empty.Add("format", childProperty));
        var builder = new ParsingBuilder(
            options,
            subcommandDefinitions: ImmutableDictionary<string, CommandDefinition>.Empty.Add("serve", serve),
            properties: ImmutableDictionary<string, PropertyDefinition>.Empty.Add("verbose", rootProperty),
            subcommands: ImmutableDictionary<string, IParsingBuilder>.Empty.Add("serve", childBuilder));

        ImmutableArray<Token> arguments =
        [
            new ArgumentOrCommandToken("serve"),
            new LongOptionToken("help")
        ];

        var result = Parse(builder, arguments);
        var subcommand = AssertSubcommand(result, serve);
        var childResult = subcommand.CommandAction();

        if (childResult is not HelpFlagsDetected help)
        {
            throw new InvalidOperationException($"Expected {nameof(HelpFlagsDetected)}, got {childResult.GetType().FullName}.");
        }

        help.FlagAction();

        var output = writer.ToString();

        await Assert.That(output).Contains("serve");
        await Assert.That(output).Contains("serve [options]");
        await Assert.That(output).Contains("Serve command help");
        await Assert.That(output).Contains("-f, --format <format>");
        await Assert.That(output).Contains("Possible values:");
        await Assert.That(output).Contains("json, yaml or any plugin-provided serializer");
        await Assert.That(output.Contains("-v, --verbose", StringComparison.Ordinal)).IsFalse();
    }

    [Test]
    public async Task CreateParsing_Returns_VersionFlag_With_Styled_Output_When_Enabled()
    {
        var writer = new StringWriter();
        var options = CreateOptions(writer, enableStyle: true);
        var builder = new ParsingBuilder(options);

        var result = Parse(builder, [new LongOptionToken("version")]);

        if (result is not VersionFlagsDetected version)
        {
            throw new InvalidOperationException($"Expected {nameof(VersionFlagsDetected)}, got {result.GetType().FullName}.");
        }

        version.FlagAction();

        var output = writer.ToString();

        await Assert.That(output).Contains("test");
        await Assert.That(output).Contains("1.0");
        await Assert.That(output).Contains("https://example.com");
        await Assert.That(output).Contains("\u001b[");
    }

    [Test]
    public async Task CreateParsing_Returns_HelpFlag_With_Styled_Possible_Values_Output_When_Enabled()
    {
        var writer = new StringWriter();
        var options = CreateOptions(writer, enableStyle: true);
        var format = CreateProperty("format",
                                    typeof(string),
                                    possibleValues: new CountablePossibleValues<string>(["json", "yaml"]));
        var builder = new ParsingBuilder(
            options,
            properties: ImmutableDictionary<string, PropertyDefinition>.Empty.Add("format", format));

        var result = Parse(builder, [new LongOptionToken("help")]);

        if (result is not HelpFlagsDetected help)
        {
            throw new InvalidOperationException($"Expected {nameof(HelpFlagsDetected)}, got {result.GetType().FullName}.");
        }

        help.FlagAction();

        var output = writer.ToString();

        await Assert.That(output).Contains("Possible values:");
        await Assert.That(output).Contains("json, yaml");
        await Assert.That(output).Contains("\u001b[");
    }

    [Test]
    public async Task CreateParsing_Does_Not_Validate_Against_Possible_Values()
    {
        var writer = new StringWriter();
        var options = CreateOptions(writer, enableStyle: false);
        var format = CreateProperty("format",
                                    typeof(string),
                                    possibleValues: new CountablePossibleValues<string>(["json", "yaml"]));
        var builder = new ParsingBuilder(
            options,
            properties: ImmutableDictionary<string, PropertyDefinition>.Empty.Add("format", format));

        ImmutableArray<Token> arguments =
        [
            new LongOptionToken("format"),
            new ArgumentOrCommandToken("xml")
        ];

        var result = Parse(builder, arguments);
        var values = AssertFinishedCollection(result);

        await Assert.That((string)values.GetValue(format)).IsEqualTo("xml");
    }

    [Test]
    public async Task ParsingOptions_DefaultDebug_Reads_Truthy_CliDebug_Values_Case_Insensitive()
    {
        foreach (var value in new[] { "1", "ON", "y", "Yes", "true" })
        {
            await Assert.That(ReadDefaultDebug(value)).IsTrue();
        }
    }

    [Test]
    public async Task ParsingOptions_DefaultDebug_Returns_False_For_Unset_Or_Unrelated_Values()
    {
        await Assert.That(ReadDefaultDebug(null)).IsFalse();
        await Assert.That(ReadDefaultDebug("nope")).IsFalse();
    }

    [Test]
    public async Task CreateParsing_Writes_Debug_Output_For_Finished_Result_When_Debug_Is_Enabled()
    {
        var writer = new StringWriter();
        var options = CreateOptions(writer, enableStyle: false, debug: true);
        var count = CreateProperty("count", typeof(int));
        var builder = new ParsingBuilder(
            options,
            properties: ImmutableDictionary<string, PropertyDefinition>.Empty.Add("count", count));

        ImmutableArray<Token> arguments =
        [
            new LongOptionToken("count"),
            new ArgumentOrCommandToken("42")
        ];

        var result = Parse(builder, arguments);
        var values = AssertFinishedCollection(result);
        var output = writer.ToString();

        await Assert.That((int)values.GetValue(count)).IsEqualTo(42);
        await Assert.That(output).Contains("Debug Parse Result");
        await Assert.That(output).Contains("Source: ParsingBuilder");
        await Assert.That(output).Contains("Source: NumberParser");
        await Assert.That(output).Contains("Tokens: --count 42");
        await Assert.That(output).Contains("State: success");
        await Assert.That(output.Contains("\u001b[", StringComparison.Ordinal)).IsFalse();
    }

    [Test]
    public async Task CreateParsing_Writes_Debug_Output_For_Chained_Subcommands_When_Debug_Is_Enabled()
    {
        var writer = new StringWriter();
        var options = CreateOptions(writer, enableStyle: false, debug: true);
        var interval = CreateProperty("interval", typeof(int));
        var serve = CreateCommand("serve");
        var watch = CreateCommand("watch");
        var watchBuilder = new ParsingBuilder(
            options,
            properties: ImmutableDictionary<string, PropertyDefinition>.Empty.Add("interval", interval));
        var serveBuilder = new ParsingBuilder(
            options,
            subcommandDefinitions: ImmutableDictionary<string, CommandDefinition>.Empty.Add("watch", watch),
            subcommands: ImmutableDictionary<string, IParsingBuilder>.Empty.Add("watch", watchBuilder));
        var rootBuilder = new ParsingBuilder(
            options,
            subcommandDefinitions: ImmutableDictionary<string, CommandDefinition>.Empty.Add("serve", serve),
            subcommands: ImmutableDictionary<string, IParsingBuilder>.Empty.Add("serve", serveBuilder));

        ImmutableArray<Token> arguments =
        [
            new ArgumentOrCommandToken("serve"),
            new ArgumentOrCommandToken("watch"),
            new LongOptionToken("interval"),
            new ArgumentOrCommandToken("5")
        ];

        var firstResult = Parse(rootBuilder, arguments);
        var firstSubcommand = AssertSubcommand(firstResult, serve);
        var firstOutput = writer.ToString();

        await Assert.That(firstOutput).Contains("Command: serve");
        await Assert.That(firstOutput).Contains("Trigger token: serve");
        await Assert.That(firstOutput).Contains("State: deferred");

        var secondResult = firstSubcommand.CommandAction();
        var secondSubcommand = AssertSubcommand(secondResult, watch);
        var secondOutput = writer.ToString();

        await Assert.That(secondOutput).Contains("Command: watch");
        await Assert.That(secondOutput).Contains("Trigger token: watch");

        var finalResult = secondSubcommand.CommandAction();
        var values = AssertFinishedCollection(finalResult);
        var finalOutput = writer.ToString();

        await Assert.That((int)values.GetValue(interval)).IsEqualTo(5);
        await Assert.That(finalOutput).Contains("Summary: commands=serve, watch");
    }

    [Test]
    public async Task CreateParsing_Writes_Debug_Output_For_Flags_And_Trigger_Argument()
    {
        var writer = new StringWriter();
        var options = CreateOptions(writer, enableStyle: false, debug: true);
        var builder = new ParsingBuilder(options);

        var result = Parse(builder, [new LongOptionToken("help")]);

        if (result is not HelpFlagsDetected help)
        {
            throw new InvalidOperationException($"Expected {nameof(HelpFlagsDetected)}, got {result.GetType().FullName}.");
        }

        var output = writer.ToString();

        await Assert.That(output).Contains("Result: HelpFlagsDetected");
        await Assert.That(output).Contains("Trigger argument: --help");

        help.FlagAction();
    }

    [Test]
    public async Task CreateParsing_Writes_Debug_Output_For_Error_Results()
    {
        var writer = new StringWriter();
        var options = CreateOptions(writer, enableStyle: false, debug: true);
        var count = CreateProperty("count", typeof(int)) with
        {
            Validation = static value => (int)value < 0 ? "must be non-negative" : null
        };
        var builder = new ParsingBuilder(
            options,
            properties: ImmutableDictionary<string, PropertyDefinition>.Empty.Add("count", count));

        var unknownResult = Parse(builder, [new LongOptionToken("unknown")]);

        if (unknownResult is not UnknownArgumentDetected)
        {
            throw new InvalidOperationException($"Expected {nameof(UnknownArgumentDetected)}, got {unknownResult.GetType().FullName}.");
        }

        await Assert.That(writer.ToString()).Contains("Unknown argument: --unknown");

        writer.GetStringBuilder().Clear();

        ImmutableArray<Token> invalidArguments = [new LongOptionToken("count")];
        var invalidResult = Parse(builder, invalidArguments);

        if (invalidResult is not InvalidArgumentDetected)
        {
            throw new InvalidOperationException($"Expected {nameof(InvalidArgumentDetected)}, got {invalidResult.GetType().FullName}.");
        }

        await Assert.That(writer.ToString()).Contains("Argument: count");
        await Assert.That(writer.ToString()).Contains("Expect: System.Int32");

        writer.GetStringBuilder().Clear();

        ImmutableArray<Token> failedArguments =
        [
            new LongOptionToken("count"),
            new ShortOptionToken("1")
        ];
        var failedResult = Parse(builder, failedArguments);

        if (failedResult is not FailedValidation)
        {
            throw new InvalidOperationException($"Expected {nameof(FailedValidation)}, got {failedResult.GetType().FullName}.");
        }

        await Assert.That(writer.ToString()).Contains("Reason: must be non-negative");
    }

    [Test]
    public async Task CreateParsing_Writes_Styled_Debug_Output_When_Style_Is_Enabled()
    {
        var writer = new StringWriter();
        var options = CreateOptions(writer, enableStyle: true, debug: true);
        var builder = new ParsingBuilder(options);

        var result = Parse(builder, [new LongOptionToken("version")]);

        if (result is not VersionFlagsDetected)
        {
            throw new InvalidOperationException($"Expected {nameof(VersionFlagsDetected)}, got {result.GetType().FullName}.");
        }

        await Assert.That(writer.ToString()).Contains("\u001b[");
    }

    [Test]
    public async Task StyledStringBuilder_Falls_Back_To_Plain_Text_When_Style_Is_Disabled()
    {
        var builder = new StyledStringBuilder(false);
        var style = new Style(Color.Sky, Color.None, true, false, false);

        builder.Append(style, "hello")
            .AppendLine(style, "world");

        await Assert.That(builder.ToString()).IsEqualTo("helloworld\n");
    }

    [Test]
    public async Task Style_ToAnsiCode_Formats_Combined_Styles()
    {
        var style = new Style(new Color(1, 2, 3, 255), new Color(4, 5, 6, 255), true, true, true);

        await Assert.That(style.ToAnsiCode()).IsEqualTo("\u001b[1;3;4;38;2;1;2;3;48;2;4;5;6m");
        await Assert.That(Style.ClearStyle).IsEqualTo("\u001b[0m");
    }

    private static IParsingResultCollection AssertFinishedCollection(ParsingResult result)
    {
        return result is ParsingFinished { UntypedResult: IParsingResultCollection collection }
            ? collection
            : throw new InvalidOperationException($"Expected {nameof(ParsingFinished)}, got {result.GetType().FullName}.");
    }

    private static ParsingResult Parse(ParsingBuilder builder, ImmutableArray<Token> arguments)
    {
        return ParsingBuilder.CreateParsing(builder.ParsingOptions, arguments, builder.ToInput());
    }

    private static Subcommand AssertSubcommand(ParsingResult result, CommandDefinition expected)
    {
        if (result is not Subcommand subcommand)
        {
            throw new InvalidOperationException($"Expected {nameof(Subcommand)}, got {result.GetType().FullName}.");
        }

        if (!EqualityComparer<CommandDefinition>.Default.Equals(subcommand.Definition, expected))
        {
            throw new InvalidOperationException(
                $"Expected subcommand '{expected.Information.Name.Value}', got '{subcommand.Definition.Information.Name.Value}'.");
        }

        return subcommand;
    }

    private static DefinitionInformation CreateInformation(string name)
    {
        return new DefinitionInformation(new NameWithVisibility(name, true), new Document(name, name));
    }

    private static PropertyDefinition CreateProperty(string name,
                                                     Type type,
                                                     bool required = false,
                                                     ImmutableArray<string> longAliases = default,
                                                     ImmutableArray<string> shortAliases = default,
                                                     PossibleValues? possibleValues = null)
    {
        var longNames = ImmutableDictionary.CreateBuilder<string, NameWithVisibility>(StringComparer.Ordinal);
        var shortNames = ImmutableDictionary.CreateBuilder<string, NameWithVisibility>(StringComparer.Ordinal);

        foreach (var alias in longAliases.IsDefaultOrEmpty ? [name] : longAliases)
        {
            longNames[alias] = new NameWithVisibility(alias, true);
        }

        foreach (var alias in shortAliases.IsDefaultOrEmpty ? [] : shortAliases)
        {
            shortNames[alias] = new NameWithVisibility(alias, true);
        }

        return new PropertyDefinition(
            CreateInformation(name),
            longNames.ToImmutable(),
            shortNames.ToImmutable(),
            null,
            type,
            required)
        {
            PossibleValues = possibleValues
        };
    }

    private static ArgumentDefinition CreateArgument(string name,
                                                     Type type,
                                                     int minimum,
                                                     int maximum,
                                                     bool required = false)
    {
        return new ArgumentDefinition(
            CreateInformation(name),
            null,
            new ArgumentArity(minimum, maximum),
            type,
            required);
    }

    private static CommandDefinition CreateCommand(string name)
    {
        return CreateCommand(name, name, name);
    }

    private static CommandDefinition CreateCommand(string name, string summary, string helpText)
    {
        return new CommandDefinition(
            new DefinitionInformation(new NameWithVisibility(name, true), new Document(summary, helpText)),
            null);
    }

    private static ParsingOptions CreateOptions(TextWriter? output = null, bool enableStyle = false, bool debug = false)
    {
        return new ParsingOptions(
            new ProgramInformation("test", new Document("test", "test help"), new Version(1, 0), "https://example.com"),
            ParsingOptions.DefaultVersionFlags,
            ParsingOptions.DefaultHelpFlags,
            output ?? TextWriter.Null,
            enableStyle,
            debug);
    }

    private static bool ReadDefaultDebug(string? value)
    {
        var original = Environment.GetEnvironmentVariable("CLI_DEBUG");

        try
        {
            Environment.SetEnvironmentVariable("CLI_DEBUG", value);
            ResetParsingOptionsField("_defaultDebug");
            return ParsingOptions.DefaultDebug;
        }
        finally
        {
            Environment.SetEnvironmentVariable("CLI_DEBUG", original);
            ResetParsingOptionsField("_defaultDebug");
        }
    }

    private static void ResetParsingOptionsField(string fieldName)
    {
        var field = typeof(ParsingOptions).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException($"Field '{fieldName}' was not found.");

        field.SetValue(null, null);
    }
}
