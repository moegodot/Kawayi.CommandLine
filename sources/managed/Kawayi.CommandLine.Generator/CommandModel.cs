// Copyright (c) 2026 MoeGodot<me@kawayi.moe>.
// Licensed under the GNU Affero General Public License v3-or-later license.

using System.Collections.Immutable;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Kawayi.CommandLine.Generator;

internal static class MetadataNames
{
    public const string ExportDocumentAttribute = "Kawayi.CommandLine.Core.Attributes.ExportDocumentAttribute";
    public const string ExportSymbolsAttribute = "Kawayi.CommandLine.Core.Attributes.ExportSymbolsAttribute";
    public const string ExportParsingAttribute = "Kawayi.CommandLine.Core.Attributes.ExportParsingAttribute";
    public const string BindableAttribute = "Kawayi.CommandLine.Core.Attributes.BindableAttribute";
    public const string CommandAttribute = "Kawayi.CommandLine.Core.Attributes.CommandAttribute";
    public const string ArgumentAttribute = "Kawayi.CommandLine.Core.Attributes.ArgumentAttribute";
    public const string PropertyAttribute = "Kawayi.CommandLine.Core.Attributes.PropertyAttribute";
    public const string SubcommandAttribute = "Kawayi.CommandLine.Core.Attributes.SubcommandAttribute";
    public const string ValueRangeAttribute = "Kawayi.CommandLine.Core.Attributes.ValueRangeAttribute";
    public const string LongAliasAttribute = "Kawayi.CommandLine.Core.Attributes.LongAliasAttribute";
    public const string ShortAliasAttribute = "Kawayi.CommandLine.Core.Attributes.ShortAliasAttribute";
    public const string AliasAttribute = "Kawayi.CommandLine.Core.Attributes.AliasAttribute";
    public const string ValidatorAttribute = "Kawayi.CommandLine.Core.Attributes.ValidatorAttribute";

    public const string DocumentExporter = "Kawayi.CommandLine.Abstractions.IDocumentExporter";
    public const string SymbolExporter = "Kawayi.CommandLine.Abstractions.ISymbolExporter";
    public const string CliSchemaExporter = "Kawayi.CommandLine.Abstractions.ICliSchemaExporter";
    public const string Bindable = "Kawayi.CommandLine.Abstractions.IBindable";
    public const string Parsable = "Kawayi.CommandLine.Abstractions.IParsable";
}

internal static class GeneratorFormats
{
    public static readonly SymbolDisplayFormat FullyQualifiedType = new(
        globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Included,
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        miscellaneousOptions:
        SymbolDisplayMiscellaneousOptions.UseSpecialTypes |
        SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers);

    public static readonly SymbolDisplayFormat FullyQualifiedNullableType = new(
        globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Included,
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        miscellaneousOptions:
        SymbolDisplayMiscellaneousOptions.UseSpecialTypes |
        SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers |
        SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);
}

internal sealed class CommandModel
{
    private CommandModel(
        INamedTypeSymbol typeSymbol,
        INamedTypeSymbol? missingPartialSymbol,
        bool hasCommandAttribute,
        bool hasExportDocumentAttribute,
        bool hasExportSymbolsAttribute,
        bool hasExportParsingAttribute,
        bool hasBindableAttribute,
        bool implementsDocumentExporter,
        bool implementsSymbolExporter,
        bool implementsCliSchemaExporter,
        bool implementsBindable,
        bool hasParsableSelfInterface,
        ImmutableArray<MemberModel> members,
        ImmutableArray<DiagnosticInfo> symbolDiagnostics)
    {
        TypeSymbol = typeSymbol;
        MissingPartialSymbol = missingPartialSymbol;
        HasCommandAttribute = hasCommandAttribute;
        HasExportDocumentAttribute = hasExportDocumentAttribute;
        HasExportSymbolsAttribute = hasExportSymbolsAttribute;
        HasExportParsingAttribute = hasExportParsingAttribute;
        HasBindableAttribute = hasBindableAttribute;
        ImplementsDocumentExporter = implementsDocumentExporter;
        ImplementsSymbolExporter = implementsSymbolExporter;
        ImplementsCliSchemaExporter = implementsCliSchemaExporter;
        ImplementsBindable = implementsBindable;
        HasParsableSelfInterface = hasParsableSelfInterface;
        Members = members;
        SymbolDiagnostics = symbolDiagnostics;
    }

