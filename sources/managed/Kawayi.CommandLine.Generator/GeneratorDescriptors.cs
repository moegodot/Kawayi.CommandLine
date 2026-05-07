// Copyright (c) 2026 MoeGodot<me@kawayi.moe>.
// Licensed under the GNU Affero General Public License v3-or-later license.

using Microsoft.CodeAnalysis;

namespace Kawayi.CommandLine.Generator;

internal static class GeneratorDescriptors
{
    public static readonly DiagnosticDescriptor DocumentNonPartial = new(
        id: "KCLG001",
        title: "ExportDocument target must be partial",
        messageFormat: "Type '{0}' must be declared partial to generate an IDocumentExporter implementation",
        category: "Kawayi.CommandLine.Generator",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor SymbolsNonPartial = new(
        id: "KCLG101",
        title: "ExportSymbols target must be partial",
        messageFormat: "Type '{0}' must be declared partial to generate an ISymbolExporter implementation",
        category: "Kawayi.CommandLine.Generator",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor MissingDocumentExporter = new(
        id: "KCLG102",
        title: "ExportSymbols target must provide documents",
        messageFormat: "Type '{0}' must implement IDocumentExporter, or use ExportDocumentAttribute or CommandAttribute to generate document exports, before symbol exports can be generated",
        category: "Kawayi.CommandLine.Generator",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor MultipleRole = new(
        id: "KCLG103",
        title: "Member cannot declare multiple symbol roles",
        messageFormat: "Member '{0}' cannot be annotated with multiple symbol role attributes at the same time",
        category: "Kawayi.CommandLine.Generator",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor MissingValueRange = new(
        id: "KCLG104",
        title: "Argument is missing ValueRangeAttribute",
        messageFormat: "Member '{0}' is annotated with ArgumentAttribute but is missing ValueRangeAttribute",
        category: "Kawayi.CommandLine.Generator",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor InvalidPropertyAlias = new(
        id: "KCLG105",
        title: "Property aliases require PropertyAttribute",
        messageFormat: "Member '{0}' uses LongAliasAttribute or ShortAliasAttribute but is not annotated with PropertyAttribute",
        category: "Kawayi.CommandLine.Generator",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor InvalidSubcommandAlias = new(
        id: "KCLG106",
        title: "Subcommand aliases require SubcommandAttribute",
        messageFormat: "Member '{0}' uses AliasAttribute but is not annotated with SubcommandAttribute",
        category: "Kawayi.CommandLine.Generator",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor DuplicateArgumentPosition = new(
        id: "KCLG107",
        title: "Argument position must be unique",
        messageFormat: "Argument position '{0}' is used more than once in the current type; conflicting member: '{1}'",
        category: "Kawayi.CommandLine.Generator",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor AliasConflict = new(
        id: "KCLG108",
        title: "Alias or subcommand name conflict",
        messageFormat: "Name or alias '{0}' is used more than once in the current type; conflicting members: '{1}' and '{2}'",
        category: "Kawayi.CommandLine.Generator",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor InvalidValidatorTarget = new(
        id: "KCLG109",
        title: "ValidatorAttribute requires ArgumentAttribute or PropertyAttribute",
        messageFormat: "Member '{0}' uses ValidatorAttribute but is not annotated with ArgumentAttribute or PropertyAttribute",
        category: "Kawayi.CommandLine.Generator",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor InvalidValidatorMethod = new(
        id: "KCLG110",
        title: "Validator method must be a matching static method",
        messageFormat: "Validator '{0}' for member '{1}' must resolve to exactly one static non-generic method returning string? and accepting '{2}'",
        category: "Kawayi.CommandLine.Generator",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor NonNullableSubcommand = new(
        id: "KCLG111",
        title: "Subcommand property should be nullable",
        messageFormat: "Subcommand member '{0}' should be nullable because binding assigns null when the subcommand is not selected",
        category: "Kawayi.CommandLine.Generator",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor NonNullableRequirementIfNull = new(
        id: "KCLG112",
        title: "RequirementIfNull member must be nullable",
        messageFormat: "Member '{0}' uses requirementIfNull but its type is not nullable",
        category: "Kawayi.CommandLine.Generator",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor RequiredSubcommandUnsupported = new(
        id: "KCLG113",
        title: "Required subcommands are not supported",
        messageFormat: "Subcommand member '{0}' sets require to true, but required subcommands are not supported",
        category: "Kawayi.CommandLine.Generator",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor ParsingNonPartial = new(
        id: "KCLG201",
        title: "ExportParsing target must be partial",
        messageFormat: "Type '{0}' must be declared partial to generate schema exports",
        category: "Kawayi.CommandLine.Generator",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor MissingSymbolExporterForParsing = new(
        id: "KCLG202",
        title: "ExportParsing target must provide symbols",
        messageFormat: "Type '{0}' must implement ISymbolExporter, or use ExportSymbolsAttribute or CommandAttribute to generate symbol exports, before schema exports can be generated",
        category: "Kawayi.CommandLine.Generator",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor InvalidSubcommandExporter = new(
        id: "KCLG203",
        title: "Subcommand type must provide schema exports",
        messageFormat: "Subcommand member '{0}' must target a type that implements ICliSchemaExporter or is annotated with ExportParsingAttribute or CommandAttribute",
        category: "Kawayi.CommandLine.Generator",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor BindingNonPartial = new(
        id: "KCLG301",
        title: "Bindable target must be partial",
        messageFormat: "Type '{0}' must be declared partial to generate an IBindable implementation",
        category: "Kawayi.CommandLine.Generator",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor MissingSymbolExporterForBinding = new(
        id: "KCLG302",
        title: "Bindable target must provide symbols",
        messageFormat: "Type '{0}' must implement ISymbolExporter, or use ExportSymbolsAttribute or CommandAttribute to generate symbol exports, before binding exports can be generated",
        category: "Kawayi.CommandLine.Generator",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor UnassignableMember = new(
        id: "KCLG303",
        title: "Bindable member must be assignable",
        messageFormat: "Member '{0}' must have a non-init setter to be populated by generated binding",
        category: "Kawayi.CommandLine.Generator",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor InvalidSubcommandBindable = new(
        id: "KCLG304",
        title: "Subcommand type must provide binding exports",
        messageFormat: "Subcommand member '{0}' must target a type that implements IBindable or is annotated with BindableAttribute or CommandAttribute",
        category: "Kawayi.CommandLine.Generator",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor MissingSubcommandConstructor = new(
        id: "KCLG305",
        title: "Subcommand type must have an accessible parameterless constructor",
        messageFormat: "Subcommand member '{0}' must target a type with an accessible parameterless constructor",
        category: "Kawayi.CommandLine.Generator",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);
}

