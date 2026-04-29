// Copyright (c) 2026 MoeGodot<me@kawayi.moe>.
// Licensed under the GNU Affero General Public License v3-or-later license.

using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Kawayi.CommandLine.Generator;

/// <summary>
/// Generates <c>IParsingExporter</c> and <c>IParsable&lt;T&gt;</c> implementations for
/// types annotated with <c>ExportParsingAttribute</c> or <c>CommandAttribute</c>.
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed partial class ExportParsingGenerator : IIncrementalGenerator
{
    private const string ExportParsingAttributeMetadataName =
        "Kawayi.CommandLine.Core.Attributes.ExportParsingAttribute";

    private const string CommandAttributeMetadataName =
        "Kawayi.CommandLine.Core.Attributes.CommandAttribute";

    private const string ExportSymbolsAttributeMetadataName =
        "Kawayi.CommandLine.Core.Attributes.ExportSymbolsAttribute";

    private const string SymbolExporterMetadataName =
        "Kawayi.CommandLine.Abstractions.ISymbolExporter";

    private const string ParsingExporterMetadataName =
        "Kawayi.CommandLine.Abstractions.IParsingExporter";

    private const string ParsableMetadataName =
        "Kawayi.CommandLine.Abstractions.IParsable";

    private const string SubcommandAttributeMetadataName =
        "Kawayi.CommandLine.Core.Attributes.SubcommandAttribute";

    private static readonly DiagnosticDescriptor NonPartialDiagnostic = new(
        id: "KCLG201",
        title: "ExportParsing target must be partial",
        messageFormat: "Type '{0}' must be declared partial to generate parsing exports",
        category: "Kawayi.CommandLine.Generator",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor MissingSymbolExporterDiagnostic = new(
        id: "KCLG202",
        title: "ExportParsing target must provide symbols",
        messageFormat: "Type '{0}' must implement ISymbolExporter, or use ExportSymbolsAttribute or CommandAttribute to generate symbol exports, before parsing exports can be generated",
        category: "Kawayi.CommandLine.Generator",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor InvalidSubcommandExporterDiagnostic = new(
        id: "KCLG203",
        title: "Subcommand type must provide parsing exports",
        messageFormat: "Subcommand member '{0}' must target a type that implements IParsingExporter or is annotated with ExportParsingAttribute or CommandAttribute",
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

    private static readonly SymbolDisplayFormat FullyQualifiedNonNullableTypeFormat = new(
        globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Included,
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        miscellaneousOptions:
        SymbolDisplayMiscellaneousOptions.UseSpecialTypes |
        SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers);

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

        if (!HasAttribute(typeSymbol, ExportParsingAttributeMetadataName) &&
            !HasAttribute(typeSymbol, CommandAttributeMetadataName))
        {
            return null;
        }

        var diagnostics = ImmutableArray.CreateBuilder<DiagnosticInfo>();
        var missingPartialSymbol = FindFirstMissingPartialType(typeSymbol);
        var hasSymbolExporter =
            typeSymbol.AllInterfaces.Any(static item => item.ToDisplayString() == SymbolExporterMetadataName) ||
            HasAttribute(typeSymbol, ExportSymbolsAttributeMetadataName) ||
            HasAttribute(typeSymbol, CommandAttributeMetadataName);

        var subcommands = ImmutableArray.CreateBuilder<SubcommandBinding>();

        foreach (var propertySymbol in typeSymbol.GetMembers().OfType<IPropertySymbol>())
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (propertySymbol.IsImplicitlyDeclared || propertySymbol.IsStatic || propertySymbol.IsIndexer)
            {
                continue;
            }

            if (GetAttribute(propertySymbol, SubcommandAttributeMetadataName) is null)
            {
                continue;
            }

            if (!SupportsParsingExporter(propertySymbol.Type))
            {
                diagnostics.Add(new DiagnosticInfo(InvalidSubcommandExporterDiagnostic,
                                                   propertySymbol.Locations.FirstOrDefault(),
                                                   [propertySymbol.Name]));
                continue;
            }

            subcommands.Add(new SubcommandBinding(propertySymbol.Name,
                                                  propertySymbol.Type.ToDisplayString(FullyQualifiedNonNullableTypeFormat)));
        }

        return new ExportTarget(typeSymbol,
                                missingPartialSymbol,
                                hasSymbolExporter,
                                subcommands.ToImmutable(),
                                diagnostics.ToImmutable());
    }

    private static bool SupportsParsingExporter(ITypeSymbol typeSymbol)
    {
        if (typeSymbol is not INamedTypeSymbol namedType)
        {
            return false;
        }

        return namedType.AllInterfaces.Any(static item => item.ToDisplayString() == ParsingExporterMetadataName) ||
               HasAttribute(namedType, ExportParsingAttributeMetadataName) ||
               HasAttribute(namedType, CommandAttributeMetadataName);
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

        if (!target.HasSymbolExporter)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                MissingSymbolExporterDiagnostic,
                target.TypeSymbol.Locations.FirstOrDefault(),
                target.TypeSymbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)));
        }

        foreach (var diagnostic in target.Diagnostics)
        {
            context.ReportDiagnostic(Diagnostic.Create(diagnostic.Descriptor, diagnostic.Location, diagnostic.Arguments));
        }

        if (!target.HasSymbolExporter || !target.Diagnostics.IsDefaultOrEmpty)
        {
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
        AppendExportParsingMethod(builder, target, containingTypes.Length + 1);
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

    private static void AppendExportParsingMethod(StringBuilder builder, ExportTarget target, int indentLevel)
    {
        var typeNameLiteral = SymbolDisplay.FormatLiteral(
            target.TypeSymbol.ToDisplayString(FullyQualifiedTypeFormat),
            true);
        var selfTypeReference = BuildSelfTypeReference(target.TypeSymbol);

        AppendIndentedLine(
            builder,
            indentLevel,
            "public static global::Kawayi.CommandLine.Abstractions.IParsingBuilder ExportParsing(global::Kawayi.CommandLine.Abstractions.ParsingOptions parsingOptions)");
        AppendIndentedLine(builder, indentLevel, "{");
        AppendIndentedLine(builder, indentLevel + 1, "global::System.ArgumentNullException.ThrowIfNull(parsingOptions);");
        AppendIndentedLine(builder, indentLevel + 1, "var builder = new global::Kawayi.CommandLine.Core.ParsingBuilder(parsingOptions);");
        AppendIndentedLine(builder, indentLevel + 1, "foreach (var symbol in Symbols)");
        AppendIndentedLine(builder, indentLevel + 1, "{");
        AppendIndentedLine(builder, indentLevel + 2, "switch (symbol)");
        AppendIndentedLine(builder, indentLevel + 2, "{");
        AppendIndentedLine(builder, indentLevel + 3, "case global::Kawayi.CommandLine.Abstractions.ArgumentDefinition argument:");
        AppendIndentedLine(builder, indentLevel + 4, "builder.Argument.Add(argument);");
        AppendIndentedLine(builder, indentLevel + 4, "break;");
        AppendIndentedLine(builder, indentLevel + 3, "case global::Kawayi.CommandLine.Abstractions.PropertyDefinition property:");
        AppendIndentedLine(builder, indentLevel + 4, "builder.Properties[property.Information.Name.Value] = property;");
        AppendIndentedLine(builder, indentLevel + 4, "break;");
        AppendIndentedLine(builder, indentLevel + 3, "case global::Kawayi.CommandLine.Abstractions.CommandDefinition command:");
        AppendIndentedLine(builder, indentLevel + 4, "builder.SubcommandDefinitions[command.Information.Name.Value] = command;");
        AppendIndentedLine(builder, indentLevel + 4, "break;");
        AppendIndentedLine(builder, indentLevel + 3, "default:");
        AppendIndentedLine(
            builder,
            indentLevel + 4,
            $"throw new global::System.InvalidOperationException(\"Unsupported symbol type '\" + symbol.GetType().FullName + \"' was found in Symbols while exporting parsing metadata for \" + {typeNameLiteral} + \".\");");
        AppendIndentedLine(builder, indentLevel + 2, "}");
        AppendIndentedLine(builder, indentLevel + 1, "}");

        foreach (var subcommand in target.Subcommands)
        {
            var propertyNameExpression = GenerateCommandLineNameExpression(subcommand.PropertyName);
            AppendIndentedLine(
                builder,
                indentLevel + 1,
                $"builder.Subcommands[GetRequiredSubcommandKey(builder, {propertyNameExpression})] = {subcommand.TypeName}.ExportParsing(parsingOptions);");
        }

        AppendIndentedLine(builder, indentLevel + 1, "return builder;");
        AppendIndentedLine(builder, indentLevel, "}");
        AppendIndentedLine(builder, indentLevel, string.Empty);
        AppendIndentedLine(
            builder,
            indentLevel,
            $"public static global::Kawayi.CommandLine.Abstractions.ParsingResult CreateParsing(global::Kawayi.CommandLine.Abstractions.ParsingOptions options, global::System.Collections.Immutable.ImmutableArray<global::Kawayi.CommandLine.Abstractions.Token> arguments, {selfTypeReference} initialState)");
        AppendIndentedLine(builder, indentLevel, "{");
        AppendIndentedLine(builder, indentLevel + 1, "global::System.ArgumentNullException.ThrowIfNull(options);");
        AppendIndentedLine(builder, indentLevel + 1, "_ = initialState;");
        AppendIndentedLine(builder, indentLevel + 1, "var builder = ExportParsing(options);");
        AppendIndentedLine(builder, indentLevel + 1, "var snapshot = builder.Build();");
        AppendIndentedLine(builder,
                           indentLevel + 1,
                           "return global::Kawayi.CommandLine.Core.ParsingBuilder.CreateParsing(options, arguments, snapshot);");
        AppendIndentedLine(builder, indentLevel, "}");

        if (target.Subcommands.Length == 0)
        {
            return;
        }

        AppendIndentedLine(builder, indentLevel, string.Empty);
        AppendIndentedLine(
            builder,
            indentLevel,
            "private static string GetRequiredSubcommandKey(global::Kawayi.CommandLine.Abstractions.IParsingBuilder builder, string commandName)");
        AppendIndentedLine(builder, indentLevel, "{");
        AppendIndentedLine(
            builder,
            indentLevel + 1,
            "if (builder.SubcommandDefinitions.TryGetValue(commandName, out var definition) && definition is not null)");
        AppendIndentedLine(builder, indentLevel + 1, "{");
        AppendIndentedLine(builder, indentLevel + 2, "return definition.Information.Name.Value;");
        AppendIndentedLine(builder, indentLevel + 1, "}");
        AppendIndentedLine(
            builder,
            indentLevel + 1,
            $"throw new global::System.InvalidOperationException(\"Expected Symbols for \" + {typeNameLiteral} + \" to contain a CommandDefinition named '\" + commandName + \"' so the generated parsing exporter can attach the child parser.\");");
        AppendIndentedLine(builder, indentLevel, "}");
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

        if (includeInterface)
        {
            var interfaces = ImmutableArray.CreateBuilder<string>(2);

            if (!typeSymbol.AllInterfaces.Any(static item => item.ToDisplayString() == ParsingExporterMetadataName))
            {
                interfaces.Add("global::Kawayi.CommandLine.Abstractions.IParsingExporter");
            }

            if (!HasParsableSelfInterface(typeSymbol))
            {
                interfaces.Add($"global::Kawayi.CommandLine.Abstractions.IParsable<{BuildSelfTypeReference(typeSymbol)}>");
            }

            if (interfaces.Count > 0)
            {
                builder.Append(" : ");
                builder.Append(string.Join(", ", interfaces));
            }
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

    private static bool HasParsableSelfInterface(INamedTypeSymbol typeSymbol)
    {
        foreach (var implementedInterface in typeSymbol.AllInterfaces)
        {
            if (!string.Equals(implementedInterface.OriginalDefinition.ToDisplayString(),
                               ParsableMetadataName + "<T>",
                               StringComparison.Ordinal))
            {
                continue;
            }

            if (implementedInterface.TypeArguments.Length == 1 &&
                SymbolEqualityComparer.Default.Equals(implementedInterface.TypeArguments[0], typeSymbol))
            {
                return true;
            }
        }

        return false;
    }

    private static string BuildSelfTypeReference(INamedTypeSymbol typeSymbol)
    {
        if (typeSymbol.TypeParameters.Length == 0)
        {
            return typeSymbol.Name;
        }

        return $"{typeSymbol.Name}<{string.Join(", ", typeSymbol.TypeParameters.Select(static parameter => parameter.Name))}>";
    }

    private static string GenerateCommandLineNameExpression(string memberName)
    {
        return $"global::Kawayi.CommandLine.Abstractions.CaseConverter.Pascal2Kebab({SymbolDisplay.FormatLiteral(memberName, true)})";
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
        return $"{sanitizedName}.ExportParsing.g.cs";
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
            bool hasSymbolExporter,
            ImmutableArray<SubcommandBinding> subcommands,
            ImmutableArray<DiagnosticInfo> diagnostics)
        {
            TypeSymbol = typeSymbol;
            MissingPartialSymbol = missingPartialSymbol;
            HasSymbolExporter = hasSymbolExporter;
            Subcommands = subcommands;
            Diagnostics = diagnostics;
        }

        public INamedTypeSymbol TypeSymbol { get; }

        public INamedTypeSymbol? MissingPartialSymbol { get; }

        public bool HasSymbolExporter { get; }

        public ImmutableArray<SubcommandBinding> Subcommands { get; }

        public ImmutableArray<DiagnosticInfo> Diagnostics { get; }
    }

    private sealed class SubcommandBinding
    {
        public SubcommandBinding(string propertyName, string typeName)
        {
            PropertyName = propertyName;
            TypeName = typeName;
        }

        public string PropertyName { get; }

        public string TypeName { get; }
    }

    private sealed class DiagnosticInfo
    {
        public DiagnosticInfo(DiagnosticDescriptor descriptor, Location? location, object?[] arguments)
        {
            Descriptor = descriptor;
            Location = location;
            Arguments = arguments;
        }

        public DiagnosticDescriptor Descriptor { get; }

        public Location? Location { get; }

        public object?[] Arguments { get; }
    }
}
