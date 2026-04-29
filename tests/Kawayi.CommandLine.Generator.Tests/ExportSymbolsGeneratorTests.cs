// Copyright (c) 2026 MoeGodot<me@kawayi.moe>.
// Licensed under the GNU Affero General Public License v3-or-later license.

using System.Collections;
using System.Collections.Immutable;
using System.Reflection;
using Kawayi.CommandLine.Abstractions;
using Kawayi.CommandLine.Core.Attributes;
using Kawayi.CommandLine.Generator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Kawayi.CommandLine.Generator.Tests;

public class ExportSymbolsGeneratorTests
{
    [Test]
    public async Task Generates_Argument_Property_And_Subcommand_Symbols()
    {
        const string source = """
            using System.Collections.Immutable;
            using Kawayi.CommandLine.Abstractions;
            using Kawayi.CommandLine.Core.Attributes;

            namespace Fixtures;

            public sealed class ChildCommand
            {
            }

            [ExportSymbols]
            public partial class Command : IDocumentExporter
            {
                public static ImmutableDictionary<string, Document> Documents { get; } =
                    ImmutableDictionary<string, Document>.Empty
                        .Add("Input", new Document("Input summary", "Input help"))
                        .Add("Verbose", new Document("Verbose summary", "Verbose help"))
                        .Add("Serve", new Document("Serve summary", "Serve help"));

                [Argument(1, require: true, visible: false)]
                [ValueRange(1, 2147483647)]
                public string[] Input { get; set; } = [];

                [Property(require: true, visible: false, valueName: "flag")]
                [LongAlias("verbose")]
                [ShortAlias("v", visible: false)]
                public bool Verbose { get; set; }

                [Subcommand(visible: true)]
                [Alias("srv")]
                [Alias("s", visible: false)]
                public ChildCommand Serve { get; } = new();
            }
            """;

        var result = RunGenerator(source, "Fixtures.Command");
        var symbols = GetSymbols(result, "Fixtures.Command");

        await Assert.That(symbols.Length).IsEqualTo(3);
        await Assert.That(HasInterface(result, "Fixtures.Command", "Kawayi.CommandLine.Abstractions.ISymbolExporter")).IsTrue();

        if (symbols[0] is not ArgumentDefinition argument)
        {
            throw new InvalidOperationException($"Expected first symbol to be {nameof(ArgumentDefinition)}.");
        }

        if (symbols[1] is not PropertyDefinition property)
        {
            throw new InvalidOperationException($"Expected second symbol to be {nameof(PropertyDefinition)}.");
        }

        if (symbols[2] is not CommandDefinition subcommand)
        {
            throw new InvalidOperationException($"Expected third symbol to be {nameof(CommandDefinition)}.");
        }

        await Assert.That(argument.Information.Name.Value).IsEqualTo("Input");
        await Assert.That(argument.Information.Name.Visible).IsFalse();
        await Assert.That(argument.Requirement).IsTrue();
        await Assert.That(argument.ValueRange.Minimum).IsEqualTo(1);
        await Assert.That(argument.ValueRange.Maximum).IsEqualTo(int.MaxValue);

        await Assert.That(property.Information.Name.Value).IsEqualTo("Verbose");
        await Assert.That(property.Information.Name.Visible).IsFalse();
        await Assert.That(property.Requirement).IsTrue();
        await Assert.That(property.NumArgs).IsEqualTo(ValueRange.ZeroOrMore);
        await Assert.That(property.ValueName).IsEqualTo("flag");
        await Assert.That(property.LongName["verbose"]).IsEqualTo(new NameWithVisibility("verbose", true));
        await Assert.That(property.ShortName["v"]).IsEqualTo(new NameWithVisibility("v", false));

        await Assert.That(subcommand.Information.Name.Value).IsEqualTo("Serve");
        await Assert.That(subcommand.Information.Name.Visible).IsTrue();
        await Assert.That(subcommand.Alias["srv"]).IsEqualTo(new NameWithVisibility("srv", true));
        await Assert.That(subcommand.Alias["s"]).IsEqualTo(new NameWithVisibility("s", false));
    }

