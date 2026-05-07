// Copyright (c) 2026 MoeGodot<me@kawayi.moe>.
// Licensed under the GNU Affero General Public License v3-or-later license.

using System.Collections;
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

public class ExportParsingGeneratorTests
{
    [Test]
    public async Task Generates_ParsingExporter_And_Builds_ParsingBuilder_From_Symbols()
    {
        const string source = """
            using Kawayi.CommandLine.Core.Attributes;

            namespace Fixtures;

            [ExportDocument]
            [ExportSymbols]
            [ExportParsing]

            public partial class ChildCommand
            {
                [Property]
                [LongAlias("force")]
                public bool ForceOption { get; set; }
            }

            [ExportDocument]
            [ExportSymbols]
            [ExportParsing]

            public partial class Command
            {
                [Argument(0)]
                [ValueRange(1, 1)]
                public string Input { get; set; } = string.Empty;

                [Property]
                [LongAlias("verbose")]
                public bool VerboseOption { get; set; }

                [Subcommand]
                public ChildCommand ServeCommand { get; } = new();
            }
            """;

        var result = RunGenerator(source, "Fixtures.Command");
        var builder = GetParsingBuilder(result, "Fixtures.Command");
        var snapshot = builder.Build();
        var symbols = GetSymbols(result, "Fixtures.Command");
        var exportedSubcommand = symbols.OfType<CommandDefinition>().Single();

        await Assert.That(HasInterface(result, "Fixtures.Command", "Kawayi.CommandLine.Abstractions.IParsingExporter")).IsTrue();
        await Assert.That(HasInterface(result, "Fixtures.Command", "Kawayi.CommandLine.Abstractions.IParsable<Fixtures.Command>")).IsTrue();
        await Assert.That(snapshot.Argument.Count).IsEqualTo(1);
        await Assert.That(snapshot.Argument[0].Information.Name.Value).IsEqualTo("input");
        await Assert.That(snapshot.Properties.ContainsKey(new LongOptionToken("verbose-option"))).IsTrue();
        await Assert.That(snapshot.SubcommandDefinitions.ContainsKey(new ArgumentOrCommandToken("serve-command"))).IsTrue();
        await Assert.That(snapshot.Subcommands.ContainsKey(new ArgumentOrCommandToken(exportedSubcommand.Information.Name.Value))).IsTrue();
        await Assert.That(snapshot.Subcommands[new ArgumentOrCommandToken(exportedSubcommand.Information.Name.Value)].Properties.ContainsKey(new LongOptionToken("force-option"))).IsTrue();
    }

    [Test]
    public async Task Generated_Parsable_CreateParsing_Composes_With_ExportParsing()
    {
        const string source = """
            using Kawayi.CommandLine.Core.Attributes;

            namespace Fixtures;

            [ExportDocument]
            [ExportSymbols]
            [ExportParsing]

            public partial class ChildCommand
            {
                [Property]
                [LongAlias("force")]
                public bool ForceOption { get; set; }
            }

            [ExportDocument]
            [ExportSymbols]
            [ExportParsing]

            public partial class Command
            {
                [Argument(0)]
                [ValueRange(1, 1)]
                public string Input { get; set; } = string.Empty;

                [Subcommand]
                public ChildCommand ServeCommand { get; } = new();
            }
            """;

        var result = RunGenerator(source, "Fixtures.Command");
        ImmutableArray<Token> arguments =
        [
            new ArgumentOrCommandToken("payload"),
            new ArgumentOrCommandToken("serve-command"),
            new LongOptionToken("force"),
            new ArgumentOrCommandToken("true")
        ];

        var parsingResult = GetGeneratedParsingResult(result, "Fixtures.Command", arguments);
        var subcommand = AssertSubcommand(parsingResult, "serve-command");
        var parentValues = AssertFinishedCollection(subcommand.ParentCommand);
        var childValues = AssertFinishedCollection(subcommand.ContinueParseAction());

        await Assert.That(parentValues.Command).IsNull();
        await Assert.That(HasExplicitString(parentValues, "input", "payload")).IsTrue();
        await Assert.That(childValues.Command?.Information.Name.Value).IsEqualTo("serve-command");
        await Assert.That(HasExplicitBoolean(childValues, "force-option")).IsTrue();
    }

