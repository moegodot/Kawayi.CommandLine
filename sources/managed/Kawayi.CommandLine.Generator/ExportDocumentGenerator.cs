// Copyright (c) 2026 MoeGodot<me@kawayi.moe>.
// Licensed under the GNU Affero General Public License v3-or-later license.

using System.Collections.Immutable;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Kawayi.CommandLine.Generator;

/// <summary>
/// Generates <c>IDocumentExporter</c> implementations for types annotated with
/// <c>ExportDocumentAttribute</c> or <c>CommandAttribute</c>.
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed partial class ExportDocumentGenerator : IIncrementalGenerator
{
    private const string AttributeMetadataName =
        "Kawayi.CommandLine.Core.Attributes.ExportDocumentAttribute";

    private const string CommandAttributeMetadataName =
        "Kawayi.CommandLine.Core.Attributes.CommandAttribute";

    private const string DocumentExporterMetadataName =
        "Kawayi.CommandLine.Abstractions.IDocumentExporter";

    private static readonly Regex WhitespaceRegex = new(@"\s+", RegexOptions.Compiled);

    private static readonly DiagnosticDescriptor NonPartialDiagnostic = new(
        id: "KCLG001",
        title: "ExportDocument target must be partial",
        messageFormat: "Type '{0}' must be declared partial to generate an IDocumentExporter implementation",
        category: "Kawayi.CommandLine.Generator",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly SymbolDisplayFormat FullyQualifiedTypeFormat = new(
        globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Included,
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        miscellaneousOptions:
        SymbolDisplayMiscellaneousOptions.UseSpecialTypes |
        SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers |
        SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var targets = context.SyntaxProvider.CreateSyntaxProvider(
                predicate: static (node, _) => node is TypeDeclarationSyntax { AttributeLists.Count: > 0 },
                transform: static (syntaxContext, cancellationToken) =>
                    CreateTarget(syntaxContext, cancellationToken))
            .Where(static target => target is not null)
            .Collect()
            .SelectMany(static (targets, _) => DistinctTargets(targets));

        context.RegisterSourceOutput(targets, static (productionContext, target) =>
        {
            Emit(productionContext, target!);
        });
    }

    private static ExportTarget? CreateTarget(
        GeneratorSyntaxContext context,
        CancellationToken cancellationToken)
    {
        if (context.Node is not TypeDeclarationSyntax declaration ||
            context.SemanticModel.GetDeclaredSymbol(declaration, cancellationToken) is not INamedTypeSymbol typeSymbol)
        {
            return null;
        }

        var hasExportDocumentAttribute = HasAttribute(typeSymbol, AttributeMetadataName);
        var hasCommandAttribute = HasAttribute(typeSymbol, CommandAttributeMetadataName);

        if (!hasExportDocumentAttribute && !hasCommandAttribute)
        {
            return null;
        }

        if (!hasExportDocumentAttribute &&
            typeSymbol.AllInterfaces.Any(static item => item.ToDisplayString() == DocumentExporterMetadataName))
        {
            return null;
        }

        var missingPartialSymbol = FindFirstMissingPartialType(typeSymbol);
        var members = ImmutableArray.CreateBuilder<MemberDocument>(typeSymbol.GetMembers().Length);

        foreach (var member in typeSymbol.GetMembers())
        {
            cancellationToken.ThrowIfCancellationRequested();

            switch (member)
            {
                case IPropertySymbol propertySymbol
                    when propertySymbol is { IsIndexer: false, IsImplicitlyDeclared: false } &&
                         IsEligibleAccessibility(propertySymbol.DeclaredAccessibility):
                    members.Add(CreateMemberDocument(propertySymbol, cancellationToken));
                    break;
                case IFieldSymbol fieldSymbol
                    when !fieldSymbol.IsImplicitlyDeclared &&
                         IsEligibleAccessibility(fieldSymbol.DeclaredAccessibility):
                    members.Add(CreateMemberDocument(fieldSymbol, cancellationToken));
                    break;
            }
        }

        return new(typeSymbol, missingPartialSymbol, members.ToImmutable());
    }

    private static MemberDocument CreateMemberDocument(ISymbol memberSymbol, CancellationToken cancellationToken)
    {
        var (summary, remarks) = ExtractDocumentation(memberSymbol, cancellationToken);
        return new(memberSymbol.Name, summary, remarks);
    }

    private static (string Summary, string Remarks) ExtractDocumentation(
        ISymbol symbol,
        CancellationToken cancellationToken)
    {
        var xml = symbol.GetDocumentationCommentXml(expandIncludes: true, cancellationToken: cancellationToken);
        if (string.IsNullOrWhiteSpace(xml))
        {
            return (string.Empty, string.Empty);
        }

        try
        {
            var root = XElement.Parse(xml, LoadOptions.PreserveWhitespace);
            return (
                NormalizeDocumentationText(root.Element("summary")?.Value),
                NormalizeDocumentationText(root.Element("remarks")?.Value));
        }
        catch
        {
            return (string.Empty, string.Empty);
        }
    }

    private static string NormalizeDocumentationText(string? rawText)
    {
        if (string.IsNullOrWhiteSpace(rawText))
        {
            return string.Empty;
        }

        return WhitespaceRegex.Replace(rawText!.Trim(), " ");
    }

    private static bool IsEligibleAccessibility(Accessibility accessibility) =>
        accessibility is Accessibility.Public or Accessibility.Protected or Accessibility.ProtectedOrInternal;

    private static AttributeData? GetAttribute(ISymbol symbol, string metadataName)
    {
        return symbol.GetAttributes().FirstOrDefault(attribute =>
            attribute.AttributeClass?.ToDisplayString() == metadataName);
    }

    private static bool HasAttribute(ISymbol symbol, string metadataName)
    {
        return GetAttribute(symbol, metadataName) is not null;
    }

    private static INamedTypeSymbol? FindFirstMissingPartialType(INamedTypeSymbol typeSymbol)
    {
        for (var current = typeSymbol; current is not null; current = current.ContainingType)
        {
            if (!IsPartial(current))
            {
                return current;
            }
        }

        return null;
    }

    private static bool IsPartial(INamedTypeSymbol typeSymbol)
    {
        foreach (var syntaxReference in typeSymbol.DeclaringSyntaxReferences)
        {
            if (syntaxReference.GetSyntax() is TypeDeclarationSyntax declaration &&
                declaration.Modifiers.Any(SyntaxKind.PartialKeyword))
            {
                return true;
            }
        }

        return false;
    }

    private static void Emit(SourceProductionContext context, ExportTarget target)
    {
        if (target.MissingPartialSymbol is not null)
        {
            var location = target.MissingPartialSymbol.Locations.FirstOrDefault();
            context.ReportDiagnostic(Diagnostic.Create(
                NonPartialDiagnostic,
                location,
                target.MissingPartialSymbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)));
            return;
        }

        context.AddSource(GetHintName(target.TypeSymbol), SourceText.From(GenerateSource(target), Encoding.UTF8));
    }

    private static string GenerateSource(ExportTarget target)
    {
        var builder = new StringBuilder();
        builder.AppendLine("// <auto-generated/>");
        builder.AppendLine("#nullable enable");

        if (!target.TypeSymbol.ContainingNamespace.IsGlobalNamespace)
        {
            builder.Append("namespace ").Append(target.TypeSymbol.ContainingNamespace.ToDisplayString()).AppendLine();
            builder.AppendLine("{");
        }

        var containingTypes = GetContainingTypes(target.TypeSymbol);
        for (var i = 0; i < containingTypes.Length; i++)
        {
            AppendIndentedLine(builder, i, BuildTypeDeclaration(containingTypes[i], includeInterface: false));
            AppendIndentedLine(builder, i, "{");
        }

        AppendIndentedLine(builder, containingTypes.Length, BuildTypeDeclaration(target.TypeSymbol, includeInterface: true));
        AppendIndentedLine(builder, containingTypes.Length, "{");
        AppendDocumentsProperty(builder, target, containingTypes.Length + 1);
        AppendIndentedLine(builder, containingTypes.Length, "}");

        for (var i = containingTypes.Length - 1; i >= 0; i--)
        {
            AppendIndentedLine(builder, i, "}");
        }

        if (!target.TypeSymbol.ContainingNamespace.IsGlobalNamespace)
        {
            builder.AppendLine("}");
        }

        return builder.ToString();
    }

    private static void AppendDocumentsProperty(StringBuilder builder, ExportTarget target, int indentLevel)
    {
        const string propertyHeader =
            "public static global::System.Collections.Immutable.ImmutableDictionary<string, global::Kawayi.CommandLine.Abstractions.Document> Documents { get; } = ";

        if (target.Members.Length == 0)
        {
            AppendIndentedLine(
                builder,
                indentLevel,
                propertyHeader +
                "global::System.Collections.Immutable.ImmutableDictionary<string, global::Kawayi.CommandLine.Abstractions.Document>.Empty;");
            return;
        }

        AppendIndentedLine(builder, indentLevel, propertyHeader);
        AppendIndentedLine(
            builder,
            indentLevel + 1,
            "global::System.Collections.Immutable.ImmutableDictionary.CreateRange<string, global::Kawayi.CommandLine.Abstractions.Document>(");
        AppendIndentedLine(
            builder,
            indentLevel + 2,
            "new global::System.Collections.Generic.KeyValuePair<string, global::Kawayi.CommandLine.Abstractions.Document>[]");
        AppendIndentedLine(builder, indentLevel + 2, "{");

        for (var i = 0; i < target.Members.Length; i++)
        {
            var member = target.Members[i];
            var trailingComma = i == target.Members.Length - 1 ? string.Empty : ",";
            var entry =
                $"new({SymbolDisplay.FormatLiteral(member.Name, true)}, new global::Kawayi.CommandLine.Abstractions.Document({SymbolDisplay.FormatLiteral(member.Summary, true)}, {SymbolDisplay.FormatLiteral(member.Remarks, true)})){trailingComma}";
            AppendIndentedLine(builder, indentLevel + 3, entry);
        }

        AppendIndentedLine(builder, indentLevel + 2, "}");
        AppendIndentedLine(builder, indentLevel + 1, ");");
    }

    private static ImmutableArray<INamedTypeSymbol> GetContainingTypes(INamedTypeSymbol typeSymbol)
    {
        if (typeSymbol.ContainingType is null)
        {
            return [];
        }

        var builder = ImmutableArray.CreateBuilder<INamedTypeSymbol>();
        for (var current = typeSymbol.ContainingType; current is not null; current = current.ContainingType)
        {
            builder.Add(current);
        }

        builder.Reverse();
        return builder.ToImmutable();
    }

    private static string BuildTypeDeclaration(INamedTypeSymbol typeSymbol, bool includeInterface)
    {
        var builder = new StringBuilder();
        builder.Append(GetAccessibilityText(typeSymbol.DeclaredAccessibility));
        builder.Append(' ');

        if (typeSymbol.IsStatic)
        {
            builder.Append("static ");
        }
        else
        {
            if (typeSymbol.IsAbstract)
            {
                builder.Append("abstract ");
            }

            if (typeSymbol.IsSealed)
            {
                builder.Append("sealed ");
            }
        }

        builder.Append("partial ");
        builder.Append(typeSymbol.IsRecord ? "record " : "class ");
        builder.Append(typeSymbol.Name);

        if (typeSymbol.TypeParameters.Length > 0)
        {
            builder.Append('<');
            builder.Append(string.Join(", ", typeSymbol.TypeParameters.Select(static parameter => parameter.Name)));
            builder.Append('>');
        }

        if (includeInterface &&
            !typeSymbol.AllInterfaces.Any(static item => item.ToDisplayString() == DocumentExporterMetadataName))
        {
            builder.Append(" : global::Kawayi.CommandLine.Abstractions.IDocumentExporter");
        }

        foreach (var typeParameter in typeSymbol.TypeParameters)
        {
            var constraintClause = BuildConstraintClause(typeParameter);
            if (!string.IsNullOrEmpty(constraintClause))
            {
                builder.Append(' ');
                builder.Append(constraintClause);
            }
        }

        return builder.ToString();
    }

    private static string BuildConstraintClause(ITypeParameterSymbol typeParameter)
    {
        var constraints = new List<string>();
        if (typeParameter.HasUnmanagedTypeConstraint)
        {
            constraints.Add("unmanaged");
        }
        else if (typeParameter.HasValueTypeConstraint)
        {
            constraints.Add("struct");
        }
        else if (typeParameter.HasReferenceTypeConstraint)
        {
            constraints.Add(
                typeParameter.ReferenceTypeConstraintNullableAnnotation == NullableAnnotation.Annotated
                    ? "class?"
                    : "class");
        }
        else if (typeParameter.HasNotNullConstraint)
        {
            constraints.Add("notnull");
        }

        constraints.AddRange(typeParameter.ConstraintTypes.Select(static type => type.ToDisplayString(FullyQualifiedTypeFormat)));

        if (typeParameter.HasConstructorConstraint)
        {
            constraints.Add("new()");
        }

        return constraints.Count == 0
            ? string.Empty
            : $"where {typeParameter.Name} : {string.Join(", ", constraints)}";
    }

    private static string GetAccessibilityText(Accessibility accessibility) =>
        accessibility switch
        {
            Accessibility.Public => "public",
            Accessibility.Private => "private",
            Accessibility.Internal => "internal",
            Accessibility.Protected => "protected",
            Accessibility.ProtectedAndInternal => "private protected",
            Accessibility.ProtectedOrInternal => "protected internal",
            _ => "internal",
        };

    private static void AppendIndentedLine(StringBuilder builder, int indentLevel, string text)
    {
        builder.Append(' ', indentLevel * 4);
        builder.AppendLine(text);
    }

    private static string GetHintName(INamedTypeSymbol typeSymbol)
    {
        var metadataName = typeSymbol.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat);
        var sanitizedName = new string(metadataName.Select(static character =>
            char.IsLetterOrDigit(character) ? character : '_').ToArray());
        return $"{sanitizedName}.ExportDocument.g.cs";
    }

    private static ImmutableArray<ExportTarget> DistinctTargets(ImmutableArray<ExportTarget?> targets)
    {
        var builder = ImmutableArray.CreateBuilder<ExportTarget>(targets.Length);
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var target in targets)
        {
            if (target is null)
            {
                continue;
            }

            var key = target.TypeSymbol.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat);
            if (seen.Add(key))
            {
                builder.Add(target);
            }
        }

        return builder.ToImmutable();
    }

    private sealed class ExportTarget
    {
        public ExportTarget(
            INamedTypeSymbol typeSymbol,
            INamedTypeSymbol? missingPartialSymbol,
            ImmutableArray<MemberDocument> members)
        {
            TypeSymbol = typeSymbol;
            MissingPartialSymbol = missingPartialSymbol;
            Members = members;
        }

        public INamedTypeSymbol TypeSymbol { get; }

        public INamedTypeSymbol? MissingPartialSymbol { get; }

        public ImmutableArray<MemberDocument> Members { get; }
    }

    private sealed class MemberDocument
    {
        public MemberDocument(string name, string summary, string remarks)
        {
            Name = name;
            Summary = summary;
            Remarks = remarks;
        }

        public string Name { get; }

        public string Summary { get; }

        public string Remarks { get; }
    }
}
