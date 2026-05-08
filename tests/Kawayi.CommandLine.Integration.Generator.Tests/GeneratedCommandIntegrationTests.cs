// Copyright (c) 2026 MoeGodot<me@kawayi.moe>.
// Licensed under the GNU Affero General Public License v3-or-later license.

using System.Collections.Immutable;
using Kawayi.CommandLine.Abstractions;
using Kawayi.CommandLine.Core;
using Kawayi.CommandLine.Core.Attributes;
using Kawayi.CommandLine.Extensions;
using CliProperty = Kawayi.CommandLine.Core.Attributes.PropertyAttribute;

namespace Kawayi.CommandLine.Integration.Tests;

public sealed class GeneratedCommandIntegrationTests
{
    [Test]
    public async Task CommandAttribute_Generates_Schema_Documents_Symbols_Parsing_And_Binding_Surface()
    {
        using var output = new StringWriter();
        var options = CreateOptions(output);
        var builder = SchemaCommand.ExportParsing(options);
        var schema = builder.Build();

        await Assert.That(typeof(ICliSchemaExporter).IsAssignableFrom(typeof(SchemaCommand))).IsTrue();
        await Assert.That(typeof(Kawayi.CommandLine.Abstractions.IParsable<SchemaCommand>).IsAssignableFrom(typeof(SchemaCommand))).IsTrue();
        await Assert.That(typeof(IBindable).IsAssignableFrom(typeof(SchemaCommand))).IsTrue();
        await Assert.That(SchemaCommand.Documents.Keys).Contains("Input");
        await Assert.That(SchemaCommand.Symbols.Length).IsGreaterThanOrEqualTo(6);
        await Assert.That(schema.GeneratedFrom).IsEqualTo(typeof(SchemaCommand));
        await Assert.That(schema.Argument[0].Information.Name.Value).IsEqualTo("input");
        await Assert.That(schema.Properties[new LongOptionToken("format")].Information.Name.Value).IsEqualTo("output-format");
        await Assert.That(schema.Properties[new LongOptionToken("hidden-format")].Information.Name.Value).IsEqualTo("output-format");
        await Assert.That(schema.Properties[new ShortOptionToken("f")].Information.Name.Value).IsEqualTo("output-format");
        await Assert.That(schema.Properties[new ShortOptionToken("x")].Information.Name.Value).IsEqualTo("output-format");
        await Assert.That(schema.Properties[new LongOptionToken("verbose")].NumArgs).IsEqualTo(ValueRange.ZeroOrOne);
        await Assert.That(schema.Properties[new LongOptionToken("tag")].NumArgs).IsEqualTo(ValueRange.ZeroOrMore);
        await Assert.That(schema.Properties[new LongOptionToken("count")].NumArgs).IsEqualTo(ValueRange.One);
        await Assert.That(schema.Properties[new LongOptionToken("format")].NumArgs).IsEqualTo(ValueRange.One);
        await Assert.That(schema.Properties[new LongOptionToken("format")].ValueName).IsEqualTo("format");
        await Assert.That(schema.SubcommandDefinitions[new ArgumentOrCommandToken("serve")].Information.Name.Value).IsEqualTo("serve");
        await Assert.That(schema.SubcommandDefinitions[new ArgumentOrCommandToken("srv")].Information.Name.Value).IsEqualTo("serve");
        await Assert.That(schema.SubcommandDefinitions[new ArgumentOrCommandToken("s")].Information.Name.Value).IsEqualTo("serve");

        var help = AssertHelp(SchemaCommand.CreateParsing(options, Tokenize("--help"), new SchemaCommand()));
        help.FlagAction();
        var helpText = output.ToString();

        await Assert.That(helpText).Contains("--format");
        await Assert.That(helpText).Contains("-f");
        await Assert.That(helpText).DoesNotContain("hidden-format");
        await Assert.That(helpText).DoesNotContain("-x");
    }