    [Test]
    public async Task CommandAttribute_Generates_ParsingExporter_And_Accepts_CommandSubcommands()
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
                [Argument(0)]
                [ValueRange(1, 1)]
                public string Input { get; set; } = string.Empty;

                /// <summary>
                /// Serve summary
                /// </summary>
                [Subcommand]
                public ChildCommand ServeCommand { get; } = new();
            }
            """;

        var result = RunGenerator(source, "Fixtures.Command");
        var builder = GetParsingBuilder(result, "Fixtures.Command");
        var snapshot = builder.Build();
        var symbols = GetSymbols(result, "Fixtures.Command");
        var exportedSubcommand = symbols.OfType<CommandDefinition>().Single();

        await Assert.That(HasInterface(result, "Fixtures.Command", "Kawayi.CommandLine.Abstractions.IParsingExporter")).IsTrue();
        await Assert.That(HasInterface(result, "Fixtures.Command", "Kawayi.CommandLine.Abstractions.IParsable<Fixtures.Command>")).IsTrue();
        await Assert.That(snapshot.Argument[0].Information.Name.Value).IsEqualTo("input");
        await Assert.That(snapshot.SubcommandDefinitions.ContainsKey(new ArgumentOrCommandToken("serve-command"))).IsTrue();
        await Assert.That(snapshot.Subcommands.ContainsKey(new ArgumentOrCommandToken(exportedSubcommand.Information.Name.Value))).IsTrue();
        await Assert.That(snapshot.Subcommands[new ArgumentOrCommandToken(exportedSubcommand.Information.Name.Value)].Properties.ContainsKey(new LongOptionToken("force-option"))).IsTrue();
    }

    [Test]
    public async Task Global_Subcommand_Promotes_Parsing_Surface_And_Preserves_Nested_Subcommands()
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
                public GlobalOptionsCommand Global { get; set; } = new();
            }
            """;

        var result = RunGenerator(source, "Fixtures.Command");
        var builder = GetParsingBuilder(result, "Fixtures.Command");
        var snapshot = builder.Build();
        ImmutableArray<Token> arguments =
        [
            new ArgumentOrCommandToken("payload"),
            new LongOptionToken("force"),
            new ArgumentOrCommandToken("true"),
            new ArgumentOrCommandToken("watch"),
            new LongOptionToken("once"),
            new ArgumentOrCommandToken("true")
        ];

        var parsingResult = GetGeneratedParsingResult(result, "Fixtures.Command", arguments);
        var subcommand = AssertSubcommand(parsingResult, "watch");
        var parentValues = AssertFinishedCollection(subcommand.ParentCommand);
        var childValues = AssertFinishedCollection(subcommand.ContinueParseAction());

        await Assert.That(snapshot.Properties.ContainsKey(new LongOptionToken("force-option"))).IsTrue();
        await Assert.That(snapshot.SubcommandDefinitions.ContainsKey(new ArgumentOrCommandToken("global"))).IsFalse();
        await Assert.That(snapshot.SubcommandDefinitions.ContainsKey(new ArgumentOrCommandToken("watch"))).IsTrue();
        await Assert.That(snapshot.Subcommands.ContainsKey(new ArgumentOrCommandToken("global"))).IsFalse();
        await Assert.That(snapshot.Subcommands.ContainsKey(new ArgumentOrCommandToken("watch"))).IsTrue();
        await Assert.That(HasExplicitString(parentValues, "input", "payload")).IsTrue();
        await Assert.That(HasExplicitBoolean(parentValues, "force-option")).IsTrue();
        await Assert.That(childValues.Command?.Information.Name.Value).IsEqualTo("watch");
        await Assert.That(HasExplicitBoolean(childValues, "once")).IsTrue();
    }

    [Test]
    public async Task NonPartialType_ReportsDiagnostic()
    {
        const string source = """
            using Kawayi.CommandLine.Core.Attributes;

            namespace Fixtures;

            [ExportParsing]

            public class Command
            {
            }
            """;

        var result = RunGenerator(source, "Fixtures.Command", expectSuccessfulEmit: false);
        var diagnostic = GetSingleDiagnostic(result, "KCLG201");

        await Assert.That(diagnostic.Severity).IsEqualTo(DiagnosticSeverity.Error);
        await Assert.That(diagnostic.GetMessage()).Contains("partial");
    }

    [Test]
    public async Task Missing_SymbolExporter_ReportsDiagnostic()
    {
        const string source = """
            using Kawayi.CommandLine.Core.Attributes;

            namespace Fixtures;

            [ExportParsing]

            public partial class Command
            {
            }
            """;

        var result = RunGenerator(source, "Fixtures.Command", expectSuccessfulEmit: false);
        var diagnostic = GetSingleDiagnostic(result, "KCLG202");

        await Assert.That(diagnostic.Severity).IsEqualTo(DiagnosticSeverity.Error);
        await Assert.That(diagnostic.GetMessage()).Contains("ISymbolExporter");
    }

    [Test]
    public async Task Subcommand_Without_ParsingExporter_ReportsDiagnostic()
    {
        const string source = """
            using Kawayi.CommandLine.Core.Attributes;

            namespace Fixtures;

            public sealed class ChildCommand
            {
            }

            [ExportDocument]
            [ExportSymbols]
            [ExportParsing]

            public partial class Command
            {
                [Subcommand]
                public ChildCommand Serve { get; } = new();
            }
            """;

        var result = RunGenerator(source, "Fixtures.Command", expectSuccessfulEmit: false);
        var diagnostic = GetSingleDiagnostic(result, "KCLG203");

        await Assert.That(diagnostic.Severity).IsEqualTo(DiagnosticSeverity.Error);
        await Assert.That(diagnostic.GetMessage()).Contains("IParsingExporter");
    }

    [Test]
    public async Task Symbol_Errors_Suppress_Parsing_Output_Without_CSharp_Cascade()
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
        await Assert.That(HasInterface(result, "Fixtures.Command", "Kawayi.CommandLine.Abstractions.IParsingExporter")).IsFalse();
        await Assert.That(compilationErrors.Any(static item => item.Id.StartsWith("CS", StringComparison.Ordinal))).IsFalse();
    }

    [Test]
    public async Task Type_Without_Command_Or_ExportParsing_Does_Not_Generate_ParsingExporter()
    {
        const string source = """
            using Kawayi.CommandLine.Core.Attributes;

            namespace Fixtures;


            public partial class Command
            {
            }
            """;

        var result = RunGenerator(source, "Fixtures.Command");

        await Assert.That(HasInterface(result, "Fixtures.Command", "Kawayi.CommandLine.Abstractions.IParsingExporter")).IsFalse();
        await Assert.That(HasInterface(result, "Fixtures.Command", "Kawayi.CommandLine.Abstractions.IParsable<Fixtures.Command>")).IsFalse();
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
                new ExportParsingGenerator().AsSourceGenerator()
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
        return new GeneratorRunOutcome(outputCompilation, runResult, targetTypeMetadataName);
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
        references.Add(MetadataReference.CreateFromFile(typeof(IParsingExporter).Assembly.Location));

        return references.ToImmutable();
    }

    private static CliSchemaBuilder GetParsingBuilder(GeneratorRunOutcome outcome, string targetTypeMetadataName)
    {
        var targetType = GetEmittedType(outcome, targetTypeMetadataName);
        var method = targetType.GetMethod("ExportParsing", BindingFlags.Public | BindingFlags.Static)
            ?? throw new InvalidOperationException("Generated type does not define the expected ExportParsing method.");
        var rawValue = method.Invoke(null, [CreateOptions()])
            ?? throw new InvalidOperationException("Generated ExportParsing method returned null.");

        return (CliSchemaBuilder)rawValue;
    }

    private static Symbol[] GetSymbols(GeneratorRunOutcome outcome, string targetTypeMetadataName)
    {
        var targetType = GetEmittedType(outcome, targetTypeMetadataName);
        var property = targetType.GetProperty("Symbols", BindingFlags.Public | BindingFlags.Static)
            ?? throw new InvalidOperationException("Generated type does not define the expected Symbols property.");
        var rawValue = property.GetValue(null)
            ?? throw new InvalidOperationException("Generated Symbols property returned null.");

        return ((IEnumerable)rawValue).Cast<Symbol>().ToArray();
    }

    private static ParsingResult GetGeneratedParsingResult(
        GeneratorRunOutcome outcome,
        string targetTypeMetadataName,
        ImmutableArray<Token> arguments)
    {
        var targetType = GetEmittedType(outcome, targetTypeMetadataName);
        var method = targetType.GetMethod("CreateParsing", BindingFlags.Public | BindingFlags.Static)
            ?? throw new InvalidOperationException("Generated type does not define the expected CreateParsing method.");
        var initialState = Activator.CreateInstance(targetType)
            ?? throw new InvalidOperationException("Could not create the generated parsing target type.");
        var rawValue = method.Invoke(null, [CreateOptions(), arguments, initialState])
            ?? throw new InvalidOperationException("Generated CreateParsing method returned null.");

        return (ParsingResult)rawValue;
    }

    private static Type GetEmittedType(GeneratorRunOutcome outcome, string targetTypeMetadataName)
    {
        using var assemblyStream = new MemoryStream();
        var emitResult = outcome.Compilation.Emit(assemblyStream);
        if (!emitResult.Success)
        {
            throw new InvalidOperationException(
                "Emit failed:\n" + string.Join("\n", emitResult.Diagnostics.Select(static diagnostic => diagnostic.ToString())));
        }

        assemblyStream.Position = 0;
        var assembly = System.Reflection.Assembly.Load(assemblyStream.ToArray());
        return assembly.GetType(targetTypeMetadataName, throwOnError: true)!;
    }

    private static ParsingOptions CreateOptions()
    {
        return new ParsingOptions(
            new ProgramInformation("test", new CommandDocument("summary", "help"), new Version(1, 0), "https://example.com"),
            ParsingOptions.DefaultVersionFlags,
            ParsingOptions.DefaultHelpFlags,
            TextWriter.Null,
            false,
            false,
            StyleTable.Default);
    }

    private static Diagnostic GetSingleDiagnostic(GeneratorRunOutcome outcome, string id)
    {
        return GetGeneratorDiagnostics(outcome)
            .Single(item => item.Id == id);
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

    private static IParsingResultCollection AssertFinishedCollection(ParsingResult result)
    {
        return result is ParsingFinished { UntypedResult: IParsingResultCollection collection }
            ? collection
            : throw new InvalidOperationException($"Expected {nameof(ParsingFinished)}, got {result.GetType().FullName}.");
    }

    private static Subcommand AssertSubcommand(ParsingResult result, string expectedCommandName)
    {
        if (result is not Subcommand subcommand)
        {
            throw new InvalidOperationException($"Expected {nameof(Subcommand)}, got {result.GetType().FullName}.");
        }

        if (!string.Equals(subcommand.Definition.Information.Name.Value, expectedCommandName, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Expected subcommand '{expectedCommandName}', got '{subcommand.Definition.Information.Name.Value}'.");
        }

        return subcommand;
    }

    private static bool HasExplicitBoolean(IParsingResultCollection collection, string definitionName)
    {
        var definition = collection.Scope.AvailableTypedDefinitions
            .Single(item => string.Equals(item.Information.Name.Value, definitionName, StringComparison.Ordinal));

        return collection.TryGetValue(definition, out var rawValue) &&
               rawValue is bool typedValue &&
               typedValue;
    }

    private static bool HasExplicitString(IParsingResultCollection collection, string definitionName, string expectedValue)
    {
        var definition = collection.Scope.AvailableTypedDefinitions
            .Single(item => string.Equals(item.Information.Name.Value, definitionName, StringComparison.Ordinal));

        return collection.TryGetValue(definition, out var rawValue) &&
               rawValue is string typedValue &&
               string.Equals(typedValue, expectedValue, StringComparison.Ordinal);
    }

    private sealed record GeneratorRunOutcome(
        Compilation Compilation,
        GeneratorDriverRunResult RunResult,
        string TargetTypeMetadataName);
}
