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

        if (symbols[0] is not ParameterDefinition argument)
        {
            throw new InvalidOperationException($"Expected first symbol to be {nameof(ParameterDefinition)}.");
        }

        if (symbols[1] is not PropertyDefinition property)
        {
            throw new InvalidOperationException($"Expected second symbol to be {nameof(PropertyDefinition)}.");
        }

        if (symbols[2] is not CommandDefinition subcommand)
        {
            throw new InvalidOperationException($"Expected third symbol to be {nameof(CommandDefinition)}.");
        }

        await Assert.That(argument.Information.Name.Value).IsEqualTo("input");
        await Assert.That(argument.Information.Name.Visible).IsFalse();
        await Assert.That(argument.Information.Document.ConciseDescription).IsEqualTo("Input summary");
        await Assert.That(argument.Requirement).IsTrue();
        await Assert.That(argument.ValueRange.Minimum).IsEqualTo(1);
        await Assert.That(argument.ValueRange.Maximum).IsEqualTo(int.MaxValue);

        await Assert.That(property.Information.Name.Value).IsEqualTo("verbose");
        await Assert.That(property.Information.Name.Visible).IsFalse();
        await Assert.That(property.Requirement).IsTrue();
        await Assert.That(property.NumArgs).IsEqualTo(ValueRange.ZeroOrOne);
        await Assert.That(property.ValueName).IsEqualTo("flag");
        await Assert.That(property.LongName["verbose"]).IsEqualTo(new NameWithVisibility("verbose", true));
        await Assert.That(property.ShortName["v"]).IsEqualTo(new NameWithVisibility("v", false));

        await Assert.That(subcommand.Information.Name.Value).IsEqualTo("serve");
        await Assert.That(subcommand.Information.Name.Visible).IsTrue();
        await Assert.That(subcommand.Alias["srv"]).IsEqualTo(new NameWithVisibility("srv", true));
        await Assert.That(subcommand.Alias["s"]).IsEqualTo(new NameWithVisibility("s", false));
    }

    [Test]
    public async Task CommandAttribute_Generates_SymbolExporter()
    {
        const string source = """
            using Kawayi.CommandLine.Core.Attributes;

            namespace Fixtures;

            [Command]
            public partial class Command
            {
                /// <summary>
                /// Verbose summary
                /// </summary>
                /// <remarks>
                /// Verbose help
                /// </remarks>
                [Property]
                [LongAlias("verbose")]
                public bool Verbose { get; set; }
            }
            """;

        var result = RunGenerator(source, "Fixtures.Command");
        var symbols = GetSymbols(result, "Fixtures.Command");
        var property = symbols.OfType<PropertyDefinition>().Single();

        await Assert.That(HasInterface(result, "Fixtures.Command", "Kawayi.CommandLine.Abstractions.ISymbolExporter")).IsTrue();
        await Assert.That(property.Information.Name.Value).IsEqualTo("verbose");
        await Assert.That(property.Information.Document.ConciseDescription).IsEqualTo("Verbose summary");
        await Assert.That(property.LongName["verbose"]).IsEqualTo(new NameWithVisibility("verbose", true));
    }

    [Test]
    public async Task Generated_Symbol_Names_Are_Kebab_Case_While_Document_Keys_Stay_Csharp_Names()
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
                        .Add("RequestId", new Document("Request summary", "Request help"))
                        .Add("HTTPServer2URL", new Document("Server summary", "Server help"))
                        .Add("ServeCommand", new Document("Serve summary", "Serve help"));

                [Argument(0)]
                [ValueRange(0, 1)]
                public string? RequestId { get; set; }

                [Property]
                [LongAlias("explicit-http")]
                public string HTTPServer2URL { get; set; } = string.Empty;

                [Subcommand]
                [Alias("serve-explicit")]
                public ChildCommand ServeCommand { get; } = new();
            }
            """;

        var result = RunGenerator(source, "Fixtures.Command");
        var symbols = GetSymbols(result, "Fixtures.Command");
        var requestId = symbols.OfType<ParameterDefinition>().Single();
        var httpServer = symbols.OfType<PropertyDefinition>().Single();
        var serveCommand = symbols.OfType<CommandDefinition>().Single();

        await Assert.That(requestId.Information.Name.Value).IsEqualTo("request-id");
        await Assert.That(requestId.Information.Document.ConciseDescription).IsEqualTo("Request summary");
        await Assert.That(httpServer.Information.Name.Value).IsEqualTo("http-server-2-url");
        await Assert.That(httpServer.Information.Document.ConciseDescription).IsEqualTo("Server summary");
        await Assert.That(httpServer.LongName["explicit-http"]).IsEqualTo(new NameWithVisibility("explicit-http", true));
        await Assert.That(serveCommand.Information.Name.Value).IsEqualTo("serve-command");
        await Assert.That(serveCommand.Information.Document.ConciseDescription).IsEqualTo("Serve summary");
        await Assert.That(serveCommand.Alias["serve-explicit"]).IsEqualTo(new NameWithVisibility("serve-explicit", true));
    }

    [Test]
    public async Task NonNullable_Subcommand_Property_Reports_Warning()
    {
        const string source = """
            #nullable enable

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
                        .Add("Serve", new Document("Serve summary", "Serve help"))
                        .Add("Global", new Document("Global summary", "Global help"));

                [Subcommand]
                public ChildCommand Serve { get; } = new();

                [Subcommand(global: true)]
                public ChildCommand Global { get; set; } = new();
            }
            """;

        var result = RunGenerator(source, "Fixtures.Command");
        var diagnostics = GetGeneratorDiagnostics(result);

        await Assert.That(diagnostics.Count(static item => item.Id == "KCLG111")).IsEqualTo(1);
        await Assert.That(diagnostics.Single(static item => item.Id == "KCLG111").Severity).IsEqualTo(DiagnosticSeverity.Warning);
        await Assert.That(diagnostics.Single(static item => item.Id == "KCLG111").GetMessage()).Contains("Serve");
    }

    [Test]
    public async Task RequirementIfNull_Is_Exported_For_Nullable_Arguments_And_Properties()
    {
        const string source = """
            #nullable enable

            using System.Collections.Immutable;
            using Kawayi.CommandLine.Abstractions;
            using Kawayi.CommandLine.Core.Attributes;

            namespace Fixtures;

            [ExportSymbols]
            public partial class Command : IDocumentExporter
            {
                public static ImmutableDictionary<string, Document> Documents { get; } =
                    ImmutableDictionary<string, Document>.Empty
                        .Add("Input", new Document("Input summary", "Input help"))
                        .Add("Count", new Document("Count summary", "Count help"));

                [Argument(0, requirementIfNull: true)]
                [ValueRange(0, 1)]
                public string? Input { get; set; }

                [Property(requirementIfNull: true)]
                [LongAlias("count")]
                public int? Count { get; set; }
            }
            """;

        var result = RunGenerator(source, "Fixtures.Command");
        var symbols = GetSymbols(result, "Fixtures.Command");
        var diagnostics = GetGeneratorDiagnostics(result);
        var argument = symbols.OfType<ParameterDefinition>().Single();
        var property = symbols.OfType<PropertyDefinition>().Single();

        await Assert.That(argument.Requirement).IsFalse();
        await Assert.That(argument.RequirementIfNull).IsTrue();
        await Assert.That(property.Requirement).IsFalse();
        await Assert.That(property.RequirementIfNull).IsTrue();
        await Assert.That(diagnostics.Any(static item => item.Id == "KCLG112")).IsFalse();
    }

    [Test]
    public async Task RequirementIfNull_On_NonNullable_Member_ReportsDiagnostic()
    {
        const string source = """
            #nullable enable

            using System.Collections.Immutable;
            using Kawayi.CommandLine.Abstractions;
            using Kawayi.CommandLine.Core.Attributes;

            namespace Fixtures;

            [ExportSymbols]
            public partial class Command : IDocumentExporter
            {
                public static ImmutableDictionary<string, Document> Documents { get; } =
                    ImmutableDictionary<string, Document>.Empty
                        .Add("Input", new Document("Input summary", "Input help"))
                        .Add("Count", new Document("Count summary", "Count help"));

                [Argument(0, requirementIfNull: true)]
                [ValueRange(0, 1)]
                public int Input { get; set; }

                [Property(requirementIfNull: true)]
                [LongAlias("count")]
                public string Count { get; set; } = string.Empty;
            }
            """;

        var result = RunGenerator(source, "Fixtures.Command", expectSuccessfulEmit: false);
        var diagnostics = GetGeneratorDiagnostics(result);

        await Assert.That(diagnostics.Count(static item => item.Id == "KCLG112")).IsEqualTo(2);
        await Assert.That(diagnostics.All(static item => item.Id != "KCLG112" || item.Severity == DiagnosticSeverity.Error)).IsTrue();
    }

    [Test]
    public async Task RequirementIfNull_On_NullableOblivious_Reference_Member_Does_Not_ReportDiagnostic()
    {
        const string source = """
            #nullable disable

            using System.Collections.Immutable;
            using Kawayi.CommandLine.Abstractions;
            using Kawayi.CommandLine.Core.Attributes;

            namespace Fixtures;

            [ExportSymbols]
            public partial class Command : IDocumentExporter
            {
                public static ImmutableDictionary<string, Document> Documents { get; } =
                    ImmutableDictionary<string, Document>.Empty
                        .Add("Input", new Document("Input summary", "Input help"))
                        .Add("Name", new Document("Name summary", "Name help"));

                [Argument(0, requirementIfNull: true)]
                [ValueRange(0, 1)]
                public string Input { get; set; }

                [Property(requirementIfNull: true)]
                [LongAlias("name")]
                public string Name { get; set; }
            }
            """;

        var result = RunGenerator(source, "Fixtures.Command");
        var diagnostics = GetGeneratorDiagnostics(result);
        var symbols = GetSymbols(result, "Fixtures.Command");

        await Assert.That(diagnostics.Any(static item => item.Id == "KCLG112")).IsFalse();
        await Assert.That(symbols.OfType<ParameterDefinition>().Single().RequirementIfNull).IsTrue();
        await Assert.That(symbols.OfType<PropertyDefinition>().Single().RequirementIfNull).IsTrue();
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
        await Assert.That(property.NumArgs).IsEqualTo(ValueRange.One);
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
    public async Task Validators_Are_Exported_For_Arguments_And_Properties()
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
                        .Add("Input", new Document("Input summary", "Input help"))
                        .Add("Count", new Document("Count summary", "Count help"))
                        .Add("Secret", new Document("Secret summary", "Secret help"));

                [Argument(0)]
                [ValueRange(0, 1)]
                [Validator(nameof(ValidateInput))]
                public string? Input { get; set; }

                [Property]
                [LongAlias("count")]
                [Validator(nameof(ValidateCount))]
                public int Count { get; set; }

                [Property]
                [LongAlias("secret")]
                [Validator(nameof(ValidateSecret))]
                public string Secret { get; set; } = string.Empty;

                private static string? ValidateInput(string? value)
                {
                    return value == "error" ? "input error" : null;
                }

                internal static string? ValidateCount(int value)
                {
                    return value < 0 ? "count error" : null;
                }

                public static string? ValidateSecret(string value)
                {
                    return value == "blocked" ? "secret error" : null;
                }
            }
            """;

        var result = RunGenerator(source, "Fixtures.Command");
        var symbols = GetSymbols(result, "Fixtures.Command");
        var input = symbols.OfType<ParameterDefinition>().Single(static argument => argument.Information.Name.Value == "input");
        var count = symbols.OfType<PropertyDefinition>().Single(static property => property.Information.Name.Value == "count");
        var secret = symbols.OfType<PropertyDefinition>().Single(static property => property.Information.Name.Value == "secret");

        await Assert.That(input.Validation).IsNotNull();
        await Assert.That(input.Validation!("ok")).IsNull();
        await Assert.That(input.Validation!("error")).IsEqualTo("input error");
        await Assert.That(count.Validation).IsNotNull();
        await Assert.That(count.Validation!(1)).IsNull();
        await Assert.That(count.Validation!(-1)).IsEqualTo("count error");
        await Assert.That(secret.Validation).IsNotNull();
        await Assert.That(secret.Validation!("open")).IsNull();
        await Assert.That(secret.Validation!("blocked")).IsEqualTo("secret error");
    }

    [Test]
    public async Task Multiple_Validators_Run_In_Declaration_Order()
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
                        .Add("Name", new Document("Name summary", "Name help"));

                [Property]
                [LongAlias("name")]
                [Validator(nameof(First))]
                [Validator(nameof(Second))]
                public string Name { get; set; } = string.Empty;

                public static string? First(string value)
                {
                    return value == "first" ? "first error" : null;
                }

                public static string? Second(string value)
                {
                    return value == "second" ? "second error" : null;
                }
            }
            """;

        var result = RunGenerator(source, "Fixtures.Command");
        var property = GetSymbols(result, "Fixtures.Command")
            .OfType<PropertyDefinition>()
            .Single(static item => item.Information.Name.Value == "name");

        await Assert.That(property.Validation).IsNotNull();
        await Assert.That(property.Validation!("ok")).IsNull();
        await Assert.That(property.Validation!("first")).IsEqualTo("first error");
        await Assert.That(property.Validation!("second")).IsEqualTo("second error");
    }

    [Test]
    public async Task Enum_Property_PossibleValues_Are_Exported_For_Root_And_Subcommand_Properties()
    {
        const string source = """
            using System.Collections.Immutable;
            using Kawayi.CommandLine.Abstractions;
            using Kawayi.CommandLine.Core.Attributes;

            namespace Fixtures;

            public enum RootMode
            {
                Basic = 0,
                Advanced = 1
            }

            public enum ChildMode
            {
                Follow = 0,
                Watch = 1
            }

            [ExportSymbols]
            public partial class ChildCommand : IDocumentExporter
            {
                public static ImmutableDictionary<string, Document> Documents { get; } =
                    ImmutableDictionary<string, Document>.Empty
                        .Add("Mode", new Document("Child mode summary", "Child mode help"));

                [Property]
                [LongAlias("mode")]
                public ChildMode Mode { get; set; }
            }

            [ExportSymbols]
            public partial class Command : IDocumentExporter
            {
                public static ImmutableDictionary<string, Document> Documents { get; } =
                    ImmutableDictionary<string, Document>.Empty
                        .Add("Mode", new Document("Root mode summary", "Root mode help"))
                        .Add("Name", new Document("Name summary", "Name help"))
                        .Add("Serve", new Document("Serve summary", "Serve help"));

                [Property]
                [LongAlias("mode")]
                public RootMode Mode { get; set; }

                [Property]
                [LongAlias("name")]
                public string Name { get; set; } = string.Empty;

                [Subcommand]
                public ChildCommand Serve { get; } = new();
            }
            """;

        var result = RunGenerator(source, "Fixtures.Command");
        var symbols = GetSymbols(result, "Fixtures.Command");
        var rootMode = symbols.OfType<PropertyDefinition>().Single(static property => property.Information.Name.Value == "mode");
        var name = symbols.OfType<PropertyDefinition>().Single(static property => property.Information.Name.Value == "name");
        var childSymbols = GetSymbols(result, "Fixtures.ChildCommand");
        var childMode = childSymbols.OfType<PropertyDefinition>().Single(static property => property.Information.Name.Value == "mode");

        await Assert.That(GetPossibleValueNames(rootMode)).IsEquivalentTo(["Basic", "Advanced"]);
        await Assert.That(name.PossibleValues).IsNull();
        await Assert.That(GetPossibleValueNames(childMode)).IsEquivalentTo(["Follow", "Watch"]);
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
        await Assert.That(names.Contains("private-argument")).IsTrue();
        await Assert.That(names.Contains("internal-option")).IsTrue();
        await Assert.That(names.Contains("protected-command")).IsTrue();
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
    public async Task Invalid_Validator_Target_ReportsDiagnostic()
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
                        .Add("Unbound", new Document("", ""))
                        .Add("Serve", new Document("", ""));

                [Validator(nameof(Validate))]
                public string Unbound { get; set; } = string.Empty;

                [Subcommand]
                [Validator(nameof(ValidateChild))]
                public ChildCommand Serve { get; } = new();

                public static string? Validate(string value)
                {
                    return null;
                }

                public static string? ValidateChild(ChildCommand value)
                {
                    return null;
                }
            }
            """;

        var result = RunGenerator(source, "Fixtures.Command", expectSuccessfulEmit: false);
        var diagnostics = GetGeneratorDiagnostics(result);

        await Assert.That(diagnostics.Count(static item => item.Id == "KCLG109")).IsEqualTo(2);
    }

    [Test]
    public async Task Invalid_Validator_Methods_ReportDiagnostic()
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
                        .Add("Missing", new Document("", ""))
                        .Add("Instance", new Document("", ""))
                        .Add("Generic", new Document("", ""))
                        .Add("WrongReturn", new Document("", ""))
                        .Add("WrongArity", new Document("", ""))
                        .Add("WrongType", new Document("", ""));

                [Property]
                [LongAlias("missing")]
                [Validator("MissingValidator")]
                public string Missing { get; set; } = string.Empty;

                [Property]
                [LongAlias("instance")]
                [Validator(nameof(InstanceValidator))]
                public string Instance { get; set; } = string.Empty;

                [Property]
                [LongAlias("generic")]
                [Validator(nameof(GenericValidator))]
                public string Generic { get; set; } = string.Empty;

                [Property]
                [LongAlias("wrong-return")]
                [Validator(nameof(WrongReturnValidator))]
                public string WrongReturn { get; set; } = string.Empty;

                [Property]
                [LongAlias("wrong-arity")]
                [Validator(nameof(WrongArityValidator))]
                public string WrongArity { get; set; } = string.Empty;

                [Property]
                [LongAlias("wrong-type")]
                [Validator(nameof(WrongTypeValidator))]
                public string WrongType { get; set; } = string.Empty;

                public string? InstanceValidator(string value)
                {
                    return null;
                }

                public static string? GenericValidator<T>(string value)
                {
                    return null;
                }

                public static bool WrongReturnValidator(string value)
                {
                    return false;
                }

                public static string? WrongArityValidator()
                {
                    return null;
                }

                public static string? WrongTypeValidator(int value)
                {
                    return null;
                }
            }
            """;

        var result = RunGenerator(source, "Fixtures.Command", expectSuccessfulEmit: false);
        var diagnostics = GetGeneratorDiagnostics(result);

        await Assert.That(diagnostics.Count(static item => item.Id == "KCLG110")).IsEqualTo(6);
    }

    [Test]
    public async Task Duplicate_Position_ReportsDiagnostic_And_CrossKind_Alias_Does_Not_Conflict()
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
        await Assert.That(diagnostics.Any(static item => item.Id == "KCLG108")).IsFalse();
    }

    [Test]
    public async Task Duplicate_Aliases_And_Surface_Names_ReportDiagnostics()
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
                        .Add("FirstName", new Document("", ""))
                        .Add("First_Name", new Document("", ""))
                        .Add("Mode", new Document("", ""))
                        .Add("Format", new Document("", ""))
                        .Add("Force", new Document("", ""))
                        .Add("Flag", new Document("", ""))
                        .Add("Serve", new Document("", ""))
                        .Add("Watch", new Document("", ""));

                [Property]
                [LongAlias("mode")]
                public bool FirstName { get; set; }

                [Property]
                [LongAlias("name")]
                public bool First_Name { get; set; }

                [Property]
                [LongAlias("dup")]
                public bool Mode { get; set; }

                [Property]
                [LongAlias("dup")]
                public bool Format { get; set; }

                [Property]
                [ShortAlias("f")]
                public bool Force { get; set; }

                [Property]
                [ShortAlias("f")]
                public bool Flag { get; set; }

                [Subcommand]
                public ChildCommand Serve { get; } = new();

                [Subcommand]
                [Alias("serve")]
                public ChildCommand Watch { get; } = new();
            }
            """;

        var result = RunGenerator(source, "Fixtures.Command", expectSuccessfulEmit: false);
        var diagnostics = GetGeneratorDiagnostics(result);

        await Assert.That(diagnostics.Count(static item => item.Id == "KCLG108")).IsEqualTo(4);
    }

    [Test]
    public async Task Required_Subcommand_ReportsDiagnostic_And_Attribute_Constructor_Throws()
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
                        .Add("Serve", new Document("", ""));

                [Subcommand(require: true)]
                public ChildCommand? Serve { get; set; }
            }
            """;

        var result = RunGenerator(source, "Fixtures.Command", expectSuccessfulEmit: false);
        var diagnostic = GetSingleDiagnostic(result, "KCLG113");

        await Assert.That(diagnostic.Severity).IsEqualTo(DiagnosticSeverity.Error);
        await Assert.That(() => new SubcommandAttribute(require: true)).Throws<NotSupportedException>();
    }

    [Test]
    public async Task Private_Tagged_Member_Document_Is_Available_When_Symbols_Are_Accessed()
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
        var property = GetSymbols(result, "Fixtures.Command")
            .OfType<PropertyDefinition>()
            .Single();

        await Assert.That(property.Information.Name.Value).IsEqualTo("private-option");
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

    private static string[] GetPossibleValueNames(PropertyDefinition definition)
    {
        if (definition.PossibleValues is not ICountablePossibleValues countable)
        {
            throw new InvalidOperationException(
                $"Expected countable possible values for '{definition.Information.Name.Value}'.");
        }

        return countable.Candidates.Cast<object?>()
            .Select(static candidate => candidate?.ToString() ?? string.Empty)
            .ToArray();
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
