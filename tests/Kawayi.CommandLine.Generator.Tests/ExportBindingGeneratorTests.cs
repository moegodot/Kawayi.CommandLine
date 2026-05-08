// Copyright (c) 2026 MoeGodot<me@kawayi.moe>.
// Licensed under the GNU Affero General Public License v3-or-later license.

using System.Collections.Immutable;
using System.Reflection;
using Kawayi.CommandLine.Abstractions;
using Kawayi.CommandLine.Core;
using Kawayi.CommandLine.Core.Attributes;
using Kawayi.CommandLine.Generator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using CommandDocument = Kawayi.CommandLine.Abstractions.Document;

namespace Kawayi.CommandLine.Generator.Tests;

public class ExportBindingGeneratorTests
{
    [Test]
    public async Task Generated_Binding_Assigns_Effective_Values_And_Selected_Subcommands_From_Leaf_Scope()
    {
        const string source = """
            using System.Collections.Immutable;
            using Kawayi.CommandLine.Abstractions;
            using Kawayi.CommandLine.Core.Attributes;

            namespace Fixtures;

            public enum Mode
            {
                Basic = 0,
                Advanced = 1
            }

            [ExportSymbols]
            [Bindable]
            [ExportParsing]
            [Command]
            public partial class ChildCommand : IDocumentExporter
            {
                public static ImmutableDictionary<string, Document> Documents { get; } =
                    ImmutableDictionary<string, Document>.Empty
                        .Add("ForceOption", new Document("Force summary", "Force help"));

                [Property]
                [LongAlias("force")]
                public bool ForceOption { get; set; }
            }

            [ExportSymbols]
            [Bindable]
            [ExportParsing]
            [Command]
            public partial class Command : IDocumentExporter
            {
                public static ImmutableDictionary<string, Document> Documents { get; } =
                    ImmutableDictionary<string, Document>.Empty
                        .Add("Input", new Document("Input summary", "Input help"))
                        .Add("VerboseOption", new Document("Verbose summary", "Verbose help"))
                        .Add("Mode", new Document("Mode summary", "Mode help"))
                        .Add("Retries", new Document("Retries summary", "Retries help"))
                        .Add("ServeCommand", new Document("Serve summary", "Serve help"));

                [Argument(0, require: true)]
                [ValueRange(1, 1)]
                public string Input { get; set; } = string.Empty;

                [Property]
                [LongAlias("verbose")]
                public bool VerboseOption { get; set; }

                [Property]
                [LongAlias("mode")]
                public Mode Mode { get; set; }

                [Property]
                [LongAlias("retries")]
                public int Retries { get; set; }

                [Subcommand]
                public ChildCommand? ServeCommand { get; private set; }
            }
            """;

        var result = RunGenerator(source, "Fixtures.Command");
        var builder = GetCliSchemaBuilder(result, "Fixtures.Command");
        builder.Properties["retries"] = builder.Properties["retries"] with
        {
            DefaultValueFactory = static () => 7
        };
        ImmutableArray<Token> arguments =
        [
            new ArgumentOrCommandToken("payload"),
            new LongOptionToken("verbose"),
            new ArgumentOrCommandToken("true"),
            new LongOptionToken("mode"),
            new ArgumentOrCommandToken("Advanced"),
            new ArgumentOrCommandToken("serve-command"),
            new LongOptionToken("force"),
            new ArgumentOrCommandToken("true")
        ];

        var leafScope = AssertFinishedCollection(ContinueSubcommands(CliSchemaParser.CreateParsing(CreateOptions(), arguments, builder.Build())));
        var command = Bind(result, "Fixtures.Command", GetRootCommand(leafScope));
        var serve = GetPropertyValue(command, "ServeCommand");

        await Assert.That(HasInterface(result, "Fixtures.Command", "Kawayi.CommandLine.Abstractions.IBindable")).IsTrue();
        await Assert.That(GetPropertyValue(command, "Input")).IsEqualTo("payload");
        await Assert.That((bool)GetPropertyValue(command, "VerboseOption")!).IsTrue();
        await Assert.That(GetPropertyValue(command, "Mode")?.ToString()).IsEqualTo("Advanced");
        await Assert.That(GetPropertyValue(command, "Retries")).IsEqualTo(7);
        await Assert.That(serve).IsNotNull();
        await Assert.That((bool)GetPropertyValue(serve!, "ForceOption")!).IsTrue();
    }