    [Test]
    public async Task Property_ValueName_Is_Null_When_Not_Configured()
    {
        const string source = """
            using System.Collections.Immutable;
            using Kawayi.CommandLine.Abstractions;
            using Kawayi.CommandLine.Core.Attributes;

            namespace Fixtures;

            [ExportSymbols]
            public partial class Command : IDocumentExporter
            {
                public static ImmutableDictionary<string, Document> Documents { get; } =
                    ImmutableDictionary<string, Document>.Empty
                        .Add("Format", new Document("Format summary", "Format help"));

                [Property]
                [LongAlias("format")]
                public string Format { get; set; } = string.Empty;
            }
            """;

        var result = RunGenerator(source, "Fixtures.Command");
        var symbols = GetSymbols(result, "Fixtures.Command");

        if (symbols[0] is not PropertyDefinition property)
        {
            throw new InvalidOperationException($"Expected first symbol to be {nameof(PropertyDefinition)}.");
        }

        await Assert.That(property.ValueName).IsNull();
        await Assert.That(property.NumArgs).IsEqualTo(ValueRange.ZeroOrMore);
    }

    [Test]
    public async Task Property_NumArgs_Is_Exported_When_Configured()
    {
        const string source = """
            using System.Collections.Immutable;
            using Kawayi.CommandLine.Abstractions;
            using Kawayi.CommandLine.Core.Attributes;

            namespace Fixtures;

            [ExportSymbols]
            public partial class Command : IDocumentExporter
            {
                public static ImmutableDictionary<string, Document> Documents { get; } =
                    ImmutableDictionary<string, Document>.Empty
                        .Add("Format", new Document("Format summary", "Format help"));

                [Property]
                [ValueRange(1, 1)]
                [LongAlias("format")]
                public string Format { get; set; } = string.Empty;
            }
            """;

        var result = RunGenerator(source, "Fixtures.Command");
        var symbols = GetSymbols(result, "Fixtures.Command");

        if (symbols[0] is not PropertyDefinition property)
        {
            throw new InvalidOperationException($"Expected first symbol to be {nameof(PropertyDefinition)}.");
        }

        await Assert.That(property.NumArgs).IsEqualTo(new ValueRange(1, 1));
    }

    [Test]
    public async Task Exports_All_Tagged_Member_Visibilities_When_Documents_Are_Provided_Manually()
    {
        const string source = """
            using System.Collections.Immutable;
            using Kawayi.CommandLine.Abstractions;
            using Kawayi.CommandLine.Core.Attributes;

            namespace Fixtures;

            [ExportSymbols]
            public partial class Command : IDocumentExporter
            {
                public static ImmutableDictionary<string, Document> Documents { get; } =
                    ImmutableDictionary<string, Document>.Empty
                        .Add("PrivateArgument", new Document("", ""))
                        .Add("InternalOption", new Document("", ""))
                        .Add("ProtectedCommand", new Document("", ""));

                [Argument(0)]
                [ValueRange(0, 1)]
                private string? PrivateArgument { get; set; }

                [Property]
                [LongAlias("internal-option")]
                internal bool InternalOption { get; set; }

                [Subcommand]
                protected Nested ProtectedCommand { get; } = new();

                public sealed class Nested
                {
                }
            }
            """;

        var result = RunGenerator(source, "Fixtures.Command");
        var names = GetSymbols(result, "Fixtures.Command")
            .Select(static symbol => symbol.Information.Name.Value)
            .ToImmutableArray();

        await Assert.That(names.Length).IsEqualTo(3);
        await Assert.That(names.Contains("PrivateArgument")).IsTrue();
        await Assert.That(names.Contains("InternalOption")).IsTrue();
        await Assert.That(names.Contains("ProtectedCommand")).IsTrue();
    }

