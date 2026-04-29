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
/// Generates <c>ISymbolExporter</c> implementations for types annotated with
/// <c>ExportSymbolsAttribute</c> or <c>CommandAttribute</c>.
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed partial class ExportSymbolsGenerator : IIncrementalGenerator
{
    private const string ExportSymbolsAttributeMetadataName =
        "Kawayi.CommandLine.Core.Attributes.ExportSymbolsAttribute";

    private const string CommandAttributeMetadataName =
        "Kawayi.CommandLine.Core.Attributes.CommandAttribute";

    private const string ExportDocumentAttributeMetadataName =
        "Kawayi.CommandLine.Core.Attributes.ExportDocumentAttribute";

    private const string DocumentExporterMetadataName =
        "Kawayi.CommandLine.Abstractions.IDocumentExporter";

    private const string ArgumentAttributeMetadataName =
        "Kawayi.CommandLine.Core.Attributes.ArgumentAttribute";

    private const string ValueRangeAttributeMetadataName =
        "Kawayi.CommandLine.Core.Attributes.ValueRangeAttribute";

    private const string PropertyAttributeMetadataName =
        "Kawayi.CommandLine.Core.Attributes.PropertyAttribute";

    private const string SubcommandAttributeMetadataName =
        "Kawayi.CommandLine.Core.Attributes.SubcommandAttribute";

    private const string LongAliasAttributeMetadataName =
        "Kawayi.CommandLine.Core.Attributes.LongAliasAttribute";

    private const string ShortAliasAttributeMetadataName =
        "Kawayi.CommandLine.Core.Attributes.ShortAliasAttribute";

    private const string AliasAttributeMetadataName =
        "Kawayi.CommandLine.Core.Attributes.AliasAttribute";

    private const string ValidatorAttributeMetadataName =
        "Kawayi.CommandLine.Core.Attributes.ValidatorAttribute";

    private static readonly DiagnosticDescriptor NonPartialDiagnostic = new(
        id: "KCLG101",
        title: "ExportSymbols target must be partial",
        messageFormat: "Type '{0}' must be declared partial to generate an ISymbolExporter implementation",
        category: "Kawayi.CommandLine.Generator",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor MissingDocumentExporterDiagnostic = new(
        id: "KCLG102",
        title: "ExportSymbols target must provide documents",
        messageFormat: "Type '{0}' must implement IDocumentExporter, or use ExportDocumentAttribute or CommandAttribute to generate document exports, before symbol exports can be generated",
        category: "Kawayi.CommandLine.Generator",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor MultipleRoleDiagnostic = new(
        id: "KCLG103",
        title: "Member cannot declare multiple symbol roles",
        messageFormat: "Member '{0}' cannot be annotated with multiple symbol role attributes at the same time",
        category: "Kawayi.CommandLine.Generator",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor MissingValueRangeDiagnostic = new(
        id: "KCLG104",
        title: "Argument is missing ValueRangeAttribute",
        messageFormat: "Member '{0}' is annotated with ArgumentAttribute but is missing ValueRangeAttribute",
        category: "Kawayi.CommandLine.Generator",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor InvalidPropertyAliasDiagnostic = new(
        id: "KCLG105",
        title: "Property aliases require PropertyAttribute",
        messageFormat: "Member '{0}' uses LongAliasAttribute or ShortAliasAttribute but is not annotated with PropertyAttribute",
        category: "Kawayi.CommandLine.Generator",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor InvalidSubcommandAliasDiagnostic = new(
        id: "KCLG106",
        title: "Subcommand aliases require SubcommandAttribute",
        messageFormat: "Member '{0}' uses AliasAttribute but is not annotated with SubcommandAttribute",
        category: "Kawayi.CommandLine.Generator",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor DuplicateArgumentPositionDiagnostic = new(
        id: "KCLG107",
        title: "Argument position must be unique",
        messageFormat: "Argument position '{0}' is used more than once in the current type; conflicting member: '{1}'",
        category: "Kawayi.CommandLine.Generator",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor AliasConflictDiagnostic = new(
        id: "KCLG108",
        title: "Alias or subcommand name conflict",
        messageFormat: "Name or alias '{0}' is used more than once in the current type; conflicting members: '{1}' and '{2}'",
        category: "Kawayi.CommandLine.Generator",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor InvalidValidatorTargetDiagnostic = new(
        id: "KCLG109",
        title: "ValidatorAttribute requires ArgumentAttribute or PropertyAttribute",
        messageFormat: "Member '{0}' uses ValidatorAttribute but is not annotated with ArgumentAttribute or PropertyAttribute",
        category: "Kawayi.CommandLine.Generator",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor InvalidValidatorMethodDiagnostic = new(
        id: "KCLG110",
        title: "Validator method must be a matching static method",
        messageFormat: "Validator '{0}' for member '{1}' must resolve to exactly one static non-generic method returning string? and accepting '{2}'",
        category: "Kawayi.CommandLine.Generator",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor NonNullableSubcommandDiagnostic = new(
        id: "KCLG111",
        title: "Subcommand property should be nullable",
        messageFormat: "Subcommand member '{0}' should be nullable because binding assigns null when the subcommand is not selected",
        category: "Kawayi.CommandLine.Generator",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    private static readonly SymbolDisplayFormat FullyQualifiedTypeFormat = new(
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
                transform: static (syntaxContext, cancellationToken) => CreateTarget(syntaxContext, cancellationToken))
            .Where(static target => target is not null)
            .Collect()
            .SelectMany(static (targets, _) => DistinctTargets(targets));

        context.RegisterSourceOutput(targets, static (productionContext, target) => Emit(productionContext, target!));
    }

    private static ExportTarget? CreateTarget(GeneratorSyntaxContext context, CancellationToken cancellationToken)
    {
        if (context.Node is not TypeDeclarationSyntax declaration ||
            context.SemanticModel.GetDeclaredSymbol(declaration, cancellationToken) is not INamedTypeSymbol typeSymbol)
        {
            return null;
        }

        if (!HasAttribute(typeSymbol, ExportSymbolsAttributeMetadataName) &&
            !HasAttribute(typeSymbol, CommandAttributeMetadataName))
        {
            return null;
        }

        var diagnostics = ImmutableArray.CreateBuilder<DiagnosticInfo>();
        var missingPartialSymbol = FindFirstMissingPartialType(typeSymbol);
        var hasDocumentExporter =
            typeSymbol.AllInterfaces.Any(static item => item.ToDisplayString() == DocumentExporterMetadataName) ||
            HasAttribute(typeSymbol, ExportDocumentAttributeMetadataName) ||
            HasAttribute(typeSymbol, CommandAttributeMetadataName);

        var exports = ImmutableArray.CreateBuilder<MemberExport>();
        var argumentPositions = new Dictionary<int, MemberExport>();
        var nameRegistry = new Dictionary<string, MemberExport>(StringComparer.Ordinal);
        var declarationOrder = 0;

        foreach (var propertySymbol in typeSymbol.GetMembers().OfType<IPropertySymbol>())
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (propertySymbol.IsImplicitlyDeclared || propertySymbol.IsStatic || propertySymbol.IsIndexer)
            {
                continue;
            }

            var roleAttribute = GetRoleAttribute(propertySymbol);
            var valueRangeAttribute = GetAttribute(propertySymbol, ValueRangeAttributeMetadataName);
            var longAliasAttributes = GetAttributes(propertySymbol, LongAliasAttributeMetadataName);
            var shortAliasAttributes = GetAttributes(propertySymbol, ShortAliasAttributeMetadataName);
            var subcommandAliasAttributes = GetAttributes(propertySymbol, AliasAttributeMetadataName);
            var validatorAttributes = GetAttributes(propertySymbol, ValidatorAttributeMetadataName);
            var roleCount = (roleAttribute.Argument is null ? 0 : 1)
                            + (roleAttribute.Property is null ? 0 : 1)
                            + (roleAttribute.Subcommand is null ? 0 : 1);

            if (roleCount == 0)
            {
                ReportInvalidAliasesIfNeeded(diagnostics, propertySymbol, longAliasAttributes, shortAliasAttributes, subcommandAliasAttributes);
                ReportInvalidValidatorTargetIfNeeded(diagnostics, propertySymbol, validatorAttributes);
                continue;
            }

            if (roleCount > 1)
            {
                diagnostics.Add(new DiagnosticInfo(MultipleRoleDiagnostic, propertySymbol.Locations.FirstOrDefault(), [propertySymbol.Name]));
                continue;
            }

            if (roleAttribute.Argument is not null)
            {
                if (!longAliasAttributes.IsDefaultOrEmpty || !shortAliasAttributes.IsDefaultOrEmpty)
                {
                    diagnostics.Add(new DiagnosticInfo(InvalidPropertyAliasDiagnostic,
                                                       propertySymbol.Locations.FirstOrDefault(),
                                                       [propertySymbol.Name]));
                }

                if (!subcommandAliasAttributes.IsDefaultOrEmpty)
                {
                    diagnostics.Add(new DiagnosticInfo(InvalidSubcommandAliasDiagnostic,
                                                       propertySymbol.Locations.FirstOrDefault(),
                                                       [propertySymbol.Name]));
                }

                if (valueRangeAttribute is null)
                {
                    diagnostics.Add(new DiagnosticInfo(MissingValueRangeDiagnostic,
                                                       propertySymbol.Locations.FirstOrDefault(),
                                                       [propertySymbol.Name]));
                    continue;
                }

                var export = CreateArgumentExport(propertySymbol,
                                                 roleAttribute.Argument,
                                                 valueRangeAttribute,
                                                 ResolveValidators(typeSymbol, propertySymbol, validatorAttributes, diagnostics),
                                                 declarationOrder++);
                exports.Add(export);

                if (argumentPositions.TryGetValue(export.ArgumentPosition!.Value, out var existingArgument))
                {
                    diagnostics.Add(new DiagnosticInfo(DuplicateArgumentPositionDiagnostic,
                                                       propertySymbol.Locations.FirstOrDefault(),
                                                       [export.ArgumentPosition.Value, existingArgument.MemberName]));
                }
                else
                {
                    argumentPositions[export.ArgumentPosition.Value] = export;
                }

                continue;
            }

            if (roleAttribute.Property is not null)
            {
                if (!subcommandAliasAttributes.IsDefaultOrEmpty)
                {
                    diagnostics.Add(new DiagnosticInfo(InvalidSubcommandAliasDiagnostic,
                                                       propertySymbol.Locations.FirstOrDefault(),
                                                       [propertySymbol.Name]));
                }

                var export = CreatePropertyExport(propertySymbol,
                                                  roleAttribute.Property,
                                                  valueRangeAttribute,
                                                  longAliasAttributes,
                                                  shortAliasAttributes,
                                                  ResolveValidators(typeSymbol, propertySymbol, validatorAttributes, diagnostics),
                                                  declarationOrder++);
                exports.Add(export);
                RegisterConflicts(diagnostics, nameRegistry, export);
                continue;
            }

            ReportInvalidValidatorTargetIfNeeded(diagnostics, propertySymbol, validatorAttributes);

            if (!longAliasAttributes.IsDefaultOrEmpty || !shortAliasAttributes.IsDefaultOrEmpty)
            {
                diagnostics.Add(new DiagnosticInfo(InvalidPropertyAliasDiagnostic,
                                                   propertySymbol.Locations.FirstOrDefault(),
                                                   [propertySymbol.Name]));
            }

            ReportNonNullableSubcommandIfNeeded(diagnostics, propertySymbol);

            var subcommandExport = CreateSubcommandExport(propertySymbol,
                                                         roleAttribute.Subcommand!,
                                                         subcommandAliasAttributes,
                                                         declarationOrder++);
            exports.Add(subcommandExport);
            RegisterConflicts(diagnostics, nameRegistry, subcommandExport);
        }

        return new ExportTarget(typeSymbol,
                                missingPartialSymbol,
                                hasDocumentExporter,
                                exports.ToImmutable(),
                                diagnostics.ToImmutable());
    }

    private static void ReportInvalidAliasesIfNeeded(
        ImmutableArray<DiagnosticInfo>.Builder diagnostics,
        IPropertySymbol propertySymbol,
        ImmutableArray<AttributeData> longAliasAttributes,
        ImmutableArray<AttributeData> shortAliasAttributes,
        ImmutableArray<AttributeData> subcommandAliasAttributes)
    {
        if (!longAliasAttributes.IsDefaultOrEmpty || !shortAliasAttributes.IsDefaultOrEmpty)
        {
            diagnostics.Add(new DiagnosticInfo(InvalidPropertyAliasDiagnostic,
                                               propertySymbol.Locations.FirstOrDefault(),
                                               [propertySymbol.Name]));
        }

        if (!subcommandAliasAttributes.IsDefaultOrEmpty)
        {
            diagnostics.Add(new DiagnosticInfo(InvalidSubcommandAliasDiagnostic,
                                               propertySymbol.Locations.FirstOrDefault(),
                                               [propertySymbol.Name]));
        }
    }

    private static void ReportInvalidValidatorTargetIfNeeded(
        ImmutableArray<DiagnosticInfo>.Builder diagnostics,
        IPropertySymbol propertySymbol,
        ImmutableArray<AttributeData> validatorAttributes)
    {
        if (validatorAttributes.IsDefaultOrEmpty)
        {
            return;
        }

        diagnostics.Add(new DiagnosticInfo(InvalidValidatorTargetDiagnostic,
                                           propertySymbol.Locations.FirstOrDefault(),
                                           [propertySymbol.Name]));
    }

    private static void ReportNonNullableSubcommandIfNeeded(
        ImmutableArray<DiagnosticInfo>.Builder diagnostics,
        IPropertySymbol propertySymbol)
    {
        if (propertySymbol.Type.IsReferenceType &&
            propertySymbol.Type.NullableAnnotation == NullableAnnotation.NotAnnotated)
        {
            diagnostics.Add(new DiagnosticInfo(NonNullableSubcommandDiagnostic,
                                               propertySymbol.Locations.FirstOrDefault(),
                                               [propertySymbol.Name]));
        }
    }

    private static void RegisterConflicts(
        ImmutableArray<DiagnosticInfo>.Builder diagnostics,
        Dictionary<string, MemberExport> nameRegistry,
        MemberExport export)
    {
        foreach (var key in export.ConflictKeys)
        {
            if (nameRegistry.TryGetValue(key, out var existing))
            {
                diagnostics.Add(new DiagnosticInfo(AliasConflictDiagnostic,
                                                   export.Location,
                                                   [key, existing.MemberName, export.MemberName]));
                continue;
            }

            nameRegistry[key] = export;
        }
    }

    private static ImmutableArray<ValidatorExport> ResolveValidators(
        INamedTypeSymbol typeSymbol,
        IPropertySymbol propertySymbol,
        ImmutableArray<AttributeData> validatorAttributes,
        ImmutableArray<DiagnosticInfo>.Builder diagnostics)
    {
        if (validatorAttributes.IsDefaultOrEmpty)
        {
            return [];
        }

        var validators = ImmutableArray.CreateBuilder<ValidatorExport>(validatorAttributes.Length);
        foreach (var validatorAttribute in validatorAttributes)
        {
            var validatorName = GetAttributeString(validatorAttribute, 0, string.Empty);
            var matches = typeSymbol.GetMembers(validatorName)
                .OfType<IMethodSymbol>()
                .Where(method => IsMatchingValidator(method, propertySymbol.Type))
                .ToArray();

            if (matches.Length != 1)
            {
                diagnostics.Add(new DiagnosticInfo(InvalidValidatorMethodDiagnostic,
                                                   propertySymbol.Locations.FirstOrDefault(),
                                                   [validatorName, propertySymbol.Name, propertySymbol.Type.ToDisplayString(FullyQualifiedTypeFormat)]));
                continue;
            }

            validators.Add(new ValidatorExport(EscapeIdentifier(matches[0].Name)));
        }

        return validators.ToImmutable();
    }

    private static bool IsMatchingValidator(IMethodSymbol methodSymbol, ITypeSymbol valueType)
    {
        return methodSymbol.IsStatic &&
               !methodSymbol.IsGenericMethod &&
               methodSymbol.ReturnType.SpecialType == SpecialType.System_String &&
               methodSymbol.Parameters.Length == 1 &&
               SymbolEqualityComparer.Default.Equals(methodSymbol.Parameters[0].Type, valueType);
    }

    private static MemberExport CreateArgumentExport(
        IPropertySymbol propertySymbol,
        AttributeData argumentAttribute,
        AttributeData valueRangeAttribute,
        ImmutableArray<ValidatorExport> validators,
        int declarationOrder)
    {
        var position = GetAttributeInt(argumentAttribute, 0, 0);
        var requirement = GetAttributeBool(argumentAttribute, 1, false);
        var visible = GetAttributeBool(argumentAttribute, 2, true);
        var minimum = GetAttributeInt(valueRangeAttribute, 0, 0);
        var maximum = GetAttributeInt(valueRangeAttribute, 1, 0);

        return new MemberExport(MemberKind.Argument,
                                propertySymbol.Name,
                                propertySymbol.Type.ToDisplayString(FullyQualifiedTypeFormat),
                                requirement,
                                visible,
                                [],
                                [],
                                null,
                                minimum,
                                maximum,
                                propertySymbol.Locations.FirstOrDefault(),
                                declarationOrder,
                                position,
                                null,
                                null,
                                validators);
    }

    private static MemberExport CreatePropertyExport(
        IPropertySymbol propertySymbol,
        AttributeData propertyAttribute,
        AttributeData? valueRangeAttribute,
        ImmutableArray<AttributeData> longAliasAttributes,
        ImmutableArray<AttributeData> shortAliasAttributes,
        ImmutableArray<ValidatorExport> validators,
        int declarationOrder)
    {
        var requirement = GetAttributeBool(propertyAttribute, 0, false);
        var visible = GetAttributeBool(propertyAttribute, 1, true);
        var valueName = GetAttributeNullableString(propertyAttribute, 2);
        var minimum = valueRangeAttribute is null ? default(int?) : GetAttributeInt(valueRangeAttribute, 0, 0);
        var maximum = valueRangeAttribute is null ? default(int?) : GetAttributeInt(valueRangeAttribute, 1, 0);
        var longAliases = CreateAliasEntries(longAliasAttributes);
        var shortAliases = CreateAliasEntries(shortAliasAttributes);
        var enumPossibleValuesExpression = propertySymbol.Type.TypeKind == TypeKind.Enum
            ? $"new global::Kawayi.CommandLine.Abstractions.CountablePossibleValues<string>(global::System.Collections.Immutable.ImmutableArray.CreateRange<string>(global::System.Enum.GetNames(typeof({propertySymbol.Type.ToDisplayString(FullyQualifiedTypeFormat)}))))"
            : null;
        var conflictKeys = longAliases.Select(static alias => alias.Name)
            .Concat(shortAliases.Select(static alias => alias.Name))
            .ToImmutableArray();

        return new MemberExport(MemberKind.Property,
                                propertySymbol.Name,
                                propertySymbol.Type.ToDisplayString(FullyQualifiedTypeFormat),
                                requirement,
                                visible,
                                longAliases,
                                shortAliases,
                                valueName,
                                minimum,
                                maximum,
                                propertySymbol.Locations.FirstOrDefault(),
                                declarationOrder,
                                null,
                                conflictKeys,
                                enumPossibleValuesExpression,
                                validators);
    }

    private static MemberExport CreateSubcommandExport(
        IPropertySymbol propertySymbol,
        AttributeData subcommandAttribute,
        ImmutableArray<AttributeData> aliasAttributes,
        int declarationOrder)
    {
        var visible = GetAttributeBool(subcommandAttribute, 1, true);
        var aliases = CreateAliasEntries(aliasAttributes);
        var conflictKeys = ImmutableArray.CreateBuilder<string>(aliases.Length + 1);
        conflictKeys.Add(propertySymbol.Name);
        conflictKeys.AddRange(aliases.Select(static alias => alias.Name));

        return new MemberExport(MemberKind.Subcommand,
                                propertySymbol.Name,
                                propertySymbol.Type.ToDisplayString(FullyQualifiedTypeFormat),
                                visibleRequirement: false,
                                visible,
                                aliases,
                                [],
                                null,
                                null,
                                null,
                                propertySymbol.Locations.FirstOrDefault(),
                                declarationOrder,
                                null,
                                conflictKeys.ToImmutable());
    }

    private static ImmutableArray<AliasEntry> CreateAliasEntries(ImmutableArray<AttributeData> attributes)
    {
        if (attributes.IsDefaultOrEmpty)
        {
            return [];
        }

        return [.. attributes.Select(static attribute =>
                new AliasEntry(GetAttributeString(attribute, 0, string.Empty),
                               GetAttributeBool(attribute, 1, true)))
            .OrderBy(static alias => alias.Name, StringComparer.Ordinal)];
    }

    private static (AttributeData? Argument, AttributeData? Property, AttributeData? Subcommand) GetRoleAttribute(IPropertySymbol propertySymbol)
    {
        return (GetAttribute(propertySymbol, ArgumentAttributeMetadataName),
                GetAttribute(propertySymbol, PropertyAttributeMetadataName),
                GetAttribute(propertySymbol, SubcommandAttributeMetadataName));
    }

    private static AttributeData? GetAttribute(ISymbol symbol, string metadataName)
    {
        return symbol.GetAttributes().FirstOrDefault(attribute =>
            attribute.AttributeClass?.ToDisplayString() == metadataName);
    }

    private static ImmutableArray<AttributeData> GetAttributes(ISymbol symbol, string metadataName)
    {
        return [.. symbol.GetAttributes().Where(attribute =>
            attribute.AttributeClass?.ToDisplayString() == metadataName)];
    }

    private static bool HasAttribute(ISymbol symbol, string metadataName)
    {
        return GetAttribute(symbol, metadataName) is not null;
    }

    private static int GetAttributeInt(AttributeData attribute, int index, int defaultValue)
    {
        if (attribute.ConstructorArguments.Length <= index)
        {
            return defaultValue;
        }

        return attribute.ConstructorArguments[index].Value is int value ? value : defaultValue;
    }

    private static bool GetAttributeBool(AttributeData attribute, int index, bool defaultValue)
    {
        if (attribute.ConstructorArguments.Length <= index)
        {
            return defaultValue;
        }

        return attribute.ConstructorArguments[index].Value is bool value ? value : defaultValue;
    }

    private static string GetAttributeString(AttributeData attribute, int index, string defaultValue)
    {
        if (attribute.ConstructorArguments.Length <= index)
        {
            return defaultValue;
        }

        return attribute.ConstructorArguments[index].Value as string ?? defaultValue;
    }

    private static string? GetAttributeNullableString(AttributeData attribute, int index)
    {
        if (attribute.ConstructorArguments.Length <= index)
        {
            return null;
        }

        return attribute.ConstructorArguments[index].Value as string;
    }

    private static string EscapeIdentifier(string identifier)
    {
        return SyntaxFacts.GetKeywordKind(identifier) == SyntaxKind.None &&
               SyntaxFacts.GetContextualKeywordKind(identifier) == SyntaxKind.None
            ? identifier
            : "@" + identifier;
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
            context.ReportDiagnostic(Diagnostic.Create(
                NonPartialDiagnostic,
                target.MissingPartialSymbol.Locations.FirstOrDefault(),
                target.MissingPartialSymbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)));
            return;
        }

        if (!target.HasDocumentExporter)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                MissingDocumentExporterDiagnostic,
                target.TypeSymbol.Locations.FirstOrDefault(),
                target.TypeSymbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)));
            return;
        }

        if (!target.Diagnostics.IsDefaultOrEmpty)
        {
            foreach (var diagnostic in target.Diagnostics)
            {
                context.ReportDiagnostic(Diagnostic.Create(diagnostic.Descriptor, diagnostic.Location, diagnostic.Args));
            }

            if (target.Diagnostics.Any(static diagnostic =>
                    diagnostic.Descriptor.DefaultSeverity == DiagnosticSeverity.Error))
            {
                return;
            }
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
        AppendSymbolsProperty(builder, target, containingTypes.Length + 1);
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

    private static void AppendSymbolsProperty(StringBuilder builder, ExportTarget target, int indentLevel)
    {
        AppendIndentedLine(builder, indentLevel, "/// <summary>");
        AppendIndentedLine(builder, indentLevel, "/// Gets the generated command-line symbol definitions for this command type.");
        AppendIndentedLine(builder, indentLevel, "/// </summary>");
        const string propertyHeader =
            "public static global::System.Collections.Immutable.ImmutableArray<global::Kawayi.CommandLine.Abstractions.Symbol> Symbols { get; } = ";

        if (target.Members.IsDefaultOrEmpty)
        {
            AppendIndentedLine(
                builder,
                indentLevel,
                propertyHeader +
                "global::System.Collections.Immutable.ImmutableArray<global::Kawayi.CommandLine.Abstractions.Symbol>.Empty;");
            return;
        }

        AppendIndentedLine(builder, indentLevel, propertyHeader);
        AppendIndentedLine(
            builder,
            indentLevel + 1,
            "global::System.Collections.Immutable.ImmutableArray.Create<global::Kawayi.CommandLine.Abstractions.Symbol>(");

        var orderedMembers = OrderMembers(target.Members);
        for (var i = 0; i < orderedMembers.Length; i++)
        {
            var trailingComma = i == orderedMembers.Length - 1 ? string.Empty : ",";
            AppendIndentedLine(builder, indentLevel + 2, GenerateSymbolExpression(orderedMembers[i]) + trailingComma);
        }

        AppendIndentedLine(builder, indentLevel + 1, ");");
    }

    private static ImmutableArray<MemberExport> OrderMembers(ImmutableArray<MemberExport> members)
    {
        return [.. members.Where(static member => member.Kind == MemberKind.Argument)
            .OrderBy(static member => member.ArgumentPosition)
            .ThenBy(static member => member.DeclarationOrder)
            .Concat(members.Where(static member => member.Kind == MemberKind.Property)
                .OrderBy(static member => member.DeclarationOrder))
            .Concat(members.Where(static member => member.Kind == MemberKind.Subcommand)
                .OrderBy(static member => member.DeclarationOrder))];
    }

    private static string GenerateSymbolExpression(MemberExport member)
    {
        return member.Kind switch
        {
            MemberKind.Argument => GenerateArgumentExpression(member),
            MemberKind.Property => GeneratePropertyExpression(member),
            MemberKind.Subcommand => GenerateSubcommandExpression(member),
            _ => throw new ArgumentOutOfRangeException(nameof(member.Kind)),
        };
    }

    private static string GenerateArgumentExpression(MemberExport member)
    {
        var argumentExpression =
            $"new global::Kawayi.CommandLine.Abstractions.ArgumentDefinition({GenerateDefinitionInformationExpression(member)}, null, new global::Kawayi.CommandLine.Abstractions.ValueRange({member.ValueRangeMinimum}, {member.ValueRangeMaximum}), typeof({member.TypeName}), {FormatBool(member.VisibleRequirement)})";
        var initializers = new List<string>(1);
        AddValidationInitializer(initializers, member);

        return ApplyInitializers(argumentExpression, initializers);
    }

    private static string GeneratePropertyExpression(MemberExport member)
    {
        var propertyExpression =
            $"new global::Kawayi.CommandLine.Abstractions.PropertyDefinition({GenerateDefinitionInformationExpression(member)}, {GenerateAliasDictionaryExpression(member.LongAliases)}, {GenerateAliasDictionaryExpression(member.ShortAliases)}, null, typeof({member.TypeName}), {FormatBool(member.VisibleRequirement)})";
        var initializers = new List<string>(2);

        if (member.ValueName is not null)
        {
            initializers.Add($"ValueName = {SymbolDisplay.FormatLiteral(member.ValueName, true)}");
        }

        if (member.ValueRangeMinimum is not null && member.ValueRangeMaximum is not null)
        {
            initializers.Add($"NumArgs = new global::Kawayi.CommandLine.Abstractions.ValueRange({member.ValueRangeMinimum}, {member.ValueRangeMaximum})");
        }

        if (member.PossibleValuesExpression is not null)
        {
            initializers.Add($"PossibleValues = {member.PossibleValuesExpression}");
        }

        AddValidationInitializer(initializers, member);

        return ApplyInitializers(propertyExpression, initializers);
    }

    private static void AddValidationInitializer(List<string> initializers, MemberExport member)
    {
        if (member.Validators.IsDefaultOrEmpty)
        {
            return;
        }

        initializers.Add($"Validation = {GenerateValidationExpression(member)}");
    }

    private static string ApplyInitializers(string expression, List<string> initializers)
    {
        return initializers.Count == 0
            ? expression
            : $"{expression} {{ {string.Join(", ", initializers)} }}";
    }

    private static string GenerateValidationExpression(MemberExport member)
    {
        if (member.Validators.Length == 1)
        {
            return $"static value => {member.Validators[0].MethodName}(({member.TypeName})value)";
        }

        var builder = new StringBuilder();
        builder.Append("static value => { ");
        for (var i = 0; i < member.Validators.Length; i++)
        {
            builder.Append("var result")
                .Append(i)
                .Append(" = ")
                .Append(member.Validators[i].MethodName)
                .Append("((")
                .Append(member.TypeName)
                .Append(")value); if (result")
                .Append(i)
                .Append(" is not null) { return result")
                .Append(i)
                .Append("; } ");
        }

        builder.Append("return null; }");
        return builder.ToString();
    }

    private static string GenerateSubcommandExpression(MemberExport member)
    {
        return
            $"new global::Kawayi.CommandLine.Abstractions.CommandDefinition({GenerateDefinitionInformationExpression(member)}, {GenerateAliasDictionaryExpression(member.LongAliases)}, null)";
    }

    private static string GenerateDefinitionInformationExpression(MemberExport member)
    {
        return
            $"new global::Kawayi.CommandLine.Abstractions.DefinitionInformation(new global::Kawayi.CommandLine.Abstractions.NameWithVisibility({GenerateCommandLineNameExpression(member.MemberName)}, {FormatBool(member.Visible)}), Documents[{SymbolDisplay.FormatLiteral(member.MemberName, true)}])";
    }

    private static string GenerateCommandLineNameExpression(string memberName)
    {
        return $"global::Kawayi.CommandLine.Abstractions.CaseConverter.Pascal2Kebab({SymbolDisplay.FormatLiteral(memberName, true)})";
    }

    private static string GenerateAliasDictionaryExpression(ImmutableArray<AliasEntry> aliases)
    {
        if (aliases.IsDefaultOrEmpty)
        {
            return "global::System.Collections.Immutable.ImmutableDictionary<string, global::Kawayi.CommandLine.Abstractions.NameWithVisibility>.Empty";
        }

        var builder = new StringBuilder();
        builder.Append("global::System.Collections.Immutable.ImmutableDictionary.CreateRange<string, global::Kawayi.CommandLine.Abstractions.NameWithVisibility>(global::System.StringComparer.Ordinal, new global::System.Collections.Generic.KeyValuePair<string, global::Kawayi.CommandLine.Abstractions.NameWithVisibility>[] { ");

        for (var i = 0; i < aliases.Length; i++)
        {
            if (i > 0)
            {
                builder.Append(", ");
            }

            builder.Append("new(")
                .Append(SymbolDisplay.FormatLiteral(aliases[i].Name, true))
                .Append(", new global::Kawayi.CommandLine.Abstractions.NameWithVisibility(")
                .Append(SymbolDisplay.FormatLiteral(aliases[i].Name, true))
                .Append(", ")
                .Append(FormatBool(aliases[i].Visible))
                .Append("))");
        }

        builder.Append(" })");
        return builder.ToString();
    }

    private static string FormatBool(bool value) => value ? "true" : "false";

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
            builder.Append(" : global::Kawayi.CommandLine.Abstractions.ISymbolExporter");
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
        return $"{sanitizedName}.ExportSymbols.g.cs";
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
            bool hasDocumentExporter,
            ImmutableArray<MemberExport> members,
            ImmutableArray<DiagnosticInfo> diagnostics)
        {
            TypeSymbol = typeSymbol;
            MissingPartialSymbol = missingPartialSymbol;
            HasDocumentExporter = hasDocumentExporter;
            Members = members;
            Diagnostics = diagnostics;
        }

        public INamedTypeSymbol TypeSymbol { get; }

        public INamedTypeSymbol? MissingPartialSymbol { get; }

        public bool HasDocumentExporter { get; }

        public ImmutableArray<MemberExport> Members { get; }

        public ImmutableArray<DiagnosticInfo> Diagnostics { get; }
    }

    private sealed class DiagnosticInfo
    {
        public DiagnosticInfo(DiagnosticDescriptor descriptor, Location? location, object?[] args)
        {
            Descriptor = descriptor;
            Location = location;
            Args = args;
        }

        public DiagnosticDescriptor Descriptor { get; }

        public Location? Location { get; }

        public object?[] Args { get; }
    }

    private sealed class MemberExport
    {
        public MemberExport(MemberKind kind,
                            string memberName,
                            string typeName,
                            bool visibleRequirement,
                            bool visible,
                            ImmutableArray<AliasEntry> longAliases,
                            ImmutableArray<AliasEntry> shortAliases,
                            string? valueName,
                            int? valueRangeMinimum,
                            int? valueRangeMaximum,
                            Location? location,
                            int declarationOrder,
                            int? argumentPosition = null,
                            ImmutableArray<string>? conflictKeys = null,
                            string? possibleValuesExpression = null,
                            ImmutableArray<ValidatorExport>? validators = null)
        {
            Kind = kind;
            MemberName = memberName;
            TypeName = typeName;
            VisibleRequirement = visibleRequirement;
            Visible = visible;
            LongAliases = longAliases;
            ShortAliases = shortAliases;
            ValueName = valueName;
            ValueRangeMinimum = valueRangeMinimum;
            ValueRangeMaximum = valueRangeMaximum;
            Location = location;
            DeclarationOrder = declarationOrder;
            ArgumentPosition = argumentPosition;
            ConflictKeys = conflictKeys ?? [];
            PossibleValuesExpression = possibleValuesExpression;
            Validators = validators ?? [];
        }

        public MemberKind Kind { get; }

        public string MemberName { get; }

        public string TypeName { get; }

        public bool VisibleRequirement { get; }

        public bool Visible { get; }

        public ImmutableArray<AliasEntry> LongAliases { get; }

        public ImmutableArray<AliasEntry> ShortAliases { get; }

        public string? ValueName { get; }

        public int? ValueRangeMinimum { get; }

        public int? ValueRangeMaximum { get; }

        public Location? Location { get; }

        public int DeclarationOrder { get; }

        public int? ArgumentPosition { get; }

        public ImmutableArray<string> ConflictKeys { get; }

        public string? PossibleValuesExpression { get; }

        public ImmutableArray<ValidatorExport> Validators { get; }
    }

    private sealed class ValidatorExport
    {
        public ValidatorExport(string methodName)
        {
            MethodName = methodName;
        }

        public string MethodName { get; }
    }

    private sealed class AliasEntry
    {
        public AliasEntry(string name, bool visible)
        {
            Name = name;
            Visible = visible;
        }

        public string Name { get; }

        public bool Visible { get; }
    }

    private enum MemberKind
    {
        Argument,
        Property,
        Subcommand
    }
}