    public INamedTypeSymbol TypeSymbol { get; }

    public INamedTypeSymbol? MissingPartialSymbol { get; }

    public bool HasCommandAttribute { get; }

    public bool HasExportDocumentAttribute { get; }

    public bool HasExportSymbolsAttribute { get; }

    public bool HasExportParsingAttribute { get; }

    public bool HasBindableAttribute { get; }

    public bool ImplementsDocumentExporter { get; }

    public bool ImplementsSymbolExporter { get; }

    public bool ImplementsCliSchemaExporter { get; }

    public bool ImplementsBindable { get; }

    public bool HasParsableSelfInterface { get; }

    public ImmutableArray<MemberModel> Members { get; }

    public ImmutableArray<DiagnosticInfo> SymbolDiagnostics { get; }

    public bool HasDocumentProvider =>
        ImplementsDocumentExporter || HasExportDocumentAttribute || HasCommandAttribute;

    public bool HasSymbolProvider =>
        ImplementsSymbolExporter || HasExportSymbolsAttribute || HasCommandAttribute;

    public bool CanGenerateSymbols =>
        MissingPartialSymbol is null &&
        HasDocumentProvider &&
        !SymbolDiagnostics.Any(static diagnostic => diagnostic.Descriptor.DefaultSeverity == DiagnosticSeverity.Error);

