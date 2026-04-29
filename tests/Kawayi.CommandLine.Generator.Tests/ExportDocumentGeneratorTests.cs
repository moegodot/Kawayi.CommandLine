// Copyright (c) 2026 MoeGodot<me@kawayi.moe>.
// Licensed under the GNU Affero General Public License v3-or-later license.

using System.Collections.Immutable;
using System.Reflection;
using Kawayi.CommandLine.Abstractions;
using Kawayi.CommandLine.Core.Attributes;
using Kawayi.CommandLine.Generator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using CommandDocument = Kawayi.CommandLine.Abstractions.Document;

namespace Kawayi.CommandLine.Generator.Tests;

public class ExportDocumentGeneratorTests
{
    [Test]
    public async Task SummaryAndRemarks_AreExported()
    {
        const string source = """
            using Kawayi.CommandLine.Core.Attributes;

            namespace Fixtures;

            [ExportDocument]
            public partial class Command
            {
                /// <summary>
                /// Property summary
                /// </summary>
                /// <remarks>
                /// Property remarks
                /// </remarks>
                public string Property { get; set; } = string.Empty;

                /// <summary>
                /// Field summary
                /// </summary>
                /// <remarks>
                /// Field remarks
                /// </remarks>
                public int Field;
            }
            """;

        var result = RunGenerator(source, "Fixtures.Command");

        await Assert.That(result.Documents).IsNotNull();
        await Assert.That(result.Documents!["Property"]).EqualTo(new CommandDocument("Property summary", "Property remarks"));
        await Assert.That(result.Documents["Field"]).EqualTo(new CommandDocument("Field summary", "Field remarks"));
        await Assert.That(HasInterface(result, "Fixtures.Command", "Kawayi.CommandLine.Abstractions.IDocumentExporter")).IsTrue();
    }

    [Test]
    public async Task CommandAttribute_Generates_DocumentExporter()
    {
        const string source = """
            using Kawayi.CommandLine.Core.Attributes;

            namespace Fixtures;

            [Command]
            public partial class Command
            {
                /// <summary>
                /// Property summary
                /// </summary>
                /// <remarks>
                /// Property remarks
                /// </remarks>
                public string Property { get; set; } = string.Empty;
            }
            """;

        var result = RunGenerator(source, "Fixtures.Command");

        await Assert.That(result.Documents).IsNotNull();
        await Assert.That(result.Documents!["Property"]).EqualTo(new CommandDocument("Property summary", "Property remarks"));
        await Assert.That(HasInterface(result, "Fixtures.Command", "Kawayi.CommandLine.Abstractions.IDocumentExporter")).IsTrue();
    }

    [Test]
    public async Task MissingSummaryOrRemarks_UsesEmptyString()
    {
        const string source = """
            using Kawayi.CommandLine.Core.Attributes;

            namespace Fixtures;

            [ExportDocument]
            public partial class Command
            {
                /// <summary>
                /// Has summary only
                /// </summary>
                public string SummaryOnly { get; set; } = string.Empty;

                /// <remarks>
                /// Has remarks only
                /// </remarks>
                public string RemarksOnly { get; set; } = string.Empty;

                public int NoDocumentation;
            }
            """;

        var result = RunGenerator(source, "Fixtures.Command");

        await Assert.That(result.Documents).IsNotNull();
        await Assert.That(result.Documents!["SummaryOnly"]).EqualTo(new CommandDocument("Has summary only", string.Empty));
        await Assert.That(result.Documents["RemarksOnly"]).EqualTo(new CommandDocument(string.Empty, "Has remarks only"));
        await Assert.That(result.Documents["NoDocumentation"]).EqualTo(new CommandDocument(string.Empty, string.Empty));
    }

