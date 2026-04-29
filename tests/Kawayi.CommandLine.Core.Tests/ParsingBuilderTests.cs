// Copyright (c) 2026 MoeGodot<me@kawayi.moe>.
// Licensed under the GNU Affero General Public License v3-or-later license.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Reflection;
using Kawayi.CommandLine.Abstractions;
using Kawayi.CommandLine.Extensions;

namespace Kawayi.CommandLine.Core.Tests;

public sealed class ParsingBuilderTests
{
    private static readonly ParsingOptions DefaultOptions = CreateOptions();
    private static readonly Lock DefaultStyleEnvironmentLock = new();
    private static readonly Lock DefaultDebugEnvironmentLock = new();

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
            new ArgumentOrCommandToken("true"),
            new LongOptionToken("count"),
            new ArgumentOrCommandToken("42"),
            new LongOptionToken("tag"),
            new ArgumentOrCommandToken("1"),
            new LongOptionToken("tag"),
            new ArgumentOrCommandToken("2")
        ];

        var result = Parse(builder, arguments);

        var values = AssertFinishedCollection(result);

        await Assert.That(GetEffectiveValue<bool>(values, verbose)).IsTrue();
        await Assert.That(GetEffectiveValue<int>(values, count)).IsEqualTo(42);

        var actualTags = GetEffectiveValue<ImmutableArray<int>>(values, tags);

        await Assert.That(actualTags.Length).IsEqualTo(2);
        await Assert.That(actualTags[0]).IsEqualTo(1);
        await Assert.That(actualTags[1]).IsEqualTo(2);
    }

    [Test]
    public async Task CreateParsing_Parses_Negative_Number_Option_Value()
    {
        var count = CreateProperty("count", typeof(int));
        var builder = new ParsingBuilder(
            DefaultOptions,
            properties: ImmutableDictionary<string, PropertyDefinition>.Empty.Add("count", count));

        ImmutableArray<Token> arguments =
        [
            new LongOptionToken("count"),
            new ShortOptionToken("1")
        ];

        var result = Parse(builder, arguments);
        var values = AssertFinishedCollection(result);

        await Assert.That(GetEffectiveValue<int>(values, count)).IsEqualTo(-1);
    }

    [Test]
    public async Task CreateParsing_Preserves_DashPrefixed_String_Option_Value()
    {
        var linkerOptions = CreateProperty("linkerOptions", typeof(string), longAliases: ["linker-opts"]);
        var builder = new ParsingBuilder(
            DefaultOptions,
            properties: ImmutableDictionary<string, PropertyDefinition>.Empty.Add("linkerOptions", linkerOptions));

        ImmutableArray<Token> arguments =
        [
            new LongOptionToken("linker-opts"),
            new ShortOptionToken("L/bin/foo.a")
        ];

        var result = Parse(builder, arguments);
        var values = AssertFinishedCollection(result);

        await Assert.That(GetEffectiveValue<string>(values, linkerOptions)).IsEqualTo("-L/bin/foo.a");
    }

    [Test]
    public async Task CreateParsing_Parses_Long_Option_With_Inline_Value()
    {
        var format = CreateProperty("format", typeof(string));
        var builder = new ParsingBuilder(
            DefaultOptions,
            properties: ImmutableDictionary<string, PropertyDefinition>.Empty.Add("format", format));

        ImmutableArray<Token> arguments =
        [
            new LongOptionToken("format", "json")
        ];

        var result = Parse(builder, arguments);
        var values = AssertFinishedCollection(result);

        await Assert.That(GetEffectiveValue<string>(values, format)).IsEqualTo("json");
    }

    [Test]
    public async Task CreateParsing_Long_Option_With_Inline_Value_Does_Not_Consume_Following_Token()
    {
        var format = CreateProperty("format", typeof(string));
        var path = CreateArgument("path", typeof(string), 0, 1);
        var builder = new ParsingBuilder(
            DefaultOptions,
            argument: [path],
            properties: ImmutableDictionary<string, PropertyDefinition>.Empty.Add("format", format));

        ImmutableArray<Token> arguments =
        [
            new LongOptionToken("format", "json"),
            new ArgumentOrCommandToken("payload")
        ];

        var result = Parse(builder, arguments);
        var values = AssertFinishedCollection(result);

        await Assert.That(GetEffectiveValue<string>(values, format)).IsEqualTo("json");
        await Assert.That(GetEffectiveValue<string>(values, path)).IsEqualTo("payload");
    }

    [Test]
    public async Task CreateParsing_Parses_Bool_Scalar_Option_With_Inline_Value()
    {
        var verbose = CreateProperty("verbose", typeof(bool), shortAliases: ["v"]);
        var builder = new ParsingBuilder(
            DefaultOptions,
            properties: ImmutableDictionary<string, PropertyDefinition>.Empty.Add("verbose", verbose));

        ImmutableArray<Token> arguments =
        [
            new LongOptionToken("verbose", "false")
        ];

        var result = Parse(builder, arguments);
        var values = AssertFinishedCollection(result);

        await Assert.That(GetEffectiveValue<bool>(values, verbose)).IsFalse();
    }

    [Test]
    public async Task CreateParsing_Parses_Bool_Scalar_Option_From_Short_Alias_With_Explicit_Value()
    {
        var verbose = CreateProperty("verbose", typeof(bool), shortAliases: ["v"]);
        var builder = new ParsingBuilder(
            DefaultOptions,
            properties: ImmutableDictionary<string, PropertyDefinition>.Empty.Add("verbose", verbose));

        ImmutableArray<Token> arguments =
        [
            new ShortOptionToken("v"),
            new ArgumentOrCommandToken("false")
        ];

        var result = Parse(builder, arguments);
        var values = AssertFinishedCollection(result);

        await Assert.That(GetEffectiveValue<bool>(values, verbose)).IsFalse();
    }

    [Test]
    public async Task CreateParsing_Parses_Bare_Bool_Long_Option_As_True()
    {
        var verbose = CreateProperty("verbose", typeof(bool), shortAliases: ["v"]);
        var builder = new ParsingBuilder(
            DefaultOptions,
            properties: ImmutableDictionary<string, PropertyDefinition>.Empty.Add("verbose", verbose));

        var result = Parse(builder, [new LongOptionToken("verbose")]);
        var values = AssertFinishedCollection(result);

        await Assert.That(GetEffectiveValue<bool>(values, verbose)).IsTrue();
    }

    [Test]
    public async Task CreateParsing_Parses_Bare_Bool_Short_Option_As_True()
    {
        var verbose = CreateProperty("verbose", typeof(bool), shortAliases: ["v"]);
        var builder = new ParsingBuilder(
            DefaultOptions,
            properties: ImmutableDictionary<string, PropertyDefinition>.Empty.Add("verbose", verbose));

        var result = Parse(builder, [new ShortOptionToken("v")]);
        var values = AssertFinishedCollection(result);

        await Assert.That(GetEffectiveValue<bool>(values, verbose)).IsTrue();
    }

    [Test]
    public async Task CreateParsing_Returns_UnknownArgument_For_Bool_Option_Followed_By_NonBoolean_Without_Positional_Target()
    {
        var verbose = CreateProperty("verbose", typeof(bool), shortAliases: ["v"]);
        var builder = new ParsingBuilder(
            DefaultOptions,
            properties: ImmutableDictionary<string, PropertyDefinition>.Empty.Add("verbose", verbose));

        ImmutableArray<Token> arguments =
        [
            new LongOptionToken("verbose"),
            new ArgumentOrCommandToken("maybe")
        ];

        var result = Parse(builder, arguments);

        if (result is not UnknownArgumentDetected unknownArgument)
        {
            throw new InvalidOperationException($"Expected {nameof(UnknownArgumentDetected)}, got {result.GetType().FullName}.");
        }

        await Assert.That(unknownArgument.UnknownArgument).IsEqualTo("maybe");
    }

    [Test]
    public async Task CreateParsing_Returns_InvalidArgument_For_Bool_Inline_NonBoolean_Value()
    {
        var verbose = CreateProperty("verbose", typeof(bool), shortAliases: ["v"]);
        var builder = new ParsingBuilder(
            DefaultOptions,
            properties: ImmutableDictionary<string, PropertyDefinition>.Empty.Add("verbose", verbose));

        var result = Parse(builder, [new LongOptionToken("verbose", "maybe")]);

        if (result is not InvalidArgumentDetected invalidArgument)
        {
            throw new InvalidOperationException($"Expected {nameof(InvalidArgumentDetected)}, got {result.GetType().FullName}.");
        }

        await Assert.That(invalidArgument.Argument).IsEqualTo("maybe");
        await Assert.That(invalidArgument.Expect).IsEqualTo("bool");
    }

    [Test]
    public async Task CreateParsing_Bool_Option_Leaves_NonBoolean_Next_Token_For_Positional_Binding()
    {
        var verbose = CreateProperty("verbose", typeof(bool), shortAliases: ["v"]);
        var path = CreateArgument("path", typeof(string), 0, 1);
        var builder = new ParsingBuilder(
            DefaultOptions,
            argument: [path],
            properties: ImmutableDictionary<string, PropertyDefinition>.Empty.Add("verbose", verbose));

        ImmutableArray<Token> arguments =
        [
            new LongOptionToken("verbose"),
            new ArgumentOrCommandToken("payload")
        ];

        var result = Parse(builder, arguments);
        var values = AssertFinishedCollection(result);

        await Assert.That(GetEffectiveValue<bool>(values, verbose)).IsTrue();
        await Assert.That(GetEffectiveValue<string>(values, path)).IsEqualTo("payload");
    }

    [Test]
    public async Task CreateParsing_Bool_Option_Leaves_NonBoolean_Next_Token_For_Subcommand_Matching()
    {
        var verbose = CreateProperty("verbose", typeof(bool), shortAliases: ["v"]);
        var serve = CreateCommand("serve");
        var childBuilder = new ParsingBuilder(DefaultOptions);
        var rootBuilder = new ParsingBuilder(
            DefaultOptions,
            subcommandDefinitions: ImmutableDictionary<string, CommandDefinition>.Empty.Add("serve", serve),
            properties: ImmutableDictionary<string, PropertyDefinition>.Empty.Add("verbose", verbose),
            subcommands: ImmutableDictionary<string, IParsingBuilder>.Empty.Add("serve", childBuilder));

        ImmutableArray<Token> arguments =
        [
            new LongOptionToken("verbose"),
            new ArgumentOrCommandToken("serve")
        ];

        var result = Parse(rootBuilder, arguments);
        var subcommand = AssertSubcommand(result, serve);
        var parentValues = AssertFinishedCollection(subcommand.ParentCommand);

        await Assert.That(GetEffectiveValue<bool>(parentValues, verbose)).IsTrue();
    }

    [Test]
    public async Task CreateParsing_Bool_Option_Leaves_NonBoolean_Next_Token_For_Option_Parsing()
    {
        var verbose = CreateProperty("verbose", typeof(bool), shortAliases: ["v"]);
        var count = CreateProperty("count", typeof(int));
        var builder = new ParsingBuilder(
            DefaultOptions,
            properties: ImmutableDictionary<string, PropertyDefinition>.Empty
                .Add("verbose", verbose)
                .Add("count", count));

        ImmutableArray<Token> arguments =
        [
            new LongOptionToken("verbose"),
            new LongOptionToken("count"),
            new ArgumentOrCommandToken("3")
        ];

        var result = Parse(builder, arguments);
        var values = AssertFinishedCollection(result);

        await Assert.That(GetEffectiveValue<bool>(values, verbose)).IsTrue();
        await Assert.That(GetEffectiveValue<int>(values, count)).IsEqualTo(3);
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
        var parentValues = AssertFinishedCollection(subcommand.ParentCommand);
        var values = AssertFinishedCollection(subcommand.ContinueParseAction());
        var root = AssertRoot(values);
        var selectedServe = AssertSubcommandNode(root, serve);

        _ = AssertRoot(parentValues);
        await Assert.That(values.Command).IsEqualTo(serve);
        await Assert.That(selectedServe).IsEqualTo(values);
        await Assert.That(GetEffectiveValue<string>(values, format)).IsEqualTo("json");
        await Assert.That(GetEffectiveValueOrDefault(root, path)).IsNull();
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
            new ArgumentOrCommandToken("true"),
            new ArgumentOrCommandToken("serve"),
            new LongOptionToken("format"),
            new ArgumentOrCommandToken("json")
        ];

        var result = Parse(rootBuilder, arguments);
        var subcommand = AssertSubcommand(result, serve);
        var parentValues = AssertFinishedCollection(subcommand.ParentCommand);
        var values = AssertFinishedCollection(subcommand.ContinueParseAction());
        var root = AssertRoot(values);

        _ = AssertRoot(parentValues);
        await Assert.That(HasExplicitValue(parentValues, verbose, out bool parentVerbose)).IsTrue();
        await Assert.That(parentVerbose).IsTrue();
        await Assert.That(HasExplicitValue(root, verbose, out bool rootVerbose)).IsTrue();
        await Assert.That(rootVerbose).IsTrue();
        await Assert.That(GetEffectiveValue<string>(values, format)).IsEqualTo("json");
    }

    [Test]
    public async Task CreateParsing_Matches_Subcommand_Alias()
    {
        var format = CreateProperty("format", typeof(string));
        var serve = CreateCommand("serve", aliases: ["srv", "s"]);
        var childBuilder = new ParsingBuilder(
            DefaultOptions,
            properties: ImmutableDictionary<string, PropertyDefinition>.Empty.Add("format", format));
        var rootBuilder = new ParsingBuilder(
            DefaultOptions,
            subcommandDefinitions: ImmutableDictionary<string, CommandDefinition>.Empty.Add("serve", serve),
            subcommands: ImmutableDictionary<string, IParsingBuilder>.Empty.Add("serve", childBuilder));

        ImmutableArray<Token> arguments =
        [
            new ArgumentOrCommandToken("srv"),
            new LongOptionToken("format"),
            new ArgumentOrCommandToken("json")
        ];

        var result = Parse(rootBuilder, arguments);
        var subcommand = AssertSubcommand(result, serve);
        var values = AssertFinishedCollection(subcommand.ContinueParseAction());
        var root = AssertRoot(values);

        await Assert.That(values.Command).IsEqualTo(serve);
        await Assert.That(AssertSubcommandNode(root, serve)).IsEqualTo(values);
        await Assert.That(GetEffectiveValue<string>(values, format)).IsEqualTo("json");
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

        var snapshot = rootBuilder.Build();

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
    public async Task ParsingInputParser_CreateParsing_Matches_ParsingBuilder_Facade()
    {
        var count = CreateProperty("count", typeof(int));
        var builder = new ParsingBuilder(
            DefaultOptions,
            properties: ImmutableDictionary<string, PropertyDefinition>.Empty.Add("count", count));
        ImmutableArray<Token> arguments =
        [
            new LongOptionToken("count"),
            new ArgumentOrCommandToken("42")
        ];

        var snapshot = builder.Build();
        var facadeResult = ParsingBuilder.CreateParsing(builder.ParsingOptions, arguments, snapshot);
        var parserResult = ParsingInputParser.CreateParsing(builder.ParsingOptions, arguments, snapshot);
        var facadeValues = AssertFinishedCollection(facadeResult);
        var parserValues = AssertFinishedCollection(parserResult);

        await Assert.That(GetEffectiveValue<int>(facadeValues, count)).IsEqualTo(42);
        await Assert.That(GetEffectiveValue<int>(parserValues, count)).IsEqualTo(42);
    }

    [Test]
    public async Task CreateParsing_Parses_Enum_Typed_Option()
    {
        var mode = CreateProperty("mode", typeof(SampleMode));
        var builder = new ParsingBuilder(
            DefaultOptions,
            properties: ImmutableDictionary<string, PropertyDefinition>.Empty.Add("mode", mode));
        ImmutableArray<Token> arguments =
        [
            new LongOptionToken("mode"),
            new ArgumentOrCommandToken("advanced")
        ];

        var result = Parse(builder, arguments);
        var values = AssertFinishedCollection(result);

        await Assert.That(GetEffectiveValue<SampleMode>(values, mode)).IsEqualTo(SampleMode.Advanced);
    }

    [Test]
    public async Task CreateParsing_Parses_Enum_Typed_Argument()
    {
        var mode = CreateArgument("mode", typeof(SampleMode), 1, 1);
        var builder = new ParsingBuilder(DefaultOptions, argument: [mode]);
        ImmutableArray<Token> arguments =
        [
            new ArgumentOrCommandToken("ADVANCED")
        ];

        var result = Parse(builder, arguments);
        var values = AssertFinishedCollection(result);

        await Assert.That(GetEffectiveValue<SampleMode>(values, mode)).IsEqualTo(SampleMode.Advanced);
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
        var root = AssertFinishedCollection(serveSubcommand.ParentCommand);
        var watchSubcommand = AssertSubcommand(serveSubcommand.ContinueParseAction(), watch);
        var serveNode = AssertFinishedCollection(watchSubcommand.ParentCommand);
        var values = AssertFinishedCollection(watchSubcommand.ContinueParseAction());
        var actualServeNode = AssertParent(values, serve);
        var actualRoot = AssertRoot(actualServeNode);
        var selectedServe = AssertSubcommandNode(actualRoot, serve);
        var selectedWatch = AssertSubcommandNode(actualServeNode, watch);

        _ = AssertRoot(root);
        await Assert.That(serveNode.Command).IsEqualTo(serve);
        await Assert.That(AssertRoot(serveNode)).IsNotNull();
        await Assert.That(values.Command).IsEqualTo(watch);
        await Assert.That(selectedServe).IsEqualTo(actualServeNode);
        await Assert.That(selectedWatch).IsEqualTo(values);
        await Assert.That(GetEffectiveValue<int>(values, interval)).IsEqualTo(5);
        await Assert.That(actualRoot.Scope.AvailableSubcommands.Contains(serve)).IsTrue();
        await Assert.That(actualServeNode.Scope.AvailableSubcommands.Contains(watch)).IsTrue();
        await Assert.That(actualRoot.TryGetSubcommand(watch, out _)).IsFalse();
        await Assert.That(values.Scope.AvailableTypedDefinitions.Contains(interval)).IsTrue();
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
        var actualHead = GetEffectiveValue<ImmutableArray<string>>(values, head);

        await Assert.That(actualHead.Length).IsEqualTo(3);
        await Assert.That(actualHead[0]).IsEqualTo("a");
        await Assert.That(actualHead[1]).IsEqualTo("b");
        await Assert.That(actualHead[2]).IsEqualTo("c");
        await Assert.That(GetEffectiveValue<string>(values, tail)).IsEqualTo("d");
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
        var actualHead = GetEffectiveValue<ImmutableArray<string>>(values, head);

        await Assert.That(actualHead.IsDefaultOrEmpty).IsTrue();
        await Assert.That(GetEffectiveValue<string>(values, tail)).IsEqualTo("d");
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
        var actualMiddle = GetEffectiveValue<ImmutableArray<string>>(values, middle);

        await Assert.That(GetEffectiveValue<string>(values, first)).IsEqualTo("a");
        await Assert.That(actualMiddle.Length).IsEqualTo(2);
        await Assert.That(actualMiddle[0]).IsEqualTo("b");
        await Assert.That(actualMiddle[1]).IsEqualTo("c");
        await Assert.That(GetEffectiveValue<string>(values, last)).IsEqualTo("d");
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
        var parentValues = AssertFinishedCollection(subcommand.ParentCommand);
        var values = AssertFinishedCollection(subcommand.ContinueParseAction());
        var root = AssertRoot(values);
        var actualFiles = GetEffectiveValue<ImmutableArray<string>>(root, files);

        _ = AssertRoot(parentValues);
        await Assert.That(actualFiles.Length).IsEqualTo(1);
        await Assert.That(actualFiles[0]).IsEqualTo("a");
        await Assert.That(GetEffectiveValue<string>(parentValues, target)).IsEqualTo("b");
        await Assert.That(GetEffectiveValue<string>(root, target)).IsEqualTo("b");
        await Assert.That(GetEffectiveValue<string>(values, format)).IsEqualTo("json");
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
    public async Task ValueRange_Throws_When_Minimum_Is_Greater_Than_Maximum()
    {
        await Assert.That(() => new ValueRange(2, 1)).Throws<ArgumentException>();
    }

    [Test]
    public async Task CreateParsing_Parses_Property_When_ValueCount_Matches_Explicit_ValueRange()
    {
        var format = CreateProperty("format", typeof(string), numArgs: new ValueRange(1, 1));
        var builder = new ParsingBuilder(
            DefaultOptions,
            properties: ImmutableDictionary<string, PropertyDefinition>.Empty.Add("format", format));

        ImmutableArray<Token> arguments =
        [
            new LongOptionToken("format"),
            new ArgumentOrCommandToken("json")
        ];

        var result = Parse(builder, arguments);
        var values = AssertFinishedCollection(result);

        await Assert.That(GetEffectiveValue<string>(values, format)).IsEqualTo("json");
    }

    [Test]
    public async Task CreateParsing_Returns_InvalidArgument_When_Property_Receives_Fewer_Values_Than_Allowed()
    {
        var format = CreateProperty("format", typeof(string), numArgs: new ValueRange(1, 1));
        var builder = new ParsingBuilder(
            DefaultOptions,
            properties: ImmutableDictionary<string, PropertyDefinition>.Empty.Add("format", format));

        var result = Parse(builder, []);

        if (result is not InvalidArgumentDetected invalid)
        {
            throw new InvalidOperationException($"Expected {nameof(InvalidArgumentDetected)}, got {result.GetType().FullName}.");
        }

        await Assert.That(invalid.Argument).IsEqualTo("format");
        await Assert.That(invalid.Expect).IsEqualTo("exactly 1 value(s)");
    }

    [Test]
    public async Task CreateParsing_Returns_InvalidArgument_When_Property_Receives_More_Values_Than_Allowed()
    {
        var format = CreateProperty("format", typeof(string), numArgs: new ValueRange(1, 1));
        var builder = new ParsingBuilder(
            DefaultOptions,
            properties: ImmutableDictionary<string, PropertyDefinition>.Empty.Add("format", format));

        ImmutableArray<Token> arguments =
        [
            new LongOptionToken("format"),
            new ArgumentOrCommandToken("json"),
            new LongOptionToken("format"),
            new ArgumentOrCommandToken("yaml")
        ];

        var result = Parse(builder, arguments);

        if (result is not InvalidArgumentDetected invalid)
        {
            throw new InvalidOperationException($"Expected {nameof(InvalidArgumentDetected)}, got {result.GetType().FullName}.");
        }

        await Assert.That(invalid.Argument).IsEqualTo("format");
        await Assert.That(invalid.Expect).IsEqualTo("exactly 1 value(s)");
    }

    [Test]
    public async Task CreateParsing_Counts_Inline_Long_Option_Value_Towards_Property_ValueRange()
    {
        var format = CreateProperty("format", typeof(string), numArgs: new ValueRange(1, 1));
        var builder = new ParsingBuilder(
            DefaultOptions,
            properties: ImmutableDictionary<string, PropertyDefinition>.Empty.Add("format", format));

        ImmutableArray<Token> arguments =
        [
            new LongOptionToken("format", "json"),
            new LongOptionToken("format", "yaml")
        ];

        var result = Parse(builder, arguments);

        if (result is not InvalidArgumentDetected invalid)
        {
            throw new InvalidOperationException($"Expected {nameof(InvalidArgumentDetected)}, got {result.GetType().FullName}.");
        }

        await Assert.That(invalid.Argument).IsEqualTo("format");
        await Assert.That(invalid.Expect).IsEqualTo("exactly 1 value(s)");
    }

    [Test]
    public async Task CreateParsing_Default_Property_ValueRange_Allows_Repeated_Scalar_Options()
    {
        var format = CreateProperty("format", typeof(string));
        var builder = new ParsingBuilder(
            DefaultOptions,
            properties: ImmutableDictionary<string, PropertyDefinition>.Empty.Add("format", format));

        ImmutableArray<Token> arguments =
        [
            new LongOptionToken("format"),
            new ArgumentOrCommandToken("json"),
            new LongOptionToken("format"),
            new ArgumentOrCommandToken("yaml")
        ];

        var result = Parse(builder, arguments);
        var values = AssertFinishedCollection(result);

        await Assert.That(GetEffectiveValue<string>(values, format)).IsEqualTo("yaml");
    }

    [Test]
    public async Task CreateParsing_Returns_UnknownArgument_For_Unknown_Long_Option_With_Inline_Value()
    {
        var builder = new ParsingBuilder(DefaultOptions);

        var result = Parse(builder, [new LongOptionToken("unknown", "value")]);

        if (result is not UnknownArgumentDetected unknown)
        {
            throw new InvalidOperationException($"Expected {nameof(UnknownArgumentDetected)}, got {result.GetType().FullName}.");
        }

        await Assert.That(unknown.UnknownArgument).IsEqualTo("--unknown");
    }

    [Test]
    public async Task CreateParsing_Preserves_Empty_Inline_Long_Option_Value()
    {
        var format = CreateProperty("format", typeof(string));
        var builder = new ParsingBuilder(
            DefaultOptions,
            properties: ImmutableDictionary<string, PropertyDefinition>.Empty.Add("format", format));

        var result = Parse(builder, [new LongOptionToken("format", string.Empty)]);
        var values = AssertFinishedCollection(result);

        await Assert.That(GetEffectiveValue<string>(values, format)).IsEqualTo(string.Empty);
    }

    [Test]
    public async Task CreateParsing_Aggregates_Property_ValueRange_Across_Repeated_Occurrences()
    {
        var tag = CreateProperty("tag", typeof(ImmutableArray<string>), numArgs: new ValueRange(2, 2));
        var builder = new ParsingBuilder(
            DefaultOptions,
            properties: ImmutableDictionary<string, PropertyDefinition>.Empty.Add("tag", tag));

        ImmutableArray<Token> arguments =
        [
            new LongOptionToken("tag"),
            new ArgumentOrCommandToken("a"),
            new LongOptionToken("tag"),
            new ArgumentOrCommandToken("b")
        ];

        var result = Parse(builder, arguments);
        var values = AssertFinishedCollection(result);
        var actualTags = GetEffectiveValue<ImmutableArray<string>>(values, tag);

        await Assert.That(actualTags.Length).IsEqualTo(2);
        await Assert.That(actualTags[0]).IsEqualTo("a");
        await Assert.That(actualTags[1]).IsEqualTo("b");
    }

    [Test]
    public async Task ParsingResultCollection_Separates_Explicit_And_Effective_Values()
    {
        var explicitProperty = CreateProperty("explicit", typeof(string));
        var defaultedProperty = CreateProperty("defaulted", typeof(int)) with
        {
            DefaultValueFactory = static _ => 7
        };
        var clrDefaultProperty = CreateProperty("plain", typeof(bool));
        var command = CreateCommand("serve");
        var scope = new TestScopeMetadata([command], [explicitProperty, defaultedProperty, clrDefaultProperty]);
        var collection = new ParsingResultCollection(
            null,
            null,
            scope,
            ImmutableDictionary<TypedDefinition, object?>.Empty.Add(explicitProperty, "value"));

        await Assert.That(HasExplicitValue(collection, explicitProperty, out string? explicitValue)).IsTrue();
        await Assert.That(explicitValue).IsEqualTo("value");
        await Assert.That(collection.TryGetValue(defaultedProperty, out _)).IsFalse();
        await Assert.That(GetEffectiveValue<string>(collection, explicitProperty)).IsEqualTo("value");
        await Assert.That(GetEffectiveValue<int>(collection, defaultedProperty)).IsEqualTo(7);
        await Assert.That(GetEffectiveValue<bool>(collection, clrDefaultProperty)).IsFalse();
        await Assert.That(collection.Scope.AvailableSubcommands.Contains(command)).IsTrue();
        await Assert.That(collection.Scope.AvailableTypedDefinitions.Contains(defaultedProperty)).IsTrue();
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
        await Assert.That(output).Contains("-v, --verbose <bool>");
        await Assert.That(output).Contains("--format <string>");
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
    public async Task CreateParsing_Help_Includes_Visible_Subcommand_Aliases()
    {
        var writer = new StringWriter();
        var options = CreateOptions(writer, enableStyle: false);
        var serve = CreateCommand("serve", summary: "Run the server", helpText: "Serve command help", aliases: ["srv", "s"]);
        var childBuilder = new ParsingBuilder(options);
        var builder = new ParsingBuilder(
            options,
            subcommandDefinitions: ImmutableDictionary<string, CommandDefinition>.Empty.Add("serve", serve),
            subcommands: ImmutableDictionary<string, IParsingBuilder>.Empty.Add("serve", childBuilder));

        var result = Parse(builder, [new LongOptionToken("help")]);

        if (result is not HelpFlagsDetected help)
        {
            throw new InvalidOperationException($"Expected {nameof(HelpFlagsDetected)}, got {result.GetType().FullName}.");
        }

        help.FlagAction();

        var output = writer.ToString();

        await Assert.That(output).Contains("Subcommands");
        await Assert.That(output).Contains("serve, s, srv");
        await Assert.That(output).Contains("test <subcommand>");
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
        var childResult = subcommand.ContinueParseAction();

        if (childResult is not HelpFlagsDetected help)
        {
            throw new InvalidOperationException($"Expected {nameof(HelpFlagsDetected)}, got {childResult.GetType().FullName}.");
        }

        help.FlagAction();

        var output = writer.ToString();

        await Assert.That(output).Contains("serve");
        await Assert.That(output).Contains("serve [options]");
        await Assert.That(output).Contains("Serve command help");
        await Assert.That(output).Contains("-f, --format <string>");
        await Assert.That(output).Contains("Possible values:");
        await Assert.That(output).Contains("json, yaml or any plugin-provided serializer");
        await Assert.That(output.Contains("-v, --verbose", StringComparison.Ordinal)).IsFalse();
    }

    [Test]
    public async Task CreateParsing_Help_Does_Not_Auto_Include_Enum_Possible_Values_For_Manual_Options()
    {
        var writer = new StringWriter();
        var options = CreateOptions(writer, enableStyle: false);
        var mode = CreateProperty("mode", typeof(SampleMode));
        var builder = new ParsingBuilder(
            options,
            properties: ImmutableDictionary<string, PropertyDefinition>.Empty.Add("mode", mode));

        var result = Parse(builder, [new LongOptionToken("help")]);

        if (result is not HelpFlagsDetected help)
        {
            throw new InvalidOperationException($"Expected {nameof(HelpFlagsDetected)}, got {result.GetType().FullName}.");
        }

        help.FlagAction();

        var output = writer.ToString();

        await Assert.That(output).Contains("--mode <SampleMode>");
        await Assert.That(output.Contains("Possible values:", StringComparison.Ordinal)).IsFalse();
        await Assert.That(output.Contains("Basic, Advanced", StringComparison.Ordinal)).IsFalse();
    }

    [Test]
    public async Task CreateParsing_Help_Uses_Bool_Metavar_For_Bool_Options()
    {
        var writer = new StringWriter();
        var options = CreateOptions(writer, enableStyle: false);
        var enabled = CreateProperty("enabled", typeof(bool));
        var builder = new ParsingBuilder(
            options,
            properties: ImmutableDictionary<string, PropertyDefinition>.Empty.Add("enabled", enabled));

        var result = Parse(builder, [new LongOptionToken("help")]);

        if (result is not HelpFlagsDetected help)
        {
            throw new InvalidOperationException($"Expected {nameof(HelpFlagsDetected)}, got {result.GetType().FullName}.");
        }

        help.FlagAction();

        await Assert.That(writer.ToString()).Contains("--enabled <bool>");
    }

    [Test]
    public async Task CreateParsing_Help_Prefers_Explicit_ValueName_For_Bool_Options()
    {
        var writer = new StringWriter();
        var options = CreateOptions(writer, enableStyle: false);
        var enabled = CreateProperty("enabled", typeof(bool), valueName: "enabled");
        var builder = new ParsingBuilder(
            options,
            properties: ImmutableDictionary<string, PropertyDefinition>.Empty.Add("enabled", enabled));

        var result = Parse(builder, [new LongOptionToken("help")]);

        if (result is not HelpFlagsDetected help)
        {
            throw new InvalidOperationException($"Expected {nameof(HelpFlagsDetected)}, got {result.GetType().FullName}.");
        }

        help.FlagAction();

        await Assert.That(writer.ToString()).Contains("--enabled <enabled>");
    }

    [Test]
    public async Task CreateParsing_Help_Prefers_Explicit_ValueName_For_Options()
    {
        var writer = new StringWriter();
        var options = CreateOptions(writer, enableStyle: false);
        var format = CreateProperty("format", typeof(string), valueName: "format");
        var builder = new ParsingBuilder(
            options,
            properties: ImmutableDictionary<string, PropertyDefinition>.Empty.Add("format", format));

        var result = Parse(builder, [new LongOptionToken("help")]);

        if (result is not HelpFlagsDetected help)
        {
            throw new InvalidOperationException($"Expected {nameof(HelpFlagsDetected)}, got {result.GetType().FullName}.");
        }

        help.FlagAction();

        await Assert.That(writer.ToString()).Contains("--format <format>");
    }

    [Test]
    public async Task CreateParsing_Help_Uses_Type_Display_Names_For_Options()
    {
        var writer = new StringWriter();
        var options = CreateOptions(writer, enableStyle: false);
        var requestId = CreateProperty("requestId", typeof(Guid), longAliases: ["request-id"]);
        var tags = CreateProperty("tags", typeof(ImmutableArray<string>), longAliases: ["tag"]);
        var env = CreateProperty("env", typeof(ImmutableDictionary<string, string>), shortAliases: ["e"]);
        var optionalCount = CreateProperty("optionalCount", typeof(int?), longAliases: ["optional-count"]);
        var builder = new ParsingBuilder(
            options,
            properties: ImmutableDictionary<string, PropertyDefinition>.Empty
                .Add("requestId", requestId)
                .Add("tags", tags)
                .Add("env", env)
                .Add("optionalCount", optionalCount));

        var result = Parse(builder, [new LongOptionToken("help")]);

        if (result is not HelpFlagsDetected help)
        {
            throw new InvalidOperationException($"Expected {nameof(HelpFlagsDetected)}, got {result.GetType().FullName}.");
        }

        help.FlagAction();

        var output = writer.ToString();

        await Assert.That(output).Contains("--request-id <Guid>");
        await Assert.That(output).Contains("--tag <ImmutableArray<string>>");
        await Assert.That(output).Contains("-e, --env <ImmutableDictionary<string, string>>");
        await Assert.That(output).Contains("--optional-count <int?>");
    }

    [Test]
    public async Task CreateParsing_Help_Does_Not_Auto_Include_Enum_Possible_Values_For_Arguments()
    {
        var writer = new StringWriter();
        var options = CreateOptions(writer, enableStyle: false);
        var mode = CreateArgument("mode", typeof(SampleMode), 1, 1);
        var builder = new ParsingBuilder(options, argument: [mode]);

        var result = Parse(builder, [new LongOptionToken("help")]);

        if (result is not HelpFlagsDetected help)
        {
            throw new InvalidOperationException($"Expected {nameof(HelpFlagsDetected)}, got {result.GetType().FullName}.");
        }

        help.FlagAction();

        var output = writer.ToString();

        await Assert.That(output).Contains("Arguments");
        await Assert.That(output).Contains("mode");
        await Assert.That(output.Contains("Possible values:", StringComparison.Ordinal)).IsFalse();
        await Assert.That(output.Contains("Basic, Advanced", StringComparison.Ordinal)).IsFalse();
    }

    [Test]
    public async Task CreateParsing_Help_Prefers_Explicit_Possible_Values_Over_Enum_Names()
    {
        var writer = new StringWriter();
        var options = CreateOptions(writer, enableStyle: false);
        var mode = CreateProperty("mode",
                                  typeof(SampleMode),
                                  possibleValues: new CountablePossibleValues<string>(["custom-mode"]));
        var builder = new ParsingBuilder(
            options,
            properties: ImmutableDictionary<string, PropertyDefinition>.Empty.Add("mode", mode));

        var result = Parse(builder, [new LongOptionToken("help")]);

        if (result is not HelpFlagsDetected help)
        {
            throw new InvalidOperationException($"Expected {nameof(HelpFlagsDetected)}, got {result.GetType().FullName}.");
        }

        help.FlagAction();

        var output = writer.ToString();

        await Assert.That(output).Contains("custom-mode");
        await Assert.That(output.Contains("Basic, Advanced", StringComparison.Ordinal)).IsFalse();
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

        await Assert.That(GetEffectiveValue<string>(values, format)).IsEqualTo("xml");
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
    public async Task ParsingOptions_DefaultStyle_Disables_Style_When_NoColor_Is_Present_And_Not_Empty()
    {
        foreach (var value in new[] { "1", "true", "0", "off" })
        {
            await Assert.That(ReadDefaultStyle(noColorValue: value)).IsFalse();
        }
    }

    [Test]
    public async Task ParsingOptions_DefaultStyle_Ignores_Empty_NoColor_Value()
    {
        await Assert.That(ReadDefaultStyle(noColorValue: string.Empty)).IsTrue();
    }

    [Test]
    public async Task ParsingOptions_DefaultStyle_Preserves_Legacy_NoColor_And_Ci_Behavior()
    {
        await Assert.That(ReadDefaultStyle(legacyNoColorValue: "1")).IsFalse();
        await Assert.That(ReadDefaultStyle(legacyNoColorValue: "nope")).IsTrue();
        await Assert.That(ReadDefaultStyle(ciValue: "yes")).IsFalse();
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

        await Assert.That(GetEffectiveValue<int>(values, count)).IsEqualTo(42);
        await Assert.That(output).Contains("Debug Parse Result");
        await Assert.That(output).Contains("Source: ParsingInputParser");
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

        var secondResult = firstSubcommand.ContinueParseAction();
        var secondSubcommand = AssertSubcommand(secondResult, watch);
        var secondOutput = writer.ToString();

        await Assert.That(secondOutput).Contains("Command: watch");
        await Assert.That(secondOutput).Contains("Trigger token: watch");

        var finalResult = secondSubcommand.ContinueParseAction();
        var values = AssertFinishedCollection(finalResult);
        var finalOutput = writer.ToString();

        await Assert.That(GetEffectiveValue<int>(values, interval)).IsEqualTo(5);
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
    public async Task StyleTable_Default_Preserves_Representative_Default_Styles()
    {
        await Assert.That(StyleTable.Default.HelpTitleStyle)
            .IsEqualTo(new Style(Color.Sky, Color.None, true, false, false));
        await Assert.That(StyleTable.Default.PossibleValuesValueStyle)
            .IsEqualTo(new Style(Color.Amber, Color.None, false, false, false));
        await Assert.That(StyleTable.Default.DebugFailureStyle)
            .IsEqualTo(new Style(Color.Rose, Color.None, true, false, false));
    }

    [Test]
    public async Task CreateParsing_Uses_Custom_StyleTable_For_Styled_Version_Output()
    {
        var writer = new StringWriter();
        var customProgramNameStyle = new Style(new Color(12, 34, 56, 255), Color.None, true, false, false);
        var options = CreateOptions(
            writer,
            enableStyle: true,
            styleTable: StyleTable.Default with
            {
                ProgramNameStyle = customProgramNameStyle
            });
        var builder = new ParsingBuilder(options);

        var result = Parse(builder, [new LongOptionToken("version")]);

        if (result is not VersionFlagsDetected version)
        {
            throw new InvalidOperationException($"Expected {nameof(VersionFlagsDetected)}, got {result.GetType().FullName}.");
        }

        version.FlagAction();

        var output = writer.ToString();

        await Assert.That(output).Contains($"{customProgramNameStyle.ToAnsiCode()}test{Style.ClearStyle}");
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
        return ParsingBuilder.CreateParsing(builder.ParsingOptions, arguments, builder.Build());
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

    private static IParsingResultCollection AssertRoot(IParsingResultCollection result)
    {
        var current = result;

        while (current.Parent is not null)
        {
            current = current.Parent;
        }

        await_root_is_null(current);
        return current;

        static void await_root_is_null(IParsingResultCollection node)
        {
            if (node.Command is not null)
            {
                throw new InvalidOperationException($"Expected root result to have null command, got '{node.Command.Information.Name.Value}'.");
            }
        }
    }

    private static IParsingResultCollection AssertParent(IParsingResultCollection result, CommandDefinition expected)
    {
        var parent = result.Parent
            ?? throw new InvalidOperationException($"Expected parent command '{expected.Information.Name.Value}', but result has no parent.");

        if (!EqualityComparer<CommandDefinition?>.Default.Equals(parent.Command, expected))
        {
            throw new InvalidOperationException(
                $"Expected parent command '{expected.Information.Name.Value}', got '{parent.Command?.Information.Name.Value ?? "<root>"}'.");
        }

        return parent;
    }

    private static IParsingResultCollection AssertSubcommandNode(IParsingResultCollection result, CommandDefinition expected)
    {
        if (!result.TryGetSubcommand(expected, out var child))
        {
            throw new InvalidOperationException(
                $"Expected direct subcommand '{expected.Information.Name.Value}' to be selected.");
        }

        if (!EqualityComparer<CommandDefinition?>.Default.Equals(child.Command, expected))
        {
            throw new InvalidOperationException(
                $"Expected direct subcommand node '{expected.Information.Name.Value}', got '{child.Command?.Information.Name.Value ?? "<root>"}'.");
        }

        return child;
    }

    private static bool HasExplicitValue<T>(IParsingResultCollection result, TypedDefinition definition, out T value)
    {
        if (result.TryGetValue(definition, out var rawValue) && rawValue is T typedValue)
        {
            value = typedValue;
            return true;
        }

        value = default!;
        return false;
    }

    private static T GetEffectiveValue<T>(IParsingResultCollection result, TypedDefinition definition)
    {
        return (T)result.GetEffectiveValueOrDefault(definition)!;
    }

    private static object? GetEffectiveValueOrDefault(IParsingResultCollection result, TypedDefinition definition)
    {
        return result.GetEffectiveValueOrDefault(definition);
    }

    private static DefinitionInformation CreateInformation(string name)
    {
        return new DefinitionInformation(new NameWithVisibility(name, true), new Document(name, name));
    }

    private static PropertyDefinition CreateProperty(string name,
                                                     [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)]
                                                     Type type,
                                                     bool required = false,
                                                     ImmutableArray<string> longAliases = default,
                                                     ImmutableArray<string> shortAliases = default,
                                                     PossibleValues? possibleValues = null,
                                                     string? valueName = null,
                                                     ValueRange? numArgs = null)
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
            NumArgs = numArgs ?? ValueRange.ZeroOrMore,
            ValueName = valueName,
            PossibleValues = possibleValues
        };
    }

    private static ArgumentDefinition CreateArgument(string name,
                                                     [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)]
                                                     Type type,
                                                     int minimum,
                                                     int maximum,
                                                     bool required = false)
    {
        return new ArgumentDefinition(
            CreateInformation(name),
            null,
            new ValueRange(minimum, maximum),
            type,
            required);
    }

    private static CommandDefinition CreateCommand(string name)
    {
        return CreateCommand(name, name, name);
    }

    private static CommandDefinition CreateCommand(string name, params string[] aliases)
    {
        return CreateCommand(name, name, name, aliases);
    }

    private static CommandDefinition CreateCommand(string name,
                                                  string summary,
                                                  string helpText,
                                                  params string[] aliases)
    {
        return new CommandDefinition(
            new DefinitionInformation(new NameWithVisibility(name, true), new Document(summary, helpText)),
            aliases.ToImmutableDictionary(static alias => alias,
                                          static alias => new NameWithVisibility(alias, true),
                                          StringComparer.Ordinal),
            null);
    }

    private static ParsingOptions CreateOptions(TextWriter? output = null,
                                               bool enableStyle = false,
                                               bool debug = false,
                                               StyleTable? styleTable = null)
    {
        return new ParsingOptions(
            new ProgramInformation("test", new Document("test", "test help"), new Version(1, 0), "https://example.com"),
            ParsingOptions.DefaultVersionFlags,
            ParsingOptions.DefaultHelpFlags,
            output ?? TextWriter.Null,
            enableStyle,
            debug,
            styleTable ?? StyleTable.Default);
    }

    private static bool ReadDefaultDebug(string? value)
    {
        lock (DefaultDebugEnvironmentLock)
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
    }

    private static bool ReadDefaultStyle(string? noColorValue = null, string? legacyNoColorValue = null, string? ciValue = null)
    {
        lock (DefaultStyleEnvironmentLock)
        {
            var originalNoColor = Environment.GetEnvironmentVariable("NO_COLOR");
            var originalLegacyNoColor = Environment.GetEnvironmentVariable("NOCOLOR");
            var originalCi = Environment.GetEnvironmentVariable("CI");

            try
            {
                Environment.SetEnvironmentVariable("NO_COLOR", noColorValue);
                Environment.SetEnvironmentVariable("NOCOLOR", legacyNoColorValue);
                Environment.SetEnvironmentVariable("CI", ciValue);
                ResetParsingOptionsField("_defaultStyle");
                return ParsingOptions.DefaultStyle;
            }
            finally
            {
                Environment.SetEnvironmentVariable("NO_COLOR", originalNoColor);
                Environment.SetEnvironmentVariable("NOCOLOR", originalLegacyNoColor);
                Environment.SetEnvironmentVariable("CI", originalCi);
                ResetParsingOptionsField("_defaultStyle");
            }
        }
    }

    private static void ResetParsingOptionsField(string fieldName)
    {
        var field = typeof(ParsingOptions).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException($"Field '{fieldName}' was not found.");

        field.SetValue(null, null);
    }

    private sealed record TestScopeMetadata(ImmutableArray<CommandDefinition> AvailableSubcommands,
                                            ImmutableArray<TypedDefinition> AvailableTypedDefinitions)
        : IParsingScopeMetadata;

    private enum SampleMode
    {
        Basic = 1,
        Advanced = 2
    }
}
