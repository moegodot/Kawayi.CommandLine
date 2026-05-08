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
    public async Task Generates_CliSchemaExporter_And_Builds_CliSchemaBuilder_From_Symbols()
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
        var builder = GetCliSchemaBuilder(result, "Fixtures.Command");
        var snapshot = builder.Build();
        var symbols = GetSymbols(result, "Fixtures.Command");
        var exportedSubcommand = symbols.OfType<CommandDefinition>().Single();

        await Assert.That(HasInterface(result, "Fixtures.Command", "Kawayi.CommandLine.Abstractions.ICliSchemaExporter")).IsTrue();
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
        var rootValues = AssertFinishedCollection(subcommand.ContinueParseAction());
        var childValues = rootValues.Subcommands[subcommand.Definition];

        await Assert.That(parentValues.CurrentCommandDefinition).IsNull();
        await Assert.That(HasExplicitString(parentValues, "input", "payload")).IsTrue();
        await Assert.That(childValues.CurrentCommandDefinition?.Information.Name.Value).IsEqualTo("serve-command");
        await Assert.That(HasExplicitBoolean(childValues, "force-option")).IsTrue();
    }

    [Test]
    public async Task CommandAttribute_Generates_CliSchemaExporter_And_Accepts_CommandSubcommands()
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
        var builder = GetCliSchemaBuilder(result, "Fixtures.Command");
        var snapshot = builder.Build();
        var symbols = GetSymbols(result, "Fixtures.Command");
        var exportedSubcommand = symbols.OfType<CommandDefinition>().Single();

        await Assert.That(HasInterface(result, "Fixtures.Command", "Kawayi.CommandLine.Abstractions.ICliSchemaExporter")).IsTrue();
        await Assert.That(HasInterface(result, "Fixtures.Command", "Kawayi.CommandLine.Abstractions.IParsable<Fixtures.Command>")).IsTrue();
        await Assert.That(snapshot.Argument[0].Information.Name.Value).IsEqualTo("input");
        await Assert.That(snapshot.SubcommandDefinitions.ContainsKey(new ArgumentOrCommandToken("serve-command"))).IsTrue();
        await Assert.That(snapshot.Subcommands.ContainsKey(new ArgumentOrCommandToken(exportedSubcommand.Information.Name.Value))).IsTrue();
        await Assert.That(snapshot.Subcommands[new ArgumentOrCommandToken(exportedSubcommand.Information.Name.Value)].Properties.ContainsKey(new LongOptionToken("force-option"))).IsTrue();
    }

    [Test]
    public async Task CommandAttribute_On_Derived_Command_Preserves_BaseType_And_Generates_Parsing_Surface()
    {
        const string source = """
            using Kawayi.CommandLine.Core.Attributes;

            namespace Fixtures;

            public class BarCommand
            {
            }

            [Command]
            public partial class FooCommand : BarCommand
            {
                /// <summary>
                /// Verbose summary
                /// </summary>
                [Property]
                [LongAlias("verbose")]
                public bool Verbose { get; set; }
            }
            """;

        var result = RunGenerator(source, "Fixtures.FooCommand");
        var targetType = result.Compilation.GetTypeByMetadataName("Fixtures.FooCommand")
            ?? throw new InvalidOperationException("Expected FooCommand to be available in the generated compilation.");
        var snapshot = GetCliSchemaBuilder(result, "Fixtures.FooCommand").Build();
        var diagnostics = GetGeneratorDiagnostics(result);

        await Assert.That(targetType.BaseType?.ToDisplayString()).IsEqualTo("Fixtures.BarCommand");
        await Assert.That(HasInterface(result, "Fixtures.FooCommand", "Kawayi.CommandLine.Abstractions.ISymbolExporter")).IsTrue();
        await Assert.That(HasInterface(result, "Fixtures.FooCommand", "Kawayi.CommandLine.Abstractions.ICliSchemaExporter")).IsTrue();
        await Assert.That(HasInterface(result, "Fixtures.FooCommand", "Kawayi.CommandLine.Abstractions.IParsable<Fixtures.FooCommand>")).IsTrue();
        await Assert.That(snapshot.Properties.ContainsKey(new LongOptionToken("verbose"))).IsTrue();
        await Assert.That(diagnostics.Any(static item => item.Severity == DiagnosticSeverity.Error)).IsFalse();
    }

    [Test]
    public async Task CommandAttribute_On_Derived_Command_Merges_Base_And_Derived_Parsing_Schemas()
    {
        const string source = """
            using Kawayi.CommandLine.Core.Attributes;

            namespace Fixtures;

            [Command]
            public partial class BarChildCommand
            {
                /// <summary>
                /// Bar child flag summary
                /// </summary>
                [Property]
                [LongAlias("bar-child-flag")]
                public bool BarChildFlag { get; set; }
            }

            [Command]
            public partial class FooChildCommand
            {
                /// <summary>
                /// Foo child flag summary
                /// </summary>
                [Property]
                [LongAlias("foo-child-flag")]
                public bool FooChildFlag { get; set; }
            }

            [Command]
            public partial class BarCommand
            {
                /// <summary>
                /// Bar input summary
                /// </summary>
                [Argument(0)]
                [ValueRange(1, 1)]
                public string BarInput { get; set; } = string.Empty;

                /// <summary>
                /// Bar flag summary
                /// </summary>
                [Property]
                [LongAlias("bar-flag")]
                public bool BarFlag { get; set; }

                /// <summary>
                /// Bar run summary
                /// </summary>
                [Subcommand]
                public BarChildCommand? BarRun { get; set; }
            }

            [Command]
            public partial class FooCommand : BarCommand
            {
                /// <summary>
                /// Foo input summary
                /// </summary>
                [Argument(0)]
                [ValueRange(1, 1)]
                public string FooInput { get; set; } = string.Empty;

                /// <summary>
                /// Foo flag summary
                /// </summary>
                [Property]
                [LongAlias("foo-flag")]
                public bool FooFlag { get; set; }

                /// <summary>
                /// Foo run summary
                /// </summary>
                [Subcommand]
                public FooChildCommand? FooRun { get; set; }
            }
            """;

        var result = RunGenerator(source, "Fixtures.FooCommand");
        var barSnapshot = GetCliSchemaBuilder(result, "Fixtures.BarCommand").Build();
        var fooSnapshot = GetCliSchemaBuilder(result, "Fixtures.FooCommand").Build();
        var parsingResult = GetGeneratedParsingResult(
            result,
            "Fixtures.FooCommand",
            [
                new ArgumentOrCommandToken("bar-value"),
                new ArgumentOrCommandToken("foo-value"),
                new LongOptionToken("bar-flag"),
                new ArgumentOrCommandToken("true"),
                new LongOptionToken("foo-flag"),
                new ArgumentOrCommandToken("true")
            ]);
        var command = AssertFinishedCollection(parsingResult);

        await Assert.That(barSnapshot.Argument.Select(static item => item.Information.Name.Value)).IsEquivalentTo(["bar-input"]);
        await Assert.That(barSnapshot.Properties.ContainsKey(new LongOptionToken("bar-flag"))).IsTrue();
        await Assert.That(barSnapshot.Properties.ContainsKey(new LongOptionToken("foo-flag"))).IsFalse();
        await Assert.That(barSnapshot.SubcommandDefinitions.ContainsKey(new ArgumentOrCommandToken("bar-run"))).IsTrue();
        await Assert.That(barSnapshot.SubcommandDefinitions.ContainsKey(new ArgumentOrCommandToken("foo-run"))).IsFalse();

        await Assert.That(fooSnapshot.GeneratedFrom?.FullName).IsEqualTo("Fixtures.FooCommand");
        await Assert.That(fooSnapshot.Argument.Select(static item => item.Information.Name.Value)).IsEquivalentTo(["bar-input", "foo-input"]);
        await Assert.That(fooSnapshot.Properties.ContainsKey(new LongOptionToken("bar-flag"))).IsTrue();
        await Assert.That(fooSnapshot.Properties.ContainsKey(new LongOptionToken("foo-flag"))).IsTrue();
        await Assert.That(fooSnapshot.SubcommandDefinitions.ContainsKey(new ArgumentOrCommandToken("bar-run"))).IsTrue();
        await Assert.That(fooSnapshot.SubcommandDefinitions.ContainsKey(new ArgumentOrCommandToken("foo-run"))).IsTrue();
        await Assert.That(fooSnapshot.Subcommands.ContainsKey(new ArgumentOrCommandToken("bar-run"))).IsTrue();
        await Assert.That(fooSnapshot.Subcommands.ContainsKey(new ArgumentOrCommandToken("foo-run"))).IsTrue();
        await Assert.That(HasExplicitString(command, "bar-input", "bar-value")).IsTrue();
        await Assert.That(HasExplicitString(command, "foo-input", "foo-value")).IsTrue();
        await Assert.That(HasExplicitBoolean(command, "bar-flag")).IsTrue();
        await Assert.That(HasExplicitBoolean(command, "foo-flag")).IsTrue();
    }

    [Test]
    public async Task CommandAttribute_On_Derived_Command_Fails_For_Duplicate_Inherited_Arguments()
    {
        const string source = """
            using Kawayi.CommandLine.Core.Attributes;

            namespace Fixtures;

            [Command]
            public partial class BarCommand
            {
                /// <summary>
                /// Input summary
                /// </summary>
                [Argument(0)]
                [ValueRange(1, 1)]
                public string Input { get; set; } = string.Empty;
            }

            [Command]
            public partial class FooCommand : BarCommand
            {
                /// <summary>
                /// Input summary
                /// </summary>
                [Argument(0)]
                [ValueRange(1, 1)]
                public string Input { get; set; } = string.Empty;
            }
            """;

        var result = RunGenerator(source, "Fixtures.FooCommand");

        await AssertInheritedSchemaConflict(result, "Fixtures.FooCommand", "argument");
    }

    [Test]
    public async Task CommandAttribute_On_Derived_Command_Fails_For_Duplicate_Inherited_Properties()
    {
        const string source = """
            using Kawayi.CommandLine.Core.Attributes;

            namespace Fixtures;

            [Command]
            public partial class BarCommand
            {
                /// <summary>
                /// Format summary
                /// </summary>
                [Property]
                [LongAlias("format")]
                public string Format { get; set; } = string.Empty;
            }

            [Command]
            public partial class FooCommand : BarCommand
            {
                /// <summary>
                /// Other format summary
                /// </summary>
                [Property]
                [LongAlias("format")]
                public string OtherFormat { get; set; } = string.Empty;
            }
            """;

        var result = RunGenerator(source, "Fixtures.FooCommand");

        await AssertInheritedSchemaConflict(result, "Fixtures.FooCommand", "property token");
    }

    [Test]
    public async Task CommandAttribute_On_Derived_Command_Fails_For_Duplicate_Inherited_Subcommands()
    {
        const string source = """
            using Kawayi.CommandLine.Core.Attributes;

            namespace Fixtures;

            [Command]
            public partial class ChildCommand
            {
            }

            [Command]
            public partial class BarCommand
            {
                /// <summary>
                /// Run summary
                /// </summary>
                [Subcommand]
                public ChildCommand? Run { get; set; }
            }

            [Command]
            public partial class FooCommand : BarCommand
            {
                /// <summary>
                /// Run summary
                /// </summary>
                [Subcommand]
                public ChildCommand? Run { get; set; }
            }
            """;

        var result = RunGenerator(source, "Fixtures.FooCommand");

        await AssertInheritedSchemaConflict(result, "Fixtures.FooCommand", "subcommand token");
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
        var builder = GetCliSchemaBuilder(result, "Fixtures.Command");
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
        var rootValues = AssertFinishedCollection(subcommand.ContinueParseAction());
        var childValues = rootValues.Subcommands[subcommand.Definition];

        await Assert.That(snapshot.Properties.ContainsKey(new LongOptionToken("force-option"))).IsTrue();
        await Assert.That(snapshot.SubcommandDefinitions.ContainsKey(new ArgumentOrCommandToken("global"))).IsFalse();
        await Assert.That(snapshot.SubcommandDefinitions.ContainsKey(new ArgumentOrCommandToken("watch"))).IsTrue();
        await Assert.That(snapshot.Subcommands.ContainsKey(new ArgumentOrCommandToken("global"))).IsFalse();
        await Assert.That(snapshot.Subcommands.ContainsKey(new ArgumentOrCommandToken("watch"))).IsTrue();
        await Assert.That(HasExplicitString(parentValues, "input", "payload")).IsTrue();
        await Assert.That(HasExplicitBoolean(parentValues, "force-option")).IsTrue();
        await Assert.That(childValues.CurrentCommandDefinition?.Information.Name.Value).IsEqualTo("watch");
        await Assert.That(HasExplicitBoolean(childValues, "once")).IsTrue();
    }

    [Test]
    public async Task Hidden_Aliases_Remain_Parsable_Without_Becoming_Visible()
    {
        const string source = """
            using Kawayi.CommandLine.Core.Attributes;

            namespace Fixtures;

            [Command]
            public partial class Command
            {
                /// <summary>
                /// Token summary
                /// </summary>
                [Property]
                [LongAlias("secret-token", visible: false)]
                [ShortAlias("s", visible: false)]
                public string Token { get; set; } = string.Empty;
            }
            """;

        var result = RunGenerator(source, "Fixtures.Command");
        var builder = GetCliSchemaBuilder(result, "Fixtures.Command");
        var snapshot = builder.Build();
        var property = snapshot.Properties[new LongOptionToken("token")];
        var parsingResult = GetGeneratedParsingResult(
            result,
            "Fixtures.Command",
            [new LongOptionToken("secret-token"), new ArgumentOrCommandToken("hush")]);
        var command = AssertFinishedCollection(parsingResult);

        await Assert.That(property.Information.Name.Visible).IsTrue();
        await Assert.That(property.LongName["secret-token"].Visible).IsFalse();
        await Assert.That(property.ShortName["s"].Visible).IsFalse();
        await Assert.That(snapshot.Properties.ContainsKey(new LongOptionToken("secret-token"))).IsTrue();
        await Assert.That(snapshot.Properties.ContainsKey(new ShortOptionToken("s"))).IsTrue();
        await Assert.That(HasExplicitString(command, "token", "hush")).IsTrue();
    }

    [Test]
    public async Task CommandAttribute_With_Explicit_ExportAttributes_Generates_One_Parsing_Surface()
    {
        const string source = """
            using Kawayi.CommandLine.Core.Attributes;

            namespace Fixtures;

            [Command]
            [ExportDocument]
            [ExportSymbols]
            [ExportParsing]
            public partial class Command
            {
                /// <summary>
                /// Input summary
                /// </summary>
                [Argument(0)]
                [ValueRange(1, 1)]
                public string Input { get; set; } = string.Empty;

                /// <summary>
                /// Force summary
                /// </summary>
                [Property]
                [LongAlias("force")]
                public bool Force { get; set; }
            }
            """;

        var result = RunGenerator(source, "Fixtures.Command");
        var symbols = GetSymbols(result, "Fixtures.Command");
        var snapshot = GetCliSchemaBuilder(result, "Fixtures.Command").Build();

        await Assert.That(symbols.OfType<ParameterDefinition>().Count()).IsEqualTo(1);
        await Assert.That(symbols.OfType<PropertyDefinition>().Count()).IsEqualTo(1);
        await Assert.That(snapshot.Argument.Count).IsEqualTo(1);
        await Assert.That(snapshot.Properties.Values.Distinct().Count()).IsEqualTo(1);
        await Assert.That(HasInterface(result, "Fixtures.Command", "Kawayi.CommandLine.Abstractions.ICliSchemaExporter")).IsTrue();
        await Assert.That(HasInterface(result, "Fixtures.Command", "Kawayi.CommandLine.Abstractions.IParsable<Fixtures.Command>")).IsTrue();
    }

    [Test]
    public async Task Global_Subcommand_Promotion_Conflict_Fails_When_Exporting_Schema()
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
                public bool Force { get; set; }
            }

            [Command]
            public partial class Command
            {
                /// <summary>
                /// Force summary
                /// </summary>
                [Property]
                [LongAlias("root-force")]
                public bool Force { get; set; }

                /// <summary>
                /// Global summary
                /// </summary>
                [Subcommand(global: true)]
                public GlobalOptionsCommand Global { get; set; } = new();
            }
            """;

        var result = RunGenerator(source, "Fixtures.Command");

        try
        {
            _ = GetCliSchemaBuilder(result, "Fixtures.Command");
            throw new InvalidOperationException("Expected global subcommand promotion to fail.");
        }
        catch (TargetInvocationException exception)
        {
            await Assert.That(GetDeepestMessage(exception)).Contains("cannot be promoted");
            await Assert.That(GetDeepestMessage(exception)).Contains("force");
        }
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
    public async Task Subcommand_Without_CliSchemaExporter_ReportsDiagnostic()
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
        await Assert.That(diagnostic.GetMessage()).Contains("ICliSchemaExporter");
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
        await Assert.That(HasInterface(result, "Fixtures.Command", "Kawayi.CommandLine.Abstractions.ICliSchemaExporter")).IsFalse();
        await Assert.That(compilationErrors.Any(static item => item.Id.StartsWith("CS", StringComparison.Ordinal))).IsFalse();
    }

    [Test]
    public async Task Type_Without_Command_Or_ExportParsing_Does_Not_Generate_CliSchemaExporter()
    {
        const string source = """
            using Kawayi.CommandLine.Core.Attributes;

            namespace Fixtures;


            public partial class Command
            {
            }
            """;

        var result = RunGenerator(source, "Fixtures.Command");

        await Assert.That(HasInterface(result, "Fixtures.Command", "Kawayi.CommandLine.Abstractions.ICliSchemaExporter")).IsFalse();
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
        references.Add(MetadataReference.CreateFromFile(typeof(ICliSchemaExporter).Assembly.Location));

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
            TextWriter.Null,
            false,
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

    private static Cli AssertFinishedCollection(ParsingResult result)
    {
        return result is ParsingFinished { UntypedResult: Cli collection }
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

    private static bool HasExplicitBoolean(Cli collection, string definitionName)
    {
        var definition = collection.Schema.Argument.Cast<TypedDefinition>()
            .Concat(collection.Schema.Properties.Values.Distinct())
            .Single(item => string.Equals(item.Information.Name.Value, definitionName, StringComparison.Ordinal));

        return TryGetValue(collection, definition, out var rawValue) &&
               rawValue is bool typedValue &&
               typedValue;
    }

    private static bool HasExplicitString(Cli collection, string definitionName, string expectedValue)
    {
        var definition = collection.Schema.Argument.Cast<TypedDefinition>()
            .Concat(collection.Schema.Properties.Values.Distinct())
            .Single(item => string.Equals(item.Information.Name.Value, definitionName, StringComparison.Ordinal));

        return TryGetValue(collection, definition, out var rawValue) &&
               rawValue is string typedValue &&
               string.Equals(typedValue, expectedValue, StringComparison.Ordinal);
    }

    private static bool TryGetValue(Cli collection, TypedDefinition definition, out object? value)
    {
        switch (definition)
        {
            case ParameterDefinition argument:
                return collection.Arguments.TryGetValue(argument, out value);
            case PropertyDefinition property:
                return collection.Properties.TryGetValue(property, out value);
            default:
                value = null;
                return false;
        }
    }

    private static async Task AssertInheritedSchemaConflict(
        GeneratorRunOutcome outcome,
        string targetTypeMetadataName,
        string expectedConflictKind)
    {
        try
        {
            _ = GetCliSchemaBuilder(outcome, targetTypeMetadataName);
            throw new InvalidOperationException("Expected inherited schema merge to fail.");
        }
        catch (TargetInvocationException exception)
        {
            var message = GetDeepestMessage(exception);
            await Assert.That(message).Contains("Inherited command schema conflict");
            await Assert.That(message).Contains(expectedConflictKind);
        }
    }

    private static string GetDeepestMessage(Exception exception)
    {
        var current = exception;

        while (current.InnerException is not null)
        {
            current = current.InnerException;
        }

        return current.Message;
    }

    private sealed record GeneratorRunOutcome(
        Compilation Compilation,
        GeneratorDriverRunResult RunResult,
        string TargetTypeMetadataName);
}