    [Test]
    public async Task NonPartialType_ReportsDiagnostic()
    {
        const string source = """
            using System.Collections.Immutable;
            using Kawayi.CommandLine.Abstractions;
            using Kawayi.CommandLine.Core.Attributes;

            namespace Fixtures;

            [ExportSymbols]
            public class Command : IDocumentExporter
            {
                public static ImmutableDictionary<string, Document> Documents { get; } =
                    ImmutableDictionary<string, Document>.Empty.Add("Input", new Document("", ""));

                [Argument(0)]
                [ValueRange(0, 1)]
                public string? Input { get; set; }
            }
            """;

        var result = RunGenerator(source, "Fixtures.Command", expectSuccessfulEmit: false);
        var diagnostic = GetSingleDiagnostic(result, "KCLG101");

        await Assert.That(diagnostic.Severity).IsEqualTo(DiagnosticSeverity.Error);
        await Assert.That(diagnostic.GetMessage()).Contains("partial");
    }

    [Test]
    public async Task Missing_DocumentExporter_ReportsDiagnostic()
    {
        const string source = """
            using Kawayi.CommandLine.Core.Attributes;

            namespace Fixtures;

            [ExportSymbols]
            public partial class Command
            {
                [Argument(0)]
                [ValueRange(0, 1)]
                public string? Input { get; set; }
            }
            """;

        var result = RunGenerator(source, "Fixtures.Command", expectSuccessfulEmit: false);
        var diagnostic = GetSingleDiagnostic(result, "KCLG102");

        await Assert.That(diagnostic.Severity).IsEqualTo(DiagnosticSeverity.Error);
        await Assert.That(diagnostic.GetMessage()).Contains("IDocumentExporter");
    }

    [Test]
    public async Task Missing_ValueRange_ReportsDiagnostic()
    {
        const string source = """
            using System.Collections.Immutable;
            using Kawayi.CommandLine.Abstractions;
            using Kawayi.CommandLine.Core.Attributes;

            namespace Fixtures;

            [ExportSymbols]
            public partial class Command : IDocumentExporter
            {
                public static ImmutableDictionary<string, Document> Documents { get; } =
                    ImmutableDictionary<string, Document>.Empty.Add("Input", new Document("", ""));

                [Argument(0)]
                public string? Input { get; set; }
            }
            """;

        var result = RunGenerator(source, "Fixtures.Command", expectSuccessfulEmit: false);
        var diagnostic = GetSingleDiagnostic(result, "KCLG104");

        await Assert.That(diagnostic.GetMessage()).Contains("ValueRangeAttribute");
    }

    [Test]
    public async Task Multiple_Roles_ReportDiagnostic()
    {
        const string source = """
            using System.Collections.Immutable;
            using Kawayi.CommandLine.Abstractions;
            using Kawayi.CommandLine.Core.Attributes;

            namespace Fixtures;

            [ExportSymbols]
            public partial class Command : IDocumentExporter
            {
                public static ImmutableDictionary<string, Document> Documents { get; } =
                    ImmutableDictionary<string, Document>.Empty.Add("Input", new Document("", ""));

                [Argument(0)]
                [ValueRange(0, 1)]
                [Property]
                public string? Input { get; set; }
            }
            """;

        var result = RunGenerator(source, "Fixtures.Command", expectSuccessfulEmit: false);
        var diagnostic = GetSingleDiagnostic(result, "KCLG103");

        await Assert.That(diagnostic.GetMessage()).Contains("multiple symbol role attributes");
    }

    [Test]
    public async Task Invalid_Alias_Usage_ReportsDiagnostics()
    {
        const string source = """
            using System.Collections.Immutable;
            using Kawayi.CommandLine.Abstractions;
            using Kawayi.CommandLine.Core.Attributes;

            namespace Fixtures;

            [ExportSymbols]
            public partial class Command : IDocumentExporter
            {
                public static ImmutableDictionary<string, Document> Documents { get; } =
                    ImmutableDictionary<string, Document>.Empty
                        .Add("Input", new Document("", ""))
                        .Add("Mode", new Document("", ""));

                [Argument(0)]
                [ValueRange(0, 1)]
                [LongAlias("input")]
                public string? Input { get; set; }

                [Property]
                [Alias("m")]
                public string? Mode { get; set; }
            }
            """;

        var result = RunGenerator(source, "Fixtures.Command", expectSuccessfulEmit: false);
        var diagnostics = GetGeneratorDiagnostics(result);

        await Assert.That(diagnostics.Any(static item => item.Id == "KCLG105")).IsTrue();
        await Assert.That(diagnostics.Any(static item => item.Id == "KCLG106")).IsTrue();
    }