    [Test]
    public async Task Generated_Binding_Preserves_Initializer_For_Absent_Optional_Members()
    {
        const string source = """
            using System.Collections.Immutable;
            using Kawayi.CommandLine.Abstractions;
            using Kawayi.CommandLine.Core.Attributes;

            namespace Fixtures;

            [ExportSymbols]
            [Bindable]
            [ExportParsing]
            [Command]
            public partial class Command : IDocumentExporter
            {
                public static ImmutableDictionary<string, Document> Documents { get; } =
                    ImmutableDictionary<string, Document>.Empty
                        .Add("Input", new Document("Input summary", "Input help"))
                        .Add("Endpoint", new Document("Endpoint summary", "Endpoint help"));

                [Argument(0, require: true)]
                [ValueRange(1, 1)]
                public string Input { get; set; } = string.Empty;

                [Property]
                [LongAlias("endpoint")]
                public string Endpoint { get; set; } = "https://example.invalid";
            }
            """;

        var result = RunGenerator(source, "Fixtures.Command");
        var builder = GetCliSchemaBuilder(result, "Fixtures.Command");
        ImmutableArray<Token> arguments =
        [
            new ArgumentOrCommandToken("payload")
        ];

        var scope = AssertFinishedCollection(CliSchemaParser.CreateParsing(CreateOptions(), arguments, builder.Build()));
        var command = Bind(result, "Fixtures.Command", scope);

        await Assert.That(GetPropertyValue(command, "Input")).IsEqualTo("payload");
        await Assert.That(GetPropertyValue(command, "Endpoint")).IsEqualTo("https://example.invalid");
    }

    [Test]
    public async Task Generated_Binding_Assigns_Null_For_Absent_Subcommands()
    {
        const string source = """
            using System.Collections.Immutable;
            using Kawayi.CommandLine.Abstractions;
            using Kawayi.CommandLine.Core.Attributes;

            namespace Fixtures;

            [ExportSymbols]
            [Bindable]
            [ExportParsing]
            [Command]
            public partial class ChildCommand : IDocumentExporter
            {
                public static ImmutableDictionary<string, Document> Documents { get; } =
                    ImmutableDictionary<string, Document>.Empty;
            }

            [ExportSymbols]
            [Bindable]
            [ExportParsing]
            [Command]
            public partial class Command : IDocumentExporter
            {
                public static ImmutableDictionary<string, Document> Documents { get; } =
                    ImmutableDictionary<string, Document>.Empty
                        .Add("Input", new Document("Input summary", "Input help"))
                        .Add("ServeCommand", new Document("Serve summary", "Serve help"));

                [Argument(0, require: true)]
                [ValueRange(1, 1)]
                public string Input { get; set; } = string.Empty;

                [Subcommand]
                public ChildCommand? ServeCommand { get; private set; }
            }
            """;

        var result = RunGenerator(source, "Fixtures.Command");
        var builder = GetCliSchemaBuilder(result, "Fixtures.Command");
        ImmutableArray<Token> arguments =
        [
            new ArgumentOrCommandToken("payload")
        ];

        var scope = AssertFinishedCollection(CliSchemaParser.CreateParsing(CreateOptions(), arguments, builder.Build()));
        var command = Bind(result, "Fixtures.Command", scope);

        await Assert.That(GetPropertyValue(command, "Input")).IsEqualTo("payload");
        await Assert.That(GetPropertyValue(command, "ServeCommand")).IsNull();
    }