    [Test]
    public async Task Generated_Command_Parses_And_Binds_All_Scalar_Types()
    {
        var guid = Guid.Parse("f4602e26-b7b4-4735-b931-d58d8f6f74c2");
        var command = ParseAndBind<TypeCoverageCommand>(
            "--byte-value", "1",
            "--s-byte-value", "-2",
            "--u-short-value", "3",
            "--short-value", "-4",
            "--u-int-value", "5",
            "--int-value", "-6",
            "--u-long-value", "7",
            "--long-value", "-8",
            "--float-value", "1.5",
            "--double-value", "2.5",
            "--decimal-value", "3.5",
            "--bool-value", "true",
            "--text", "hello",
            "--mode", "advanced",
            "--id", guid.ToString(),
            "--endpoint", "https://example.com/api",
            "--date-time-value", "2026-05-08T09:10:11",
            "--date-time-offset-value", "2026-05-08T09:10:11+00:00",
            "--date-only-value", "2026-05-08",
            "--time-only-value", "09:10:11");

        await Assert.That(command.ByteValue).IsEqualTo((byte)1);
        await Assert.That(command.SByteValue).IsEqualTo((sbyte)-2);
        await Assert.That(command.UShortValue).IsEqualTo((ushort)3);
        await Assert.That(command.ShortValue).IsEqualTo((short)-4);
        await Assert.That(command.UIntValue).IsEqualTo(5u);
        await Assert.That(command.IntValue).IsEqualTo(-6);
        await Assert.That(command.ULongValue).IsEqualTo(7UL);
        await Assert.That(command.LongValue).IsEqualTo(-8L);
        await Assert.That(command.FloatValue).IsEqualTo(1.5f);
        await Assert.That(command.DoubleValue).IsEqualTo(2.5d);
        await Assert.That(command.DecimalValue).IsEqualTo(3.5m);
        await Assert.That(command.BoolValue).IsTrue();
        await Assert.That(command.Text).IsEqualTo("hello");
        await Assert.That(command.Mode).IsEqualTo(IntegrationMode.Advanced);
        await Assert.That(command.Id).IsEqualTo(guid);
        await Assert.That(command.Endpoint).IsEqualTo(new Uri("https://example.com/api"));
        await Assert.That(command.DateTimeValue).IsEqualTo(new DateTime(2026, 5, 8, 9, 10, 11));
        await Assert.That(command.DateTimeOffsetValue).IsEqualTo(new DateTimeOffset(2026, 5, 8, 9, 10, 11, TimeSpan.Zero));
        await Assert.That(command.DateOnlyValue).IsEqualTo(new DateOnly(2026, 5, 8));
        await Assert.That(command.TimeOnlyValue).IsEqualTo(new TimeOnly(9, 10, 11));
    }

    [Test]
    public async Task Generated_Command_Parses_And_Binds_List_Set_And_Map_Containers()
    {
        var command = ParseAndBind<ContainerCoverageCommand>(
            "--numbers", "1", "2",
            "--names", "alpha", "beta",
            "--queue-values", "3", "4",
            "--stack-values", "5", "6",
            "--hash-set-values", "red", "red", "blue",
            "--sorted-set-values", "9", "7",
            "--map", "answer=41", "answer=42",
            "--sorted-map", @"key\=part=value\=part", "plain=value=tail");

        await Assert.That(command.Numbers).IsEquivalentTo([1, 2]);
        await Assert.That(command.Names).IsEquivalentTo(["alpha", "beta"]);
        await Assert.That(command.QueueValues).IsEquivalentTo([3, 4]);
        await Assert.That(command.StackValues).Contains(5);
        await Assert.That(command.StackValues).Contains(6);
        await Assert.That(command.HashSetValues).IsEquivalentTo(["red", "blue"]);
        await Assert.That(command.SortedSetValues).IsEquivalentTo([7, 9]);
        await Assert.That(command.Map["answer"]).IsEqualTo(42);
        await Assert.That(command.SortedMap["key=part"]).IsEqualTo("value=part");
        await Assert.That(command.SortedMap["plain"]).IsEqualTo("value=tail");
    }

    [Test]
    public async Task Bool_Options_Support_Implicit_Explicit_And_Inline_Values()
    {
        var implicitTrue = ParseAndBind<BoolEdgeCommand>("--flag");
        var explicitFalse = ParseAndBind<BoolEdgeCommand>("--flag", "false", "payload");
        var inlineFalse = ParseAndBind<BoolEdgeCommand>("--flag=false", "payload");

        await Assert.That(implicitTrue.Flag).IsTrue();
        await Assert.That(explicitFalse.Flag).IsFalse();
        await Assert.That(explicitFalse.Tail).IsEqualTo("payload");
        await Assert.That(inlineFalse.Flag).IsFalse();
        await Assert.That(inlineFalse.Tail).IsEqualTo("payload");
    }