    [Test]
    public async Task OnlySupportedVisibleMembers_AreExported()
    {
        const string source = """
            using Kawayi.CommandLine.Core.Attributes;

            namespace Fixtures;

            public class BaseCommand
            {
                /// <summary>
                /// Inherited member
                /// </summary>
                public string BaseProperty { get; set; } = string.Empty;
            }

            [ExportDocument]
            public partial class Command : BaseCommand
            {
                /// <summary>
                /// Public property
                /// </summary>
                public string PublicProperty { get; set; } = string.Empty;

                /// <summary>
                /// Protected property
                /// </summary>
                protected string ProtectedProperty { get; set; } = string.Empty;

                /// <summary>
                /// Protected internal property
                /// </summary>
                protected internal string ProtectedInternalProperty { get; set; } = string.Empty;

                /// <summary>
                /// Public field
                /// </summary>
                public int PublicField;

                /// <summary>
                /// Protected field
                /// </summary>
                protected int ProtectedField;

                /// <summary>
                /// Protected internal field
                /// </summary>
                protected internal int ProtectedInternalField;

                /// <summary>
                /// Private property
                /// </summary>
                private string PrivateProperty { get; set; } = string.Empty;

                /// <summary>
                /// Internal property
                /// </summary>
                internal string InternalProperty { get; set; } = string.Empty;

                /// <summary>
                /// Private protected field
                /// </summary>
                private protected int PrivateProtectedField;

                /// <summary>
                /// Indexer property
                /// </summary>
                public string this[int index] => index.ToString();
            }
            """;

        var result = RunGenerator(source, "Fixtures.Command");

        await Assert.That(result.Documents).IsNotNull();
        await Assert.That(result.Documents!.Count).EqualTo(6);
        await Assert.That(result.Documents.ContainsKey("PublicProperty")).IsTrue();
        await Assert.That(result.Documents.ContainsKey("ProtectedProperty")).IsTrue();
        await Assert.That(result.Documents.ContainsKey("ProtectedInternalProperty")).IsTrue();
        await Assert.That(result.Documents.ContainsKey("PublicField")).IsTrue();
        await Assert.That(result.Documents.ContainsKey("ProtectedField")).IsTrue();
        await Assert.That(result.Documents.ContainsKey("ProtectedInternalField")).IsTrue();
        await Assert.That(result.Documents.ContainsKey("PrivateProperty")).IsFalse();
        await Assert.That(result.Documents.ContainsKey("InternalProperty")).IsFalse();
        await Assert.That(result.Documents.ContainsKey("PrivateProtectedField")).IsFalse();
        await Assert.That(result.Documents.ContainsKey("BaseProperty")).IsFalse();
        await Assert.That(result.Documents.ContainsKey("Item")).IsFalse();
    }

    [Test]
    public async Task NonPartialType_ReportsDiagnostic()
    {
        const string source = """
            using Kawayi.CommandLine.Core.Attributes;

            namespace Fixtures;

            [ExportDocument]
            public class Command
            {
                /// <summary>
                /// Public property
                /// </summary>
                public string Property { get; set; } = string.Empty;
            }
            """;

        var result = RunGenerator(source, "Fixtures.Command", expectSuccessfulEmit: false);
        var diagnostic = result.RunResult.Results.SelectMany(static generatorResult => generatorResult.Diagnostics)
            .Single(static item => item.Id == "KCLG001");

        await Assert.That(diagnostic.Severity).EqualTo(DiagnosticSeverity.Error);
        await Assert.That(diagnostic.GetMessage()).Contains("partial");
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

        GeneratorDriver driver = CSharpGeneratorDriver.Create(new ExportDocumentGenerator().AsSourceGenerator());
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
        IReadOnlyDictionary<string, CommandDocument>? documents = null;

        if (expectSuccessfulEmit)
        {
            using var assemblyStream = new MemoryStream();
            var emitResult = outputCompilation.Emit(assemblyStream);
            if (!emitResult.Success)
            {
                throw new InvalidOperationException(
                    "Emit failed:\n" + string.Join("\n", emitResult.Diagnostics.Select(static diagnostic => diagnostic.ToString())));
            }

            assemblyStream.Position = 0;
            var assembly = System.Reflection.Assembly.Load(assemblyStream.ToArray());
            var targetType = assembly.GetType(targetTypeMetadataName, throwOnError: true)!;
            documents =
                (IReadOnlyDictionary<string, CommandDocument>)targetType.GetProperty("Documents", BindingFlags.Public | BindingFlags.Static)!
                    .GetValue(null)!;
        }

        return new(outputCompilation, runResult, documents);
    }

    private static ImmutableArray<MetadataReference> CreateMetadataReferences()
    {
        var references = ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES"))!
            .Split(Path.PathSeparator)
            .Select(static path => MetadataReference.CreateFromFile(path))
            .Cast<MetadataReference>()
            .ToImmutableArray()
            .ToBuilder();

        references.Add(MetadataReference.CreateFromFile(typeof(ExportDocumentAttribute).Assembly.Location));
        references.Add(MetadataReference.CreateFromFile(typeof(IDocumentExporter).Assembly.Location));

        return references.ToImmutable();
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
        IReadOnlyDictionary<string, CommandDocument>? Documents);
}