    [Test]
    public async Task Global_Subcommand_Is_Always_Instantiated_And_Binds_From_Parent_Scope()
    {
        const string source = """
            using Kawayi.CommandLine.Core.Attributes;

            namespace Fixtures;

            [Command]
            public partial class GlobalOptionsCommand
            {
                /// <summary>
                /// Force summary
                /// </summary>
                [Property]
                [LongAlias("force")]
                public bool ForceOption { get; set; }
            }

            [Command]
            public partial class Command
            {
                /// <summary>
                /// Input summary
                /// </summary>
                [Argument(0, require: true)]
                [ValueRange(1, 1)]
                public string Input { get; set; } = string.Empty;

                /// <summary>
                /// Global summary
                /// </summary>
                [Subcommand(global: true)]
                public GlobalOptionsCommand Global { get; private set; } = new();
            }
            """;

        var result = RunGenerator(source, "Fixtures.Command");
        var builder = GetCliSchemaBuilder(result, "Fixtures.Command");
        ImmutableArray<Token> arguments =
        [
            new ArgumentOrCommandToken("payload"),
            new LongOptionToken("force"),
            new ArgumentOrCommandToken("true")
        ];

        var scope = AssertFinishedCollection(CliSchemaParser.CreateParsing(CreateOptions(), arguments, builder.Build()));
        var command = Bind(result, "Fixtures.Command", scope);
        var global = GetPropertyValue(command, "Global");

        await Assert.That(GetPropertyValue(command, "Input")).IsEqualTo("payload");
        await Assert.That(global).IsNotNull();
        await Assert.That((bool)GetPropertyValue(global!, "ForceOption")!).IsTrue();
    }

    [Test]
    public async Task Global_Subcommand_Binds_From_Leaf_Scope_And_Preserves_Nested_Selected_Subcommands()
    {
        const string source = """
            using Kawayi.CommandLine.Core.Attributes;

            namespace Fixtures;

            [Command]
            public partial class WatchCommand
            {
                /// <summary>
                /// Once summary
                /// </summary>
                [Property]
                [LongAlias("once")]
                public bool Once { get; set; }
            }

            [Command]
            public partial class GlobalOptionsCommand
            {
                /// <summary>
                /// Force summary
                /// </summary>
                [Property]
                [LongAlias("force")]
                public bool ForceOption { get; set; }

                /// <summary>
                /// Watch summary
                /// </summary>
                [Subcommand]
                public WatchCommand? Watch { get; private set; }
            }

            [Command]
            public partial class Command
            {
                /// <summary>
                /// Input summary
                /// </summary>
                [Argument(0, require: true)]
                [ValueRange(1, 1)]
                public string Input { get; set; } = string.Empty;

                /// <summary>
                /// Global summary
                /// </summary>
                [Subcommand(global: true)]
                public GlobalOptionsCommand Global { get; private set; } = new();
            }
            """;

        var result = RunGenerator(source, "Fixtures.Command");
        var builder = GetCliSchemaBuilder(result, "Fixtures.Command");
        ImmutableArray<Token> arguments =
        [
            new ArgumentOrCommandToken("payload"),
            new LongOptionToken("force"),
            new ArgumentOrCommandToken("true"),
            new ArgumentOrCommandToken("watch"),
            new LongOptionToken("once"),
            new ArgumentOrCommandToken("true")
        ];

        var leafScope = AssertFinishedCollection(ContinueSubcommands(CliSchemaParser.CreateParsing(CreateOptions(), arguments, builder.Build())));
        var command = Bind(result, "Fixtures.Command", GetRootCommand(leafScope));
        var global = GetPropertyValue(command, "Global");
        var watch = GetPropertyValue(global!, "Watch");

        await Assert.That(GetPropertyValue(command, "Input")).IsEqualTo("payload");
        await Assert.That(global).IsNotNull();
        await Assert.That((bool)GetPropertyValue(global!, "ForceOption")!).IsTrue();
        await Assert.That(watch).IsNotNull();
        await Assert.That((bool)GetPropertyValue(watch!, "Once")!).IsTrue();
    }