    [Test]
    public async Task Bool_Inline_Values_Do_Not_Probe_The_Next_Token()
    {
        var shortInline = ParseAndBind<VerboseInlineCommand>("-vfalse", "true");
        var longInline = ParseAndBind<VerboseInlineCommand>("--verbose=false", "true");

        await Assert.That(shortInline.Verbose).IsFalse();
        await Assert.That(shortInline.Tail).IsEqualTo("true");
        await Assert.That(longInline.Verbose).IsFalse();
        await Assert.That(longInline.Tail).IsEqualTo("true");
    }

    [Test]
    public async Task Bool_Options_Do_Not_Consume_NonBoolean_Values_Or_Subcommand_Names()
    {
        var nonBoolean = BoolOnlyCommand.CreateParsing(CreateOptions(), Tokenize("--flag", "maybe"), new BoolOnlyCommand());
        var subcommandResult = BoolEdgeCommand.CreateParsing(CreateOptions(), Tokenize("--flag", "run"), new BoolEdgeCommand());
        var subcommand = AssertSubcommand(subcommandResult, "run");
        var parent = AssertFinished(subcommand.ParentCommand);

        await Assert.That(nonBoolean).IsTypeOf<UnknownArgumentDetected>();
        await Assert.That((bool)parent.Properties.Single().Value).IsTrue();
    }

    [Test]
    public async Task Parser_Reports_Scalar_Unknown_And_Invalid_Value_Errors()
    {
        await Assert.That(ErrorCommand.CreateParsing(CreateOptions(), Tokenize(), new ErrorCommand())).IsTypeOf<InvalidArgumentDetected>();
        await Assert.That(ErrorCommand.CreateParsing(CreateOptions(), Tokenize("--name"), new ErrorCommand())).IsTypeOf<InvalidArgumentDetected>();
        await Assert.That(ErrorCommand.CreateParsing(CreateOptions(), Tokenize("--name", "a", "--name", "b"), new ErrorCommand())).IsTypeOf<InvalidArgumentDetected>();
        await Assert.That(ErrorCommand.CreateParsing(CreateOptions(), Tokenize("--unknown"), new ErrorCommand())).IsTypeOf<UnknownArgumentDetected>();
        await Assert.That(ErrorCommand.CreateParsing(CreateOptions(), Tokenize("--number", "abc"), new ErrorCommand())).IsTypeOf<InvalidArgumentDetected>();
    }

    [Test]
    public async Task Dash_Prefixed_Numeric_Values_And_Escaped_Strings_Parse_As_Values()
    {
        var command = ParseAndBind<DashCommand>("-1", @"\--literal", "--threshold", "-1.5");
        var knownOptionAsValue = DashCommand.CreateParsing(CreateOptions(), Tokenize("0", "literal", "--threshold", "-v"), new DashCommand());

        await Assert.That(command.Count).IsEqualTo(-1);
        await Assert.That(command.Literal).IsEqualTo("--literal");
        await Assert.That(command.Threshold).IsEqualTo(-1.5m);
        await Assert.That(knownOptionAsValue).IsTypeOf<InvalidArgumentDetected>();
    }

    [Test]
    public async Task Positional_Arguments_Are_Greedy_While_Reserving_Later_Minimums()
    {
        var command = ParseAndBind<GreedyCommand>("alpha", "beta", "tail");

        await Assert.That(command.Extras).IsEquivalentTo(["alpha", "beta"]);
        await Assert.That(command.Tail).IsEqualTo("tail");
    }