    public static CommandModel Create(INamedTypeSymbol typeSymbol, CancellationToken cancellationToken)
    {
        var diagnostics = ImmutableArray.CreateBuilder<DiagnosticInfo>();
        var members = ImmutableArray.CreateBuilder<MemberModel>();
        var argumentPositions = new Dictionary<int, MemberModel>();
        var propertyNames = new Dictionary<string, MemberModel>(StringComparer.Ordinal);
        var longAliases = new Dictionary<string, MemberModel>(StringComparer.Ordinal);
        var shortAliases = new Dictionary<string, MemberModel>(StringComparer.Ordinal);
        var subcommandNames = new Dictionary<string, MemberModel>(StringComparer.Ordinal);
        var declarationOrder = 0;

        foreach (var propertySymbol in typeSymbol.GetMembers().OfType<IPropertySymbol>())
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (propertySymbol.IsImplicitlyDeclared || propertySymbol.IsStatic || propertySymbol.IsIndexer)
            {
                continue;
            }

            var argumentAttribute = GetAttribute(propertySymbol, MetadataNames.ArgumentAttribute);
            var propertyAttribute = GetAttribute(propertySymbol, MetadataNames.PropertyAttribute);
            var subcommandAttribute = GetAttribute(propertySymbol, MetadataNames.SubcommandAttribute);
            var longAliasAttributes = GetAttributes(propertySymbol, MetadataNames.LongAliasAttribute);
            var shortAliasAttributes = GetAttributes(propertySymbol, MetadataNames.ShortAliasAttribute);
            var aliasAttributes = GetAttributes(propertySymbol, MetadataNames.AliasAttribute);
            var validatorAttributes = GetAttributes(propertySymbol, MetadataNames.ValidatorAttribute);
            var roleCount = (argumentAttribute is null ? 0 : 1) +
                            (propertyAttribute is null ? 0 : 1) +
                            (subcommandAttribute is null ? 0 : 1);

            if (roleCount == 0)
            {
                ReportInvalidAliasesIfNeeded(diagnostics, propertySymbol, longAliasAttributes, shortAliasAttributes, aliasAttributes);
                ReportInvalidValidatorTargetIfNeeded(diagnostics, propertySymbol, validatorAttributes);
                continue;
            }

            if (roleCount > 1)
            {
                diagnostics.Add(new DiagnosticInfo(GeneratorDescriptors.MultipleRole, propertySymbol.Locations.FirstOrDefault(), [propertySymbol.Name]));
                continue;
            }

            var (summary, remarks) = DocumentationHelpers.ExtractDocumentation(propertySymbol, cancellationToken);
            var commandLineName = GenerateCommandLineNameExpression(propertySymbol.Name);
            var commandLineKey = ConvertPascalToKebab(propertySymbol.Name);

            if (argumentAttribute is not null)
            {
                if (!longAliasAttributes.IsDefaultOrEmpty || !shortAliasAttributes.IsDefaultOrEmpty)
                {
                    diagnostics.Add(new DiagnosticInfo(GeneratorDescriptors.InvalidPropertyAlias, propertySymbol.Locations.FirstOrDefault(), [propertySymbol.Name]));
                }

                if (!aliasAttributes.IsDefaultOrEmpty)
                {
                    diagnostics.Add(new DiagnosticInfo(GeneratorDescriptors.InvalidSubcommandAlias, propertySymbol.Locations.FirstOrDefault(), [propertySymbol.Name]));
                }

                var valueRangeAttribute = GetAttribute(propertySymbol, MetadataNames.ValueRangeAttribute);
                if (valueRangeAttribute is null)
                {
                    diagnostics.Add(new DiagnosticInfo(GeneratorDescriptors.MissingValueRange, propertySymbol.Locations.FirstOrDefault(), [propertySymbol.Name]));
                    continue;
                }

                ReportNonNullableRequirementIfNullIfNeeded(diagnostics, propertySymbol, argumentAttribute, 3);

                var member = new MemberModel(
                    MemberKind.Argument,
                    propertySymbol,
                    propertySymbol.Name,
                    commandLineName,
                    commandLineKey,
                    propertySymbol.Type.ToDisplayString(GeneratorFormats.FullyQualifiedType),
                    propertySymbol.Type.ToDisplayString(GeneratorFormats.FullyQualifiedNullableType),
                    declarationOrder++,
                    summary,
                    remarks,
                    visibleRequirement: GetAttributeBool(argumentAttribute, 1, false),
                    visible: GetAttributeBool(argumentAttribute, 2, true),
                    requirementIfNull: GetAttributeBool(argumentAttribute, 3, false),
                    argumentPosition: GetAttributeInt(argumentAttribute, 0, 0),
                    valueRangeMinimum: GetAttributeInt(valueRangeAttribute, 0, 0),
                    valueRangeMaximum: GetAttributeInt(valueRangeAttribute, 1, 0),
                    valueName: null,
                    longAliases: [],
                    shortAliases: [],
                    aliases: [],
                    validators: ResolveValidators(typeSymbol, propertySymbol, validatorAttributes, diagnostics),
                    enumPossibleValuesExpression: null,
                    isGlobalSubcommand: false);

                members.Add(member);
                if (argumentPositions.TryGetValue(member.ArgumentPosition!.Value, out var existingArgument))
                {
                    diagnostics.Add(new DiagnosticInfo(GeneratorDescriptors.DuplicateArgumentPosition,
                                                       propertySymbol.Locations.FirstOrDefault(),
                                                       [member.ArgumentPosition.Value, existingArgument.MemberName]));
                }
                else
                {
                    argumentPositions[member.ArgumentPosition.Value] = member;
                }

                continue;
            }

            if (propertyAttribute is not null)
            {
                if (!aliasAttributes.IsDefaultOrEmpty)
                {
                    diagnostics.Add(new DiagnosticInfo(GeneratorDescriptors.InvalidSubcommandAlias, propertySymbol.Locations.FirstOrDefault(), [propertySymbol.Name]));
                }

                ReportNonNullableRequirementIfNullIfNeeded(diagnostics, propertySymbol, propertyAttribute, 3);

                var valueRangeAttribute = GetAttribute(propertySymbol, MetadataNames.ValueRangeAttribute);
                var modelLongAliases = CreateAliasEntries(longAliasAttributes);
                var modelShortAliases = CreateAliasEntries(shortAliasAttributes);
                var enumPossibleValuesExpression = propertySymbol.Type.TypeKind == TypeKind.Enum
                    ? $"new global::Kawayi.CommandLine.Abstractions.CountablePossibleValues<string>(global::System.Collections.Immutable.ImmutableArray.CreateRange<string>(global::System.Enum.GetNames(typeof({propertySymbol.Type.ToDisplayString(GeneratorFormats.FullyQualifiedType)}))))"
                    : null;

                var member = new MemberModel(
                    MemberKind.Property,
                    propertySymbol,
                    propertySymbol.Name,
                    commandLineName,
                    commandLineKey,
                    propertySymbol.Type.ToDisplayString(GeneratorFormats.FullyQualifiedType),
                    propertySymbol.Type.ToDisplayString(GeneratorFormats.FullyQualifiedNullableType),
                    declarationOrder++,
                    summary,
                    remarks,
                    visibleRequirement: GetAttributeBool(propertyAttribute, 0, false),
                    visible: GetAttributeBool(propertyAttribute, 1, true),
                    requirementIfNull: GetAttributeBool(propertyAttribute, 3, false),
                    argumentPosition: null,
                    valueRangeMinimum: valueRangeAttribute is null ? null : GetAttributeInt(valueRangeAttribute, 0, 0),
                    valueRangeMaximum: valueRangeAttribute is null ? null : GetAttributeInt(valueRangeAttribute, 1, 0),
                    valueName: GetAttributeNullableString(propertyAttribute, 2),
                    longAliases: modelLongAliases,
                    shortAliases: modelShortAliases,
                    aliases: [],
                    validators: ResolveValidators(typeSymbol, propertySymbol, validatorAttributes, diagnostics),
                    enumPossibleValuesExpression: enumPossibleValuesExpression,
                    isGlobalSubcommand: false);

                members.Add(member);
                RegisterConflict(diagnostics, propertyNames, member.CommandLineKey, member);
                RegisterCrossConflict(diagnostics, longAliases, member.CommandLineKey, member);
                RegisterCrossConflicts(diagnostics, propertyNames, member.LongAliases, member);
                RegisterConflicts(diagnostics, longAliases, member.LongAliases, member);
                RegisterConflicts(diagnostics, shortAliases, member.ShortAliases, member);
                continue;
            }

            ReportInvalidValidatorTargetIfNeeded(diagnostics, propertySymbol, validatorAttributes);

            if (!longAliasAttributes.IsDefaultOrEmpty || !shortAliasAttributes.IsDefaultOrEmpty)
            {
                diagnostics.Add(new DiagnosticInfo(GeneratorDescriptors.InvalidPropertyAlias, propertySymbol.Locations.FirstOrDefault(), [propertySymbol.Name]));
            }

            var required = GetAttributeBool(subcommandAttribute!, 0, false);
            if (required)
            {
                diagnostics.Add(new DiagnosticInfo(GeneratorDescriptors.RequiredSubcommandUnsupported, propertySymbol.Locations.FirstOrDefault(), [propertySymbol.Name]));
            }

            ReportNonNullableSubcommandIfNeeded(diagnostics, propertySymbol, subcommandAttribute!);

            var aliases = CreateAliasEntries(aliasAttributes);
            var subcommand = new MemberModel(
                MemberKind.Subcommand,
                propertySymbol,
                propertySymbol.Name,
                commandLineName,
                commandLineKey,
                propertySymbol.Type.ToDisplayString(GeneratorFormats.FullyQualifiedType),
                propertySymbol.Type.ToDisplayString(GeneratorFormats.FullyQualifiedNullableType),
                declarationOrder++,
                summary,
                remarks,
                visibleRequirement: false,
                visible: GetAttributeBool(subcommandAttribute!, 1, true),
                requirementIfNull: false,
                argumentPosition: null,
                valueRangeMinimum: null,
                valueRangeMaximum: null,
                valueName: null,
                longAliases: [],
                shortAliases: [],
                aliases: aliases,
                validators: [],
                enumPossibleValuesExpression: null,
                isGlobalSubcommand: GetAttributeBool(subcommandAttribute!, 2, false));

            members.Add(subcommand);
            RegisterConflict(diagnostics, subcommandNames, subcommand.CommandLineKey, subcommand);
            RegisterConflicts(diagnostics, subcommandNames, subcommand.Aliases, subcommand);
        }