    [Test]
    public async Task CommandAttribute_Generates_Binding_And_Accepts_CommandSubcommands()
    {
        const string source = """
            using Kawayi.CommandLine.Core.Attributes;

            namespace Fixtures;

            [Command]
            public partial class ChildCommand
            {
                /// <summary>
                /// Force summary
                /// </summary>
                [Property]
                [LongAlias("force")]
                public bool ForceOption { get; set; }
            }

            [Command]
            public partial class Command
            {
                /// <summary>
                /// Input summary
                /// </summary>
                [Argument(0, require: true)]
                [ValueRange(1, 1)]
                public string Input { get; set; } = string.Empty;

                /// <summary>
                /// Serve summary
                /// </summary>
                [Subcommand]
                public ChildCommand? ServeCommand { get; private set; }
            }
            """;

        var result = RunGenerator(source, "Fixtures.Command");
        var builder = GetCliSchemaBuilder(result, "Fixtures.Command");
        ImmutableArray<Token> arguments =
        [
            new ArgumentOrCommandToken("payload"),
            new ArgumentOrCommandToken("serve-command"),
            new LongOptionToken("force"),
            new ArgumentOrCommandToken("true")
        ];

        var leafScope = AssertFinishedCollection(ContinueSubcommands(CliSchemaParser.CreateParsing(CreateOptions(), arguments, builder.Build())));
        var command = Bind(result, "Fixtures.Command", GetRootCommand(leafScope));
        var serve = GetPropertyValue(command, "ServeCommand");

        await Assert.That(HasInterface(result, "Fixtures.Command", "Kawayi.CommandLine.Abstractions.IBindable")).IsTrue();
        await Assert.That(GetPropertyValue(command, "Input")).IsEqualTo("payload");
        await Assert.That(serve).IsNotNull();
        await Assert.That((bool)GetPropertyValue(serve!, "ForceOption")!).IsTrue();
    }

    [Test]
    public async Task Binding_Uses_Current_Scope_And_Recursively_Binds_Selected_Subcommands()
    {
        const string source = """
            using Kawayi.CommandLine.Core.Attributes;

            namespace Fixtures;

            [Command]
            public partial class ChildCommand
            {
                /// <summary>
                /// Child input summary
                /// </summary>
                [Argument(0, require: true)]
                [ValueRange(1, 1)]
                public string Input { get; set; } = string.Empty;

                /// <summary>
                /// Child value summary
                /// </summary>
                [Property]
                [LongAlias("value")]
                public string Value { get; set; } = string.Empty;
            }

            [Command]
            public partial class Command
            {
                /// <summary>
                /// Root input summary
                /// </summary>
                [Argument(0, require: true)]
                [ValueRange(1, 1)]
                public string Input { get; set; } = string.Empty;

                /// <summary>
                /// Root value summary
                /// </summary>
                [Property]
                [LongAlias("value")]
                public string Value { get; set; } = string.Empty;

                /// <summary>
                /// Serve summary
                /// </summary>
                [Subcommand]
                public ChildCommand? Serve { get; private set; }
            }
            """;

        var result = RunGenerator(source, "Fixtures.Command");
        var builder = GetCliSchemaBuilder(result, "Fixtures.Command");
        ImmutableArray<Token> arguments =
        [
            new ArgumentOrCommandToken("root-input"),
            new LongOptionToken("value"),
            new ArgumentOrCommandToken("root-value"),
            new ArgumentOrCommandToken("serve"),
            new ArgumentOrCommandToken("child-input"),
            new LongOptionToken("value"),
            new ArgumentOrCommandToken("child-value")
        ];

        var leafScope = AssertFinishedCollection(ContinueSubcommands(CliSchemaParser.CreateParsing(CreateOptions(), arguments, builder.Build())));
        var command = Bind(result, "Fixtures.Command", GetRootCommand(leafScope));
        var serve = GetPropertyValue(command, "Serve");

        await Assert.That(GetPropertyValue(command, "Input")).IsEqualTo("root-input");
        await Assert.That(GetPropertyValue(command, "Value")).IsEqualTo("root-value");
        await Assert.That(serve).IsNotNull();
        await Assert.That(GetPropertyValue(serve!, "Input")).IsEqualTo("child-input");
        await Assert.That(GetPropertyValue(serve!, "Value")).IsEqualTo("child-value");
    }