    [Test]
    public async Task Option_Terminator_Forwards_Remaining_Tokens_And_Protects_Response_File_Tokens()
    {
        var path = Path.Combine(Path.GetTempPath(), $"{nameof(GeneratedCommandIntegrationTests)}-{Guid.NewGuid():N}.txt");

        try
        {
            File.WriteAllLines(path, ["--tag", "expanded"]);
            var tokens = ResponseFileReplacer.Instance.Replace(Tokenize("payload", "--tag", "before", "--", $"@{path}", "-x"));
            var cli = ParseCli<TerminatorCommand>(tokens);
            var command = cli.Bind<TerminatorCommand>();

            await Assert.That(command.Input).IsEqualTo("payload");
            await Assert.That(command.Tag).IsEquivalentTo(["before"]);
            await Assert.That(cli.ToProgramArguments.Length).IsEqualTo(2);
            await Assert.That(cli.ToProgramArguments[0]).IsEqualTo(new ArgumentToken($"@{path}"));
            await Assert.That(cli.ToProgramArguments[1]).IsEqualTo(new ArgumentToken("-x"));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public async Task Default_Value_Factory_Validation_And_Binding_Use_Effective_Values()
    {
        var builder = EffectiveCommand.ExportParsing(CreateOptions());
        builder.Properties["retries"] = builder.Properties["retries"] with
        {
            DefaultValueFactory = static () => 3
        };
        var cli = AssertFinished(EffectiveCommand.CreateParsing(CreateOptions(), Tokenize(), new EffectiveCommand()));
        var commandWithoutAugmentation = cli.Bind<EffectiveCommand>();
        var augmentedCli = AssertFinished(CliSchemaParser.CreateParsing(CreateOptions(), Tokenize(), builder.Build()));
        var augmentedCommand = augmentedCli.Bind<EffectiveCommand>();

        await Assert.That(commandWithoutAugmentation.Retries).IsEqualTo(-1);
        await Assert.That(commandWithoutAugmentation.OptionalInitialized).IsEqualTo(42);
        await Assert.That(augmentedCommand.Retries).IsEqualTo(3);
        await Assert.That(augmentedCommand.OptionalInitialized).IsEqualTo(42);
    }

    [Test]
    public async Task Requirement_RequirementIfNull_And_Validators_Report_Errors()
    {
        var nullBuilder = NullRequirementCommand.ExportParsing(CreateOptions());
        nullBuilder.Properties["token"] = nullBuilder.Properties["token"] with
        {
            DefaultValueFactory = static () => null!
        };
        var defaultValidationBuilder = ValidationCommand.ExportParsing(CreateOptions());
        defaultValidationBuilder.Properties["positive"] = defaultValidationBuilder.Properties["positive"] with
        {
            DefaultValueFactory = static () => 0
        };

        await Assert.That(RequiredCommand.CreateParsing(CreateOptions(), Tokenize(), new RequiredCommand())).IsTypeOf<InvalidArgumentDetected>();
        await Assert.That(CliSchemaParser.CreateParsing(CreateOptions(), Tokenize(), nullBuilder.Build())).IsTypeOf<InvalidArgumentDetected>();
        await Assert.That(ValidationCommand.CreateParsing(CreateOptions(), Tokenize("--positive", "0"), new ValidationCommand())).IsTypeOf<FailedValidation>();
        await Assert.That(ValidationCommand.CreateParsing(CreateOptions(), Tokenize("--throwing", "1"), new ValidationCommand())).IsTypeOf<FailedValidation>();
        await Assert.That(CliSchemaParser.CreateParsing(CreateOptions(), Tokenize(), defaultValidationBuilder.Build())).IsTypeOf<FailedValidation>();
    }

    [Test]
    public async Task Regular_Subcommands_Are_Deferred_Recursive_And_Alias_Aware()
    {
        var result = SubcommandRoot.CreateParsing(CreateOptions(), Tokenize("srv", "localhost", "--port", "8080", "watch", "--interval", "5"), new SubcommandRoot());
        var root = AssertFinished(ContinueSubcommands(result));
        var command = root.Bind<SubcommandRoot>();

        await Assert.That(command.Serve).IsNotNull();
        await Assert.That(command.Serve!.Host).IsEqualTo("localhost");
        await Assert.That(command.Serve.Port).IsEqualTo(8080);
        await Assert.That(command.Serve.Watch).IsNotNull();
        await Assert.That(command.Serve.Watch!.Interval).IsEqualTo(5);
    }

    [Test]
    public async Task Subcommands_Are_Preferred_But_ArgumentToken_Does_Not_Match_Subcommands()
    {
        var subcommand = SubcommandRoot.CreateParsing(CreateOptions(), Tokenize("serve", "localhost"), new SubcommandRoot());
        var argumentToken = SubcommandRoot.CreateParsing(CreateOptions(), [new ArgumentToken("serve")], new SubcommandRoot());
        var command = AssertFinished(argumentToken).Bind<SubcommandRoot>();

        AssertSubcommand(subcommand, "serve");
        await Assert.That(command.Input).IsEqualTo("serve");
    }

    [Test]
    public async Task Global_Subcommand_Is_Promoted_And_Binds_From_Parent_Scope()
    {
        var cli = ParseCli<GlobalRoot>("payload", "--force");
        var command = cli.Bind<GlobalRoot>();

        await Assert.That(cli.Schema.Properties.ContainsKey(new LongOptionToken("force"))).IsTrue();
        await Assert.That(command.Input).IsEqualTo("payload");
        await Assert.That(command.Global).IsNotNull();
        await Assert.That(command.Global.Force).IsTrue();
    }

    [Test]
    public async Task Subcommand_Scope_Help_And_Version_Flags_Terminate_That_Scope()
    {
        var help = SubcommandRoot.CreateParsing(CreateOptions(), Tokenize("serve", "localhost", "--help"), new SubcommandRoot());
        var version = SubcommandRoot.CreateParsing(CreateOptions(), Tokenize("serve", "localhost", "--version"), new SubcommandRoot());

        await Assert.That(AssertSubcommand(help, "serve").ContinueParseAction()).IsTypeOf<HelpFlagsDetected>();
        await Assert.That(AssertSubcommand(version, "serve").ContinueParseAction()).IsTypeOf<VersionFlagsDetected>();
    }

    private static TCommand ParseAndBind<TCommand>(params string[] arguments)
        where TCommand : Kawayi.CommandLine.Abstractions.IParsable<TCommand>, IBindable, new()
    {
        return ParseCli<TCommand>(arguments).Bind<TCommand>();
    }

    private static Cli ParseCli<TCommand>(params string[] arguments)
        where TCommand : Kawayi.CommandLine.Abstractions.IParsable<TCommand>, new()
    {
        return ParseCli<TCommand>(Tokenize(arguments));
    }

    private static Cli ParseCli<TCommand>(ImmutableArray<Token> tokens)
        where TCommand : Kawayi.CommandLine.Abstractions.IParsable<TCommand>, new()
    {
        return AssertFinished(ContinueSubcommands(TCommand.CreateParsing(CreateOptions(), tokens, new TCommand())));
    }

    private static ParsingResult ContinueSubcommands(ParsingResult result)
    {
        while (result is Subcommand subcommand)
        {
            result = subcommand.ContinueParseAction();
        }

        return result;
    }

    private static ImmutableArray<Token> Tokenize(params string[] arguments)
    {
        return Tokenizer.Instance.Tokenize(ImmutableArray.CreateRange(arguments));
    }

    private static ParsingOptions CreateOptions(TextWriter? output = null)
    {
        return new ParsingOptions(
            new ProgramInformation("integration", new Document("summary", "help"), new Version(1, 2, 3), "https://example.com"),
            ParsingOptions.DefaultVersionFlags,
            ParsingOptions.DefaultHelpFlags,
            output ?? TextWriter.Null,
            output ?? TextWriter.Null,
            false,
            false,
            false,
            StyleTable.Default);
    }

    private static Cli AssertFinished(ParsingResult result)
    {
        return result is ParsingFinished { UntypedResult: Cli cli }
            ? cli
            : throw new InvalidOperationException($"Expected {nameof(ParsingFinished)}, got {result.GetType().FullName}.");
    }

    private static Subcommand AssertSubcommand(ParsingResult result, string name)
    {
        if (result is not Subcommand subcommand)
        {
            throw new InvalidOperationException($"Expected {nameof(Subcommand)}, got {result.GetType().FullName}.");
        }

        if (!StringComparer.Ordinal.Equals(subcommand.Definition.Information.Name.Value, name))
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
}

public enum IntegrationMode
{
    Basic = 0,
    Advanced = 1
}

/// <summary>
/// Schema command.
/// </summary>
[Command]
public partial class SchemaCommand
{
    /// <summary>
    /// Input value.
    /// </summary>
    [Argument(0)]
    [ValueRange(0, 1)]
    public string? Input { get; set; }

    /// <summary>
    /// Output format.
    /// </summary>
    [CliProperty(valueName: "format")]
    [LongAlias("format")]
    [LongAlias("hidden-format", visible: false)]
    [ShortAlias("f")]
    [ShortAlias("x", visible: false)]
    public string OutputFormat { get; set; } = string.Empty;

    /// <summary>
    /// Verbose flag.
    /// </summary>
    [CliProperty]
    [LongAlias("verbose")]
    public bool Verbose { get; set; }

    /// <summary>
    /// Tag values.
    /// </summary>
    [CliProperty]
    [LongAlias("tag")]
    public ImmutableArray<string> Tag { get; set; }

    /// <summary>
    /// Count value.
    /// </summary>
    [CliProperty]
    [LongAlias("count")]
    public int Count { get; set; }

    /// <summary>
    /// Serve command.
    /// </summary>
    [Subcommand]
    [Alias("srv")]
    [Alias("s", visible: false)]
    public SchemaServeCommand? Serve { get; private set; }
}

/// <summary>
/// Schema serve command.
/// </summary>
[Command]
public partial class SchemaServeCommand
{
}

/// <summary>
/// Scalar type coverage command.
/// </summary>
[Command]
public partial class TypeCoverageCommand
{
    /// <summary>Byte value.</summary>
    [CliProperty]
    [LongAlias("byte-value")]
    public byte ByteValue { get; set; }

    /// <summary>SByte value.</summary>
    [CliProperty]
    [LongAlias("s-byte-value")]
    public sbyte SByteValue { get; set; }

    /// <summary>UShort value.</summary>
    [CliProperty]
    [LongAlias("u-short-value")]
    public ushort UShortValue { get; set; }

    /// <summary>Short value.</summary>
    [CliProperty]
    [LongAlias("short-value")]
    public short ShortValue { get; set; }

    /// <summary>UInt value.</summary>
    [CliProperty]
    [LongAlias("u-int-value")]
    public uint UIntValue { get; set; }

    /// <summary>Int value.</summary>
    [CliProperty]
    [LongAlias("int-value")]
    public int IntValue { get; set; }

    /// <summary>ULong value.</summary>
    [CliProperty]
    [LongAlias("u-long-value")]
    public ulong ULongValue { get; set; }

    /// <summary>Long value.</summary>
    [CliProperty]
    [LongAlias("long-value")]
    public long LongValue { get; set; }

    /// <summary>Float value.</summary>
    [CliProperty]
    [LongAlias("float-value")]
    public float FloatValue { get; set; }

    /// <summary>Double value.</summary>
    [CliProperty]
    [LongAlias("double-value")]
    public double DoubleValue { get; set; }

    /// <summary>Decimal value.</summary>
    [CliProperty]
    [LongAlias("decimal-value")]
    public decimal DecimalValue { get; set; }

    /// <summary>Bool value.</summary>
    [CliProperty]
    [LongAlias("bool-value")]
    public bool BoolValue { get; set; }

    /// <summary>Text value.</summary>
    [CliProperty]
    [LongAlias("text")]
    public string Text { get; set; } = string.Empty;

    /// <summary>Mode value.</summary>
    [CliProperty]
    [LongAlias("mode")]
    public IntegrationMode Mode { get; set; }

    /// <summary>Guid value.</summary>
    [CliProperty]
    [LongAlias("id")]
    public Guid Id { get; set; }

    /// <summary>Uri value.</summary>
    [CliProperty]
    [LongAlias("endpoint")]
    public Uri Endpoint { get; set; } = new("https://example.invalid");

    /// <summary>DateTime value.</summary>
    [CliProperty]
    [LongAlias("date-time-value")]
    public DateTime DateTimeValue { get; set; }

    /// <summary>DateTimeOffset value.</summary>
    [CliProperty]
    [LongAlias("date-time-offset-value")]
    public DateTimeOffset DateTimeOffsetValue { get; set; }

    /// <summary>DateOnly value.</summary>
    [CliProperty]
    [LongAlias("date-only-value")]
    public DateOnly DateOnlyValue { get; set; }

    /// <summary>TimeOnly value.</summary>
    [CliProperty]
    [LongAlias("time-only-value")]
    public TimeOnly TimeOnlyValue { get; set; }
}

/// <summary>
/// Container coverage command.
/// </summary>
[Command]
public partial class ContainerCoverageCommand
{
    /// <summary>Number array.</summary>
    [CliProperty]
    [LongAlias("numbers")]
    public ImmutableArray<int> Numbers { get; set; }

    /// <summary>Name list.</summary>
    [CliProperty]
    [LongAlias("names")]
    public ImmutableList<string> Names { get; set; } = ImmutableList<string>.Empty;

    /// <summary>Queue values.</summary>
    [CliProperty]
    [LongAlias("queue-values")]
    public ImmutableQueue<int> QueueValues { get; set; } = ImmutableQueue<int>.Empty;

    /// <summary>Stack values.</summary>
    [CliProperty]
    [LongAlias("stack-values")]
    public ImmutableStack<int> StackValues { get; set; } = ImmutableStack<int>.Empty;

    /// <summary>Hash set values.</summary>
    [CliProperty]
    [LongAlias("hash-set-values")]
    public ImmutableHashSet<string> HashSetValues { get; set; } = ImmutableHashSet<string>.Empty;

    /// <summary>Sorted set values.</summary>
    [CliProperty]
    [LongAlias("sorted-set-values")]
    public ImmutableSortedSet<int> SortedSetValues { get; set; } = ImmutableSortedSet<int>.Empty;

    /// <summary>Map values.</summary>
    [CliProperty]
    [LongAlias("map")]
    public ImmutableDictionary<string, int> Map { get; set; } = ImmutableDictionary<string, int>.Empty;

    /// <summary>Sorted map values.</summary>
    [CliProperty]
    [LongAlias("sorted-map")]
    public ImmutableSortedDictionary<string, string> SortedMap { get; set; } = ImmutableSortedDictionary<string, string>.Empty;
}

/// <summary>
/// Bool edge command.
/// </summary>
[Command]
public partial class BoolEdgeCommand
{
    /// <summary>Tail argument.</summary>
    [Argument(0)]
    [ValueRange(0, 1)]
    public string? Tail { get; set; }

    /// <summary>Flag value.</summary>
    [CliProperty]
    [LongAlias("flag")]
    [ShortAlias("f")]
    public bool Flag { get; set; }

    /// <summary>Run command.</summary>
    [Subcommand]
    public BoolRunCommand? Run { get; private set; }
}

/// <summary>
/// Bool only command.
/// </summary>
[Command]
public partial class BoolOnlyCommand
{
    /// <summary>Flag value.</summary>
    [CliProperty]
    [LongAlias("flag")]
    public bool Flag { get; set; }
}

/// <summary>
/// Verbose inline command.
/// </summary>
[Command]
public partial class VerboseInlineCommand
{
    /// <summary>Tail argument.</summary>
    [Argument(0)]
    [ValueRange(0, 1)]
    public string? Tail { get; set; }

    /// <summary>Verbose flag.</summary>
    [CliProperty]
    [LongAlias("verbose")]
    [ShortAlias("v")]
    public bool Verbose { get; set; }
}

/// <summary>
/// Bool run command.
/// </summary>
[Command]
public partial class BoolRunCommand
{
}

/// <summary>
/// Error coverage command.
/// </summary>
[Command]
public partial class ErrorCommand
{
    /// <summary>Required value.</summary>
    [CliProperty(require: true)]
    [LongAlias("required")]
    public string Required { get; set; } = string.Empty;

    /// <summary>Name value.</summary>
    [CliProperty]
    [LongAlias("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>Number value.</summary>
    [CliProperty]
    [LongAlias("number")]
    public int Number { get; set; }
}

/// <summary>
/// Dash value command.
/// </summary>
[Command]
public partial class DashCommand
{
    /// <summary>Count argument.</summary>
    [Argument(0, require: true)]
    [ValueRange(1, 1)]
    public int Count { get; set; }

    /// <summary>Literal argument.</summary>
    [Argument(1, require: true)]
    [ValueRange(1, 1)]
    public string Literal { get; set; } = string.Empty;

    /// <summary>Threshold value.</summary>
    [CliProperty]
    [LongAlias("threshold")]
    public decimal Threshold { get; set; }

    /// <summary>Verbose flag.</summary>
    [CliProperty]
    [ShortAlias("v")]
    public bool Verbose { get; set; }
}

/// <summary>
/// Greedy positional command.
/// </summary>
[Command]
public partial class GreedyCommand
{
    /// <summary>Extra values.</summary>
    [Argument(0)]
    [ValueRange(0, int.MaxValue)]
    public ImmutableArray<string> Extras { get; set; }

    /// <summary>Tail value.</summary>
    [Argument(1, require: true)]
    [ValueRange(1, 1)]
    public string Tail { get; set; } = string.Empty;
}

/// <summary>
/// Terminator command.
/// </summary>
[Command]
public partial class TerminatorCommand
{
    /// <summary>Input value.</summary>
    [Argument(0, require: true)]
    [ValueRange(1, 1)]
    public string Input { get; set; } = string.Empty;

    /// <summary>Tag values.</summary>
    [CliProperty]
    [LongAlias("tag")]
    public ImmutableArray<string> Tag { get; set; }
}

/// <summary>
/// Effective value command.
/// </summary>
[Command]
public partial class EffectiveCommand
{
    /// <summary>Retries value.</summary>
    [CliProperty]
    [LongAlias("retries")]
    public int Retries { get; set; } = -1;

    /// <summary>Initialized value.</summary>
    [CliProperty]
    [LongAlias("optional-initialized")]
    public int OptionalInitialized { get; set; } = 42;
}

/// <summary>
/// Required command.
/// </summary>
[Command]
public partial class RequiredCommand
{
    /// <summary>Required value.</summary>
    [CliProperty(require: true)]
    [LongAlias("required")]
    public string Required { get; set; } = string.Empty;
}

/// <summary>
/// Null requirement command.
/// </summary>
[Command]
public partial class NullRequirementCommand
{
    /// <summary>Token value.</summary>
    [CliProperty(requirementIfNull: true)]
    [LongAlias("token")]
    public string? Token { get; set; }
}

/// <summary>
/// Validation command.
/// </summary>
[Command]
public partial class ValidationCommand
{
    /// <summary>Positive value.</summary>
    [CliProperty]
    [LongAlias("positive")]
    [Validator(nameof(ValidatePositive))]
    public int Positive { get; set; }

    /// <summary>Throwing value.</summary>
    [CliProperty]
    [LongAlias("throwing")]
    [Validator(nameof(ValidateThrowing))]
    public int Throwing { get; set; }

    /// <summary>Validate positive values.</summary>
    public static string? ValidatePositive(int value)
    {
        return value <= 0 ? "value must be greater than zero." : null;
    }

    /// <summary>Throw during validation.</summary>
    public static string? ValidateThrowing(int value)
    {
        throw new InvalidOperationException($"Rejected {value}.");
    }
}

/// <summary>
/// Subcommand root.
/// </summary>
[Command]
public partial class SubcommandRoot
{
    /// <summary>Input value.</summary>
    [Argument(0)]
    [ValueRange(0, 1)]
    public string? Input { get; set; }

    /// <summary>Serve command.</summary>
    [Subcommand]
    [Alias("srv")]
    [Alias("s", visible: false)]
    public SubcommandServe? Serve { get; private set; }
}

/// <summary>
/// Serve command.
/// </summary>
[Command]
public partial class SubcommandServe
{
    /// <summary>Host value.</summary>
    [Argument(0, require: true)]
    [ValueRange(1, 1)]
    public string Host { get; set; } = string.Empty;

    /// <summary>Port value.</summary>
    [CliProperty]
    [LongAlias("port")]
    public int Port { get; set; }

    /// <summary>Watch command.</summary>
    [Subcommand]
    public SubcommandWatch? Watch { get; private set; }
}

/// <summary>
/// Watch command.
/// </summary>
[Command]
public partial class SubcommandWatch
{
    /// <summary>Interval value.</summary>
    [CliProperty(require: true)]
    [LongAlias("interval")]
    public int Interval { get; set; }
}

/// <summary>
/// Global root.
/// </summary>
[Command]
public partial class GlobalRoot
{
    /// <summary>Input value.</summary>
    [Argument(0)]
    [ValueRange(0, 1)]
    public string? Input { get; set; }

    /// <summary>Global options.</summary>
    [Subcommand(global: true)]
    public GlobalOptions Global { get; private set; } = new();
}

/// <summary>
/// Global options.
/// </summary>
[Command]
public partial class GlobalOptions
{
    /// <summary>Force value.</summary>
    [CliProperty]
    [LongAlias("force")]
    public bool Force { get; set; }
}