    [Test]
    public async Task Duplicate_Position_And_Alias_Conflicts_ReportDiagnostics()
    {
        const string source = """
            using System.Collections.Immutable;
            using Kawayi.CommandLine.Abstractions;
            using Kawayi.CommandLine.Core.Attributes;

            namespace Fixtures;

            public sealed class ChildCommand
            {
            }

            [ExportSymbols]
            public partial class Command : IDocumentExporter
            {
                public static ImmutableDictionary<string, Document> Documents { get; } =
                    ImmutableDictionary<string, Document>.Empty
                        .Add("First", new Document("", ""))
                        .Add("Second", new Document("", ""))
                        .Add("Mode", new Document("", ""))
                        .Add("Serve", new Document("", ""));

                [Argument(0)]
                [ValueRange(0, 1)]
                public string? First { get; set; }

                [Argument(0)]
                [ValueRange(0, 1)]
                public string? Second { get; set; }

                [Property]
                [LongAlias("dup")]
                public bool Mode { get; set; }

                [Subcommand]
                [Alias("dup")]
                public ChildCommand Serve { get; } = new();
            }
            """;

        var result = RunGenerator(source, "Fixtures.Command", expectSuccessfulEmit: false);
        var diagnostics = GetGeneratorDiagnostics(result);

        await Assert.That(diagnostics.Any(static item => item.Id == "KCLG107")).IsTrue();
        await Assert.That(diagnostics.Any(static item => item.Id == "KCLG108")).IsTrue();
    }

    [Test]
    public async Task Missing_Document_Entry_Fails_When_Symbols_Are_Accessed()
    {
        const string source = """
            using Kawayi.CommandLine.Core.Attributes;

            namespace Fixtures;

            [ExportDocument]
            [ExportSymbols]
            public partial class Command
            {
                [Property]
                [LongAlias("private-option")]
                private bool PrivateOption { get; set; }
            }
            """;

        var result = RunGenerator(source, "Fixtures.Command");

        try
        {
            _ = GetSymbols(result, "Fixtures.Command");
            throw new InvalidOperationException("Expected symbol access to fail because the generated Documents dictionary does not contain the private member.");
        }
        catch (TargetInvocationException exception)
        {
            await Assert.That(GetDeepestMessage(exception)).Contains("PrivateOption");
        }
        catch (TypeInitializationException exception)
        {
            await Assert.That(GetDeepestMessage(exception)).Contains("PrivateOption");
        }
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
                new ExportSymbolsGenerator().AsSourceGenerator()
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

        references.Add(MetadataReference.CreateFromFile(typeof(ExportSymbolsAttribute).Assembly.Location));
        references.Add(MetadataReference.CreateFromFile(typeof(ISymbolExporter).Assembly.Location));
        references.Add(MetadataReference.CreateFromFile(typeof(IDocumentExporter).Assembly.Location));

        return references.ToImmutable();
    }

    private static Symbol[] GetSymbols(GeneratorRunOutcome outcome, string targetTypeMetadataName)
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
        var targetType = assembly.GetType(targetTypeMetadataName, throwOnError: true)!;
        var property = targetType.GetProperty("Symbols", BindingFlags.Public | BindingFlags.Static)
            ?? throw new InvalidOperationException("Generated type does not define the expected Symbols property.");
        var rawValue = property.GetValue(null)
            ?? throw new InvalidOperationException("Generated Symbols property returned null.");

        return ((IEnumerable)rawValue).Cast<Symbol>().ToArray();
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