    [Test]
    public async Task Binding_Root_From_Continued_Subcommand_Result_Uses_Composed_Subcommand_Tree()
    {
        const string source = """
            using Kawayi.CommandLine.Core.Attributes;

            namespace Fixtures;

            [Command]
            public partial class ChildCommand
            {
                /// <summary>
                /// Force summary
                /// </summary>
                [Property]
                [LongAlias("force")]
                public bool Force { get; set; }
            }

            [Command]
            public partial class Command
            {
                /// <summary>
                /// Input summary
                /// </summary>
                [Argument(0, require: true)]
                [ValueRange(1, 1)]
                public string Input { get; set; } = string.Empty;

                /// <summary>
                /// Serve summary
                /// </summary>
                [Subcommand]
                public ChildCommand? Serve { get; private set; }
            }
            """;

        var result = RunGenerator(source, "Fixtures.Command");
        var builder = GetCliSchemaBuilder(result, "Fixtures.Command");
        ImmutableArray<Token> arguments =
        [
            new ArgumentOrCommandToken("payload"),
            new ArgumentOrCommandToken("serve"),
            new LongOptionToken("force"),
            new ArgumentOrCommandToken("true")
        ];

        var rootScope = AssertFinishedCollection(ContinueSubcommands(CliSchemaParser.CreateParsing(CreateOptions(), arguments, builder.Build())));
        var command = Bind(result, "Fixtures.Command", rootScope);
        var serve = GetPropertyValue(command, "Serve");

        await Assert.That(GetPropertyValue(command, "Input")).IsEqualTo("payload");
        await Assert.That(serve).IsNotNull();
        await Assert.That((bool)GetPropertyValue(serve!, "Force")!).IsTrue();
    }

    [Test]
    public async Task Binding_Throws_For_Mismatched_Root_Command_Type()
    {
        const string source = """
            using Kawayi.CommandLine.Core.Attributes;

            namespace Fixtures;

            [Command]
            public partial class FirstCommand
            {
                /// <summary>
                /// Input summary
                /// </summary>
                [Argument(0, require: true)]
                [ValueRange(1, 1)]
                public string Input { get; set; } = string.Empty;
            }

            [Command]
            public partial class SecondCommand
            {
                /// <summary>
                /// Input summary
                /// </summary>
                [Argument(0, require: true)]
                [ValueRange(1, 1)]
                public string Input { get; set; } = string.Empty;
            }
            """;

        var result = RunGenerator(source, "Fixtures.FirstCommand");
        var builder = GetCliSchemaBuilder(result, "Fixtures.FirstCommand");
        ImmutableArray<Token> arguments =
        [
            new ArgumentOrCommandToken("payload")
        ];

        var scope = AssertFinishedCollection(CliSchemaParser.CreateParsing(CreateOptions(), arguments, builder.Build()));

        await Assert.That(() => Bind(result, "Fixtures.SecondCommand", scope)).Throws<ArgumentException>();
    }

    [Test]
    public async Task Symbol_Errors_Suppress_Binding_Output_Without_CSharp_Cascade()
    {
        const string source = """
            using Kawayi.CommandLine.Core.Attributes;

            namespace Fixtures;

            [Command]
            public partial class Command
            {
                /// <summary>
                /// Input summary
                /// </summary>
                [Argument(0)]
                public string? Input { get; set; }
            }
            """;

        var result = RunGenerator(source, "Fixtures.Command", expectSuccessfulEmit: false);
        var generatorDiagnostics = GetGeneratorDiagnostics(result);
        var compilationErrors = result.Compilation.GetDiagnostics()
            .Where(static item => item.Severity == DiagnosticSeverity.Error)
            .ToArray();

        await Assert.That(generatorDiagnostics.Any(static item => item.Id == "KCLG104")).IsTrue();
        await Assert.That(HasInterface(result, "Fixtures.Command", "Kawayi.CommandLine.Abstractions.IBindable")).IsFalse();
        await Assert.That(compilationErrors.Any(static item => item.Id.StartsWith("CS", StringComparison.Ordinal))).IsFalse();
    }