        return new CommandModel(
            typeSymbol,
            FindFirstMissingPartialType(typeSymbol),
            HasAttribute(typeSymbol, MetadataNames.CommandAttribute),
            HasAttribute(typeSymbol, MetadataNames.ExportDocumentAttribute),
            HasAttribute(typeSymbol, MetadataNames.ExportSymbolsAttribute),
            HasAttribute(typeSymbol, MetadataNames.ExportParsingAttribute),
            HasAttribute(typeSymbol, MetadataNames.BindableAttribute),
            ImplementsInterface(typeSymbol, MetadataNames.DocumentExporter),
            ImplementsInterface(typeSymbol, MetadataNames.SymbolExporter),
            ImplementsInterface(typeSymbol, MetadataNames.CliSchemaExporter),
            ImplementsInterface(typeSymbol, MetadataNames.Bindable),
            HasParsableSelfInterfaceImpl(typeSymbol),
            members.ToImmutable(),
            diagnostics.ToImmutable());
    }

    public static string GenerateCommandLineNameExpression(string memberName) =>
        $"global::Kawayi.CommandLine.Abstractions.CaseConverter.Pascal2Kebab({SymbolDisplay.FormatLiteral(memberName, true)})";

    private static string ConvertPascalToKebab(string pascal)
    {
        if (pascal.Length == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder(pascal.Length + 8);
        var previousKind = CharacterKind.None;

        for (var i = 0; i < pascal.Length; i++)
        {
            var current = pascal[i];
            if (current is '-' or '_' || char.IsWhiteSpace(current))
            {
                AppendSeparator(builder);
                previousKind = CharacterKind.Separator;
                continue;
            }

            var currentKind = GetCharacterKind(current);
            var nextKind = i + 1 < pascal.Length && pascal[i + 1] is not '-' and not '_' && !char.IsWhiteSpace(pascal[i + 1])
                ? GetCharacterKind(pascal[i + 1])
                : CharacterKind.None;

            if (ShouldAppendSeparator(previousKind, currentKind, nextKind))
            {
                AppendSeparator(builder);
            }

            builder.Append(char.ToLowerInvariant(current));
            previousKind = currentKind;
        }

        if (builder.Length > 0 && builder[builder.Length - 1] == '-')
        {
            builder.Length--;
        }

        return builder.ToString();
    }

    private static bool ShouldAppendSeparator(CharacterKind previousKind, CharacterKind currentKind, CharacterKind nextKind)
    {
        return previousKind switch
        {
            CharacterKind.None or CharacterKind.Separator => false,
            CharacterKind.Lower => currentKind is CharacterKind.Upper or CharacterKind.Digit,
            CharacterKind.Upper => currentKind is CharacterKind.Digit ||
                                   currentKind == CharacterKind.Upper && nextKind == CharacterKind.Lower,
            CharacterKind.Digit => currentKind is CharacterKind.Lower or CharacterKind.Upper,
            _ => false,
        };
    }

    private static void AppendSeparator(StringBuilder builder)
    {
        if (builder.Length > 0 && builder[builder.Length - 1] != '-')
        {
            builder.Append('-');
        }
    }

    private static CharacterKind GetCharacterKind(char value)
    {
        if (char.IsUpper(value))
        {
            return CharacterKind.Upper;
        }

        if (char.IsLower(value))
        {
            return CharacterKind.Lower;
        }

        return char.IsDigit(value)
            ? CharacterKind.Digit
            : CharacterKind.Lower;
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
            diagnostics.Add(new DiagnosticInfo(GeneratorDescriptors.InvalidPropertyAlias, propertySymbol.Locations.FirstOrDefault(), [propertySymbol.Name]));
        }

        if (!subcommandAliasAttributes.IsDefaultOrEmpty)
        {
            diagnostics.Add(new DiagnosticInfo(GeneratorDescriptors.InvalidSubcommandAlias, propertySymbol.Locations.FirstOrDefault(), [propertySymbol.Name]));
        }
    }

    private static void ReportInvalidValidatorTargetIfNeeded(
        ImmutableArray<DiagnosticInfo>.Builder diagnostics,
        IPropertySymbol propertySymbol,
        ImmutableArray<AttributeData> validatorAttributes)
    {
        if (!validatorAttributes.IsDefaultOrEmpty)
        {
            diagnostics.Add(new DiagnosticInfo(GeneratorDescriptors.InvalidValidatorTarget, propertySymbol.Locations.FirstOrDefault(), [propertySymbol.Name]));
        }
    }

    private static void ReportNonNullableSubcommandIfNeeded(
        ImmutableArray<DiagnosticInfo>.Builder diagnostics,
        IPropertySymbol propertySymbol,
        AttributeData subcommandAttribute)
    {
        if (GetAttributeBool(subcommandAttribute, 2, false))
        {
            return;
        }

        if (propertySymbol.Type.IsReferenceType &&
            propertySymbol.Type.NullableAnnotation == NullableAnnotation.NotAnnotated)
        {
            diagnostics.Add(new DiagnosticInfo(GeneratorDescriptors.NonNullableSubcommand, propertySymbol.Locations.FirstOrDefault(), [propertySymbol.Name]));
        }
    }

    private static void ReportNonNullableRequirementIfNullIfNeeded(
        ImmutableArray<DiagnosticInfo>.Builder diagnostics,
        IPropertySymbol propertySymbol,
        AttributeData attribute,
        int requirementIfNullIndex)
    {
        if (!GetAttributeBool(attribute, requirementIfNullIndex, false) ||
            IsNullableType(propertySymbol.Type))
        {
            return;
        }

        diagnostics.Add(new DiagnosticInfo(GeneratorDescriptors.NonNullableRequirementIfNull, propertySymbol.Locations.FirstOrDefault(), [propertySymbol.Name]));
    }

    private static bool IsNullableType(ITypeSymbol typeSymbol)
    {
        if (typeSymbol.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T)
        {
            return true;
        }

        if (typeSymbol.IsValueType)
        {
            return false;
        }

        return typeSymbol.NullableAnnotation != NullableAnnotation.NotAnnotated;
    }

    private static ImmutableArray<ValidatorModel> ResolveValidators(
        INamedTypeSymbol typeSymbol,
        IPropertySymbol propertySymbol,
        ImmutableArray<AttributeData> validatorAttributes,
        ImmutableArray<DiagnosticInfo>.Builder diagnostics)
    {
        if (validatorAttributes.IsDefaultOrEmpty)
        {
            return [];
        }

        var validators = ImmutableArray.CreateBuilder<ValidatorModel>(validatorAttributes.Length);
        foreach (var validatorAttribute in validatorAttributes)
        {
            var validatorName = GetAttributeString(validatorAttribute, 0, string.Empty);
            var matches = typeSymbol.GetMembers(validatorName)
                .OfType<IMethodSymbol>()
                .Where(method => IsMatchingValidator(method, propertySymbol.Type))
                .ToArray();

            if (matches.Length != 1)
            {
                diagnostics.Add(new DiagnosticInfo(GeneratorDescriptors.InvalidValidatorMethod,
                                                   propertySymbol.Locations.FirstOrDefault(),
                                                   [validatorName, propertySymbol.Name, propertySymbol.Type.ToDisplayString(GeneratorFormats.FullyQualifiedType)]));
                continue;
            }

            validators.Add(new ValidatorModel(GeneratorSource.EscapeIdentifier(matches[0].Name)));
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

    private static ImmutableArray<AliasModel> CreateAliasEntries(ImmutableArray<AttributeData> attributes)
    {
        if (attributes.IsDefaultOrEmpty)
        {
            return [];
        }

        return [.. attributes.Select(static attribute =>
                new AliasModel(GetAttributeString(attribute, 0, string.Empty),
                               GetAttributeBool(attribute, 1, true)))
            .OrderBy(static alias => alias.Name, StringComparer.Ordinal)];
    }

    private static void RegisterConflicts(
        ImmutableArray<DiagnosticInfo>.Builder diagnostics,
        Dictionary<string, MemberModel> registry,
        ImmutableArray<AliasModel> aliases,
        MemberModel member)
    {
        foreach (var alias in aliases)
        {
            RegisterConflict(diagnostics, registry, alias.Name, member);
        }
    }

    private static void RegisterConflict(
        ImmutableArray<DiagnosticInfo>.Builder diagnostics,
        Dictionary<string, MemberModel> registry,
        string key,
        MemberModel member)
    {
        if (registry.TryGetValue(key, out var existing))
        {
            diagnostics.Add(new DiagnosticInfo(GeneratorDescriptors.AliasConflict, member.Location, [key, existing.MemberName, member.MemberName]));
            return;
        }

        registry[key] = member;
    }

    private static void RegisterCrossConflicts(
        ImmutableArray<DiagnosticInfo>.Builder diagnostics,
        Dictionary<string, MemberModel> registry,
        ImmutableArray<AliasModel> aliases,
        MemberModel member)
    {
        foreach (var alias in aliases)
        {
            RegisterCrossConflict(diagnostics, registry, alias.Name, member);
        }
    }

    private static void RegisterCrossConflict(
        ImmutableArray<DiagnosticInfo>.Builder diagnostics,
        Dictionary<string, MemberModel> registry,
        string key,
        MemberModel member)
    {
        if (!registry.TryGetValue(key, out var existing) || ReferenceEquals(existing, member))
        {
            return;
        }

        diagnostics.Add(new DiagnosticInfo(GeneratorDescriptors.AliasConflict, member.Location, [key, existing.MemberName, member.MemberName]));
    }

    public static AttributeData? GetAttribute(ISymbol symbol, string metadataName)
    {
        return symbol.GetAttributes().FirstOrDefault(attribute =>
            attribute.AttributeClass?.ToDisplayString() == metadataName);
    }

    public static ImmutableArray<AttributeData> GetAttributes(ISymbol symbol, string metadataName)
    {
        return [.. symbol.GetAttributes().Where(attribute =>
            attribute.AttributeClass?.ToDisplayString() == metadataName)];
    }

    public static bool HasAttribute(ISymbol symbol, string metadataName)
    {
        return GetAttribute(symbol, metadataName) is not null;
    }

    public static bool ImplementsInterface(ITypeSymbol typeSymbol, string metadataName)
    {
        return typeSymbol is INamedTypeSymbol namedType &&
               namedType.AllInterfaces.Any(item => string.Equals(item.ToDisplayString(), metadataName, StringComparison.Ordinal));
    }

    public static bool GetAttributeBool(AttributeData attribute, int index, bool defaultValue)
    {
        if (attribute.ConstructorArguments.Length <= index)
        {
            return defaultValue;
        }

        return attribute.ConstructorArguments[index].Value is bool value ? value : defaultValue;
    }

    public static int GetAttributeInt(AttributeData attribute, int index, int defaultValue)
    {
        if (attribute.ConstructorArguments.Length <= index)
        {
            return defaultValue;
        }

        return attribute.ConstructorArguments[index].Value is int value ? value : defaultValue;
    }

    public static string GetAttributeString(AttributeData attribute, int index, string defaultValue)
    {
        if (attribute.ConstructorArguments.Length <= index)
        {
            return defaultValue;
        }

        return attribute.ConstructorArguments[index].Value as string ?? defaultValue;
    }

    public static string? GetAttributeNullableString(AttributeData attribute, int index)
    {
        if (attribute.ConstructorArguments.Length <= index)
        {
            return null;
        }

        return attribute.ConstructorArguments[index].Value as string;
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

    private static bool HasParsableSelfInterfaceImpl(INamedTypeSymbol typeSymbol)
    {
        foreach (var implementedInterface in typeSymbol.AllInterfaces)
        {
            if (!string.Equals(implementedInterface.OriginalDefinition.ToDisplayString(),
                               MetadataNames.Parsable + "<T>",
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
}

internal sealed class MemberModel
{
    public MemberModel(
        MemberKind kind,
        IPropertySymbol propertySymbol,
        string memberName,
        string commandLineName,
        string commandLineKey,
        string typeName,
        string nullableTypeName,
        int declarationOrder,
        string summary,
        string remarks,
        bool visibleRequirement,
        bool visible,
        bool requirementIfNull,
        int? argumentPosition,
        int? valueRangeMinimum,
        int? valueRangeMaximum,
        string? valueName,
        ImmutableArray<AliasModel> longAliases,
        ImmutableArray<AliasModel> shortAliases,
        ImmutableArray<AliasModel> aliases,
        ImmutableArray<ValidatorModel> validators,
        string? enumPossibleValuesExpression,
        bool isGlobalSubcommand)
    {
        Kind = kind;
        PropertySymbol = propertySymbol;
        MemberName = memberName;
        CommandLineName = commandLineName;
        CommandLineKey = commandLineKey;
        TypeName = typeName;
        NullableTypeName = nullableTypeName;
        DeclarationOrder = declarationOrder;
        Summary = summary;
        Remarks = remarks;
        VisibleRequirement = visibleRequirement;
        Visible = visible;
        RequirementIfNull = requirementIfNull;
        ArgumentPosition = argumentPosition;
        ValueRangeMinimum = valueRangeMinimum;
        ValueRangeMaximum = valueRangeMaximum;
        ValueName = valueName;
        LongAliases = longAliases;
        ShortAliases = shortAliases;
        Aliases = aliases;
        Validators = validators;
        EnumPossibleValuesExpression = enumPossibleValuesExpression;
        IsGlobalSubcommand = isGlobalSubcommand;
    }

    public MemberKind Kind { get; }

    public IPropertySymbol PropertySymbol { get; }

    public string MemberName { get; }

    public string CommandLineName { get; }

    public string CommandLineKey { get; }

    public string TypeName { get; }

    public string NullableTypeName { get; }

    public int DeclarationOrder { get; }

    public string Summary { get; }

    public string Remarks { get; }

    public bool VisibleRequirement { get; }

    public bool Visible { get; }

    public bool RequirementIfNull { get; }

    public int? ArgumentPosition { get; }

    public int? ValueRangeMinimum { get; }

    public int? ValueRangeMaximum { get; }

    public string? ValueName { get; }

    public ImmutableArray<AliasModel> LongAliases { get; }

    public ImmutableArray<AliasModel> ShortAliases { get; }

    public ImmutableArray<AliasModel> Aliases { get; }

    public ImmutableArray<ValidatorModel> Validators { get; }

    public string? EnumPossibleValuesExpression { get; }

    public bool IsGlobalSubcommand { get; }

    public Location? Location => PropertySymbol.Locations.FirstOrDefault();
}

internal sealed class AliasModel
{
    public AliasModel(string name, bool visible)
    {
        Name = name;
        Visible = visible;
    }

    public string Name { get; }

    public bool Visible { get; }
}

internal sealed class ValidatorModel
{
    public ValidatorModel(string methodName)
    {
        MethodName = methodName;
    }

    public string MethodName { get; }
}

internal sealed class DiagnosticInfo
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

internal enum MemberKind
{
    Argument,
    Property,
    Subcommand
}

internal enum CharacterKind
{
    None,
    Separator,
    Lower,
    Upper,
    Digit
}

internal static class DocumentationHelpers
{
    private static readonly Regex WhitespaceRegex = new(@"\s+", RegexOptions.Compiled);

    public static (string Summary, string Remarks) ExtractDocumentation(
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
}

internal static class GeneratorSource
{
    public static ImmutableArray<INamedTypeSymbol> GetContainingTypes(INamedTypeSymbol typeSymbol)
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

    public static string BuildTypeDeclaration(
        INamedTypeSymbol typeSymbol,
        string? interfaceList,
        bool includeInterface)
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

        if (includeInterface && !string.IsNullOrEmpty(interfaceList))
        {
            builder.Append(" : ");
            builder.Append(interfaceList);
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

    public static string BuildSelfTypeReference(INamedTypeSymbol typeSymbol)
    {
        if (typeSymbol.TypeParameters.Length == 0)
        {
            return typeSymbol.Name;
        }

        return $"{typeSymbol.Name}<{string.Join(", ", typeSymbol.TypeParameters.Select(static parameter => parameter.Name))}>";
    }

    public static string GetHintName(INamedTypeSymbol typeSymbol, string suffix)
    {
        var metadataName = typeSymbol.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat);
        var sanitizedName = new string(metadataName.Select(static character =>
            char.IsLetterOrDigit(character) ? character : '_').ToArray());
        return $"{sanitizedName}.{suffix}.g.cs";
    }

    public static void AppendIndentedLine(StringBuilder builder, int indentLevel, string text)
    {
        builder.Append(' ', indentLevel * 4);
        builder.AppendLine(text);
    }

    public static string EscapeIdentifier(string identifier)
    {
        return SyntaxFacts.GetKeywordKind(identifier) == SyntaxKind.None &&
               SyntaxFacts.GetContextualKeywordKind(identifier) == SyntaxKind.None
            ? identifier
            : "@" + identifier;
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

        constraints.AddRange(typeParameter.ConstraintTypes.Select(static type => type.ToDisplayString(GeneratorFormats.FullyQualifiedNullableType)));

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
}