    [Test]
    public async Task Invalid_Bindable_Shapes_Report_Diagnostics()
    {
        const string source = """
            using System.Collections.Immutable;
            using Kawayi.CommandLine.Abstractions;
            using Kawayi.CommandLine.Core.Attributes;

            namespace Fixtures;

            [Bindable]
            public class NonPartialCommand
            {
            }

            [Bindable]
            public partial class MissingSymbolsCommand
            {
            }

            [ExportSymbols]
            [Bindable]
            public partial class UnassignableCommand : IDocumentExporter
            {
                public static ImmutableDictionary<string, Document> Documents { get; } =
                    ImmutableDictionary<string, Document>.Empty
                        .Add("Input", new Document("Input summary", "Input help"));

                [Argument(0)]
                [ValueRange(0, 1)]
                public string? Input { get; }
            }

            public sealed class NonBindableChild
            {
            }

            [ExportSymbols]
            [Bindable]
            public partial class InvalidSubcommandCommand : IDocumentExporter
            {
                public static ImmutableDictionary<string, Document> Documents { get; } =
                    ImmutableDictionary<string, Document>.Empty
                        .Add("Serve", new Document("Serve summary", "Serve help"));

                [Subcommand]
                public NonBindableChild? Serve { get; set; }
            }

            public sealed class PrivateCtorChild : IBindable
            {
                private PrivateCtorChild()
                {
                }

                public void Bind(Cli results)
                {
                }
            }

            [ExportSymbols]
            [Bindable]
            public partial class MissingCtorCommand : IDocumentExporter
            {
                public static ImmutableDictionary<string, Document> Documents { get; } =
                    ImmutableDictionary<string, Document>.Empty
                        .Add("Serve", new Document("Serve summary", "Serve help"));

                [Subcommand]
                public PrivateCtorChild? Serve { get; set; }
            }
            """;

        var result = RunGenerator(source, "Fixtures.MissingCtorCommand", expectSuccessfulEmit: false);
        var diagnostics = GetGeneratorDiagnostics(result);

        await Assert.That(diagnostics.Any(static item => item.Id == "KCLG301")).IsTrue();
        await Assert.That(diagnostics.Any(static item => item.Id == "KCLG302")).IsTrue();
        await Assert.That(diagnostics.Any(static item => item.Id == "KCLG303")).IsTrue();
        await Assert.That(diagnostics.Any(static item => item.Id == "KCLG304")).IsTrue();
        await Assert.That(diagnostics.Any(static item => item.Id == "KCLG305")).IsTrue();
    }

    private static GeneratorRunOutcome RunGenerator(
        string source,
        string targetTypeMetadataName,
        bool expectSuccessfulEmit = true)
    {
        var parseOptions = CSharpParseOptions.Default.WithDocumentationMode(DocumentationMode.Diagnose);
        var syntaxTree = CSharpSyntaxTree.ParseText(source, parseOptions);
        var compilation = CSharpCompilation.Create(
            assemblyName: $"GeneratorTests_{Guid.NewGuid():N}",
            syntaxTrees: [syntaxTree],
            references: CreateMetadataReferences(),
            options: new(OutputKind.DynamicallyLinkedLibrary));

        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            [
                new ExportDocumentGenerator().AsSourceGenerator(),
                new ExportSymbolsGenerator().AsSourceGenerator(),
                new ExportParsingGenerator().AsSourceGenerator(),
                new ExportBindingGenerator().AsSourceGenerator()
            ]);
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var generatorDiagnostics);

        if (expectSuccessfulEmit)
        {
            var compilationDiagnostics = outputCompilation.GetDiagnostics()
                .Concat(generatorDiagnostics)
                .Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
                .ToArray();

            if (compilationDiagnostics.Length > 0)
            {
                throw new InvalidOperationException(
                    "Compilation failed:\n" + string.Join("\n", compilationDiagnostics.Select(static diagnostic => diagnostic.ToString())));
            }
        }

        var runResult = driver.GetRunResult();
        return new GeneratorRunOutcome(outputCompilation, runResult, targetTypeMetadataName, new Lazy<System.Reflection.Assembly>(() => LoadAssembly(outputCompilation)));
    }

    private static ImmutableArray<MetadataReference> CreateMetadataReferences()
    {
        var references = ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES"))!
            .Split(Path.PathSeparator)
            .Select(static path => MetadataReference.CreateFromFile(path))
            .Cast<MetadataReference>()
            .ToImmutableArray()
            .ToBuilder();

        references.Add(MetadataReference.CreateFromFile(typeof(CommandAttribute).Assembly.Location));
        references.Add(MetadataReference.CreateFromFile(typeof(CliSchemaParser).Assembly.Location));
        references.Add(MetadataReference.CreateFromFile(typeof(IBindable).Assembly.Location));

        return references.ToImmutable();
    }

    private static CliSchemaBuilder GetCliSchemaBuilder(GeneratorRunOutcome outcome, string targetTypeMetadataName)
    {
        var targetType = GetEmittedType(outcome, targetTypeMetadataName);
        var method = targetType.GetMethod("ExportParsing", BindingFlags.Public | BindingFlags.Static)
            ?? throw new InvalidOperationException("Generated type does not define the expected ExportParsing method.");
        var rawValue = method.Invoke(null, [CreateOptions()])
            ?? throw new InvalidOperationException("Generated ExportParsing method returned null.");

        return (CliSchemaBuilder)rawValue;
    }

    private static object Bind(
        GeneratorRunOutcome outcome,
        string targetTypeMetadataName,
        Cli result)
    {
        var targetType = GetEmittedType(outcome, targetTypeMetadataName);
        var instance = Activator.CreateInstance(targetType)
            ?? throw new InvalidOperationException("Could not create the generated binding target type.");
        ((IBindable)instance).Bind(result, new BindingOptions());
        return instance;
    }

    private static Type GetEmittedType(GeneratorRunOutcome outcome, string targetTypeMetadataName)
    {
        return outcome.LoadedAssembly.Value.GetType(targetTypeMetadataName, throwOnError: true)!;
    }

    private static System.Reflection.Assembly LoadAssembly(Compilation compilation)
    {
        using var assemblyStream = new MemoryStream();
        var emitResult = compilation.Emit(assemblyStream);
        if (!emitResult.Success)
        {
            throw new InvalidOperationException(
                "Emit failed:\n" + string.Join("\n", emitResult.Diagnostics.Select(static diagnostic => diagnostic.ToString())));
        }

        assemblyStream.Position = 0;
        return System.Reflection.Assembly.Load(assemblyStream.ToArray());
    }

    private static ParsingResult ContinueSubcommands(ParsingResult result)
    {
        var current = result;

        while (current is Subcommand subcommand)
        {
            current = subcommand.ContinueParseAction();
        }

        return current;
    }

    private static Cli AssertFinishedCollection(ParsingResult result)
    {
        return result is ParsingFinished { UntypedResult: Cli collection }
            ? collection
            : throw new InvalidOperationException($"Expected {nameof(ParsingFinished)}, got {result.GetType().FullName}.");
    }

    private static Cli GetRootCommand(Cli result)
    {
        var current = result;

        while (current.ParentCommand is not null)
        {
            current = current.ParentCommand;
        }

        return current;
    }

    private static object? GetPropertyValue(object instance, string propertyName)
    {
        return instance.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance)!
            .GetValue(instance);
    }

    private static ParsingOptions CreateOptions()
    {
        return new ParsingOptions(
            new ProgramInformation("test", new CommandDocument("summary", "help"), new Version(1, 0), "https://example.com"),
            ParsingOptions.DefaultVersionFlags,
            ParsingOptions.DefaultHelpFlags,
            TextWriter.Null,
            TextWriter.Null,
            false,
            false,
            false,
            StyleTable.Default);
    }

    private static ImmutableArray<Diagnostic> GetGeneratorDiagnostics(GeneratorRunOutcome outcome)
    {
        return [.. outcome.RunResult.Results.SelectMany(static generatorResult => generatorResult.Diagnostics)];
    }

    private static bool HasInterface(
        GeneratorRunOutcome outcome,
        string targetTypeMetadataName,
        string interfaceMetadataName)
    {
        var targetType = outcome.Compilation.GetTypeByMetadataName(targetTypeMetadataName);
        return targetType?.AllInterfaces.Any(item => item.ToDisplayString() == interfaceMetadataName) == true;
    }

    private sealed record GeneratorRunOutcome(
        Compilation Compilation,
        GeneratorDriverRunResult RunResult,
        string TargetTypeMetadataName,
        Lazy<System.Reflection.Assembly> LoadedAssembly);
}
