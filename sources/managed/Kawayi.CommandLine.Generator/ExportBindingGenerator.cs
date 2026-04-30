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
/// Generates <c>IBindable</c> implementations for types annotated with
/// <c>BindableAttribute</c> or <c>CommandAttribute</c>.
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed partial class ExportBindingGenerator : IIncrementalGenerator
{
    private const string BindableAttributeMetadataName =
        "Kawayi.CommandLine.Core.Attributes.BindableAttribute";

    private const string CommandAttributeMetadataName =
        "Kawayi.CommandLine.Core.Attributes.CommandAttribute";

    private const string ExportSymbolsAttributeMetadataName =
        "Kawayi.CommandLine.Core.Attributes.ExportSymbolsAttribute";

    private const string SymbolExporterMetadataName =
        "Kawayi.CommandLine.Abstractions.ISymbolExporter";

    private const string BindableMetadataName =
        "Kawayi.CommandLine.Abstractions.IBindable";

    private const string ArgumentAttributeMetadataName =
        "Kawayi.CommandLine.Core.Attributes.ArgumentAttribute";

    private const string PropertyAttributeMetadataName =
        "Kawayi.CommandLine.Core.Attributes.PropertyAttribute";

    private const string SubcommandAttributeMetadataName =
        "Kawayi.CommandLine.Core.Attributes.SubcommandAttribute";

    private static readonly DiagnosticDescriptor NonPartialDiagnostic = new(
        id: "KCLG301",
        title: "Bindable target must be partial",
        messageFormat: "Type '{0}' must be declared partial to generate an IBindable implementation",
        category: "Kawayi.CommandLine.Generator",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor MissingSymbolExporterDiagnostic = new(
        id: "KCLG302",
        title: "Bindable target must provide symbols",
        messageFormat: "Type '{0}' must implement ISymbolExporter, or use ExportSymbolsAttribute or CommandAttribute to generate symbol exports, before binding exports can be generated",
        category: "Kawayi.CommandLine.Generator",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor UnassignableMemberDiagnostic = new(
        id: "KCLG303",
        title: "Bindable member must be assignable",
        messageFormat: "Member '{0}' must have a non-init setter to be populated by generated binding",
        category: "Kawayi.CommandLine.Generator",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor InvalidSubcommandBindableDiagnostic = new(
        id: "KCLG304",
        title: "Subcommand type must provide binding exports",
        messageFormat: "Subcommand member '{0}' must target a type that implements IBindable or is annotated with BindableAttribute or CommandAttribute",
        category: "Kawayi.CommandLine.Generator",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor MissingSubcommandConstructorDiagnostic = new(
        id: "KCLG305",
        title: "Subcommand type must have an accessible parameterless constructor",
        messageFormat: "Subcommand member '{0}' must target a type with an accessible parameterless constructor",
        category: "Kawayi.CommandLine.Generator",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly SymbolDisplayFormat FullyQualifiedTypeFormat = new(
        globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Included,
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        miscellaneousOptions:
        SymbolDisplayMiscellaneousOptions.UseSpecialTypes |
        SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers);

    private static readonly SymbolDisplayFormat FullyQualifiedNullableTypeFormat = new(
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

        if (!HasAttribute(typeSymbol, BindableAttributeMetadataName) &&
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

        var members = ImmutableArray.CreateBuilder<MemberBinding>();
        var declarationOrder = 0;

        foreach (var propertySymbol in typeSymbol.GetMembers().OfType<IPropertySymbol>())
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (propertySymbol.IsImplicitlyDeclared || propertySymbol.IsStatic || propertySymbol.IsIndexer)
            {
                continue;
            }

            var kind = GetMemberKind(propertySymbol);
            if (kind is null)
            {
                continue;
            }

            if (!CanAssign(propertySymbol))
            {
                diagnostics.Add(new DiagnosticInfo(UnassignableMemberDiagnostic,
                                                   propertySymbol.Locations.FirstOrDefault(),
                                                   [propertySymbol.Name]));
                continue;
            }

            var isGlobalSubcommand = kind == MemberKind.Subcommand &&
                                     GetAttributeBool(GetAttribute(propertySymbol, SubcommandAttributeMetadataName)!, 2, false);

            if (kind == MemberKind.Subcommand)
            {
                if (!SupportsBinding(propertySymbol.Type))
                {
                    diagnostics.Add(new DiagnosticInfo(InvalidSubcommandBindableDiagnostic,
                                                       propertySymbol.Locations.FirstOrDefault(),
                                                       [propertySymbol.Name]));
                    continue;
                }

                if (!HasAccessibleParameterlessConstructor(propertySymbol.Type))
                {
                    diagnostics.Add(new DiagnosticInfo(MissingSubcommandConstructorDiagnostic,
                                                       propertySymbol.Locations.FirstOrDefault(),
                                                       [propertySymbol.Name]));
                    continue;
                }

                if (isGlobalSubcommand && !SupportsGeneratedBinding(propertySymbol.Type))
                {
                    diagnostics.Add(new DiagnosticInfo(InvalidSubcommandBindableDiagnostic,
                                                       propertySymbol.Locations.FirstOrDefault(),
                                                       [propertySymbol.Name]));
                    continue;
                }
            }

            members.Add(new MemberBinding(kind.Value,
                                          propertySymbol.Name,
                                          propertySymbol.Type.ToDisplayString(FullyQualifiedTypeFormat),
                                          propertySymbol.Type.ToDisplayString(FullyQualifiedNullableTypeFormat),
                                          declarationOrder++,
                                          isGlobalSubcommand));
        }

        return new ExportTarget(typeSymbol,
                                missingPartialSymbol,
                                hasSymbolExporter,
                                members.ToImmutable(),
                                diagnostics.ToImmutable());
    }

    private static MemberKind? GetMemberKind(IPropertySymbol propertySymbol)
    {
        var isArgument = HasAttribute(propertySymbol, ArgumentAttributeMetadataName);
        var isProperty = HasAttribute(propertySymbol, PropertyAttributeMetadataName);
        var isSubcommand = HasAttribute(propertySymbol, SubcommandAttributeMetadataName);

        if (isArgument)
        {
            return MemberKind.Argument;
        }

        if (isProperty)
        {
            return MemberKind.Property;
        }

        return isSubcommand ? MemberKind.Subcommand : null;
    }

    private static bool CanAssign(IPropertySymbol propertySymbol)
    {
        return propertySymbol.SetMethod is { IsInitOnly: false };
    }

    private static bool SupportsBinding(ITypeSymbol typeSymbol)
    {
        if (typeSymbol is not INamedTypeSymbol namedType)
        {
            return false;
        }

        return namedType.AllInterfaces.Any(static item => item.ToDisplayString() == BindableMetadataName) ||
               HasAttribute(namedType, BindableAttributeMetadataName) ||
               HasAttribute(namedType, CommandAttributeMetadataName);
    }

    private static bool SupportsGeneratedBinding(ITypeSymbol typeSymbol)
    {
        return typeSymbol is INamedTypeSymbol namedType &&
               (HasAttribute(namedType, BindableAttributeMetadataName) ||
                HasAttribute(namedType, CommandAttributeMetadataName));
    }

    private static bool HasAccessibleParameterlessConstructor(ITypeSymbol typeSymbol)
    {
        if (typeSymbol is not INamedTypeSymbol namedType)
        {
            return false;
        }

        return namedType.InstanceConstructors.Any(static constructor =>
            constructor.Parameters.Length == 0 &&
            !constructor.IsStatic &&
            constructor.DeclaredAccessibility is Accessibility.Public
                or Accessibility.Internal
                or Accessibility.ProtectedOrInternal);
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
        AppendBindMethod(builder, target, containingTypes.Length + 1);
        AppendHelpers(builder, target, containingTypes.Length + 1);
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

    private static void AppendBindMethod(StringBuilder builder, ExportTarget target, int indentLevel)
    {
        AppendIndentedLine(builder, indentLevel, "/// <inheritdoc />");
        AppendIndentedLine(
            builder,
            indentLevel,
            "public void Bind(global::Kawayi.CommandLine.Abstractions.IParsingResultCollection results)");
        AppendIndentedLine(builder, indentLevel, "{");
        AppendIndentedLine(builder, indentLevel + 1, "global::System.ArgumentNullException.ThrowIfNull(results);");
        AppendIndentedLine(builder, indentLevel + 1, "var bindingScope = FindBindingScope(results);");

        foreach (var member in target.Members.OrderBy(static item => item.DeclarationOrder))
        {
            switch (member.Kind)
            {
                case MemberKind.Argument:
                    AppendTypedMemberBinding(builder, member, "global::Kawayi.CommandLine.Abstractions.ArgumentDefinition", indentLevel + 1);
                    break;
                case MemberKind.Property:
                    AppendTypedMemberBinding(builder, member, "global::Kawayi.CommandLine.Abstractions.PropertyDefinition", indentLevel + 1);
                    break;
                case MemberKind.Subcommand:
                    AppendSubcommandBinding(builder, member, indentLevel + 1);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(member.Kind));
            }
        }

        AppendIndentedLine(builder, indentLevel, "}");
    }

    private static void AppendTypedMemberBinding(
        StringBuilder builder,
        MemberBinding member,
        string definitionType,
        int indentLevel)
    {
        var definitionVariable = member.MemberName + "Definition";
        var commandLineNameExpression = GenerateCommandLineNameExpression(member.MemberName);

        AppendIndentedLine(
            builder,
            indentLevel,
            $"var {definitionVariable} = GetRequiredTypedDefinition<{definitionType}>(bindingScope, {commandLineNameExpression});");
        AppendIndentedLine(
            builder,
            indentLevel,
            $"{EscapeIdentifier(member.MemberName)} = ({member.NullableTypeName})GetEffectiveValue(bindingScope, {definitionVariable}, {SymbolDisplay.FormatLiteral(member.MemberName, true)})!;");
    }

    private static void AppendSubcommandBinding(StringBuilder builder, MemberBinding member, int indentLevel)
    {
        if (member.IsGlobal)
        {
            var globalPropertyName = EscapeIdentifier(member.MemberName);
            var globalChildVariable = member.MemberName + "Value";
            AppendIndentedLine(builder, indentLevel, $"var {globalChildVariable} = new {member.TypeName}();");
            AppendIndentedLine(
                builder,
                indentLevel,
                $"((global::Kawayi.CommandLine.Abstractions.IBindable){globalChildVariable}).Bind(bindingScope);");
            AppendIndentedLine(builder, indentLevel, $"{globalPropertyName} = {globalChildVariable};");
            return;
        }

        var definitionVariable = member.MemberName + "Definition";
        var resultVariable = member.MemberName + "Results";
        var childVariable = member.MemberName + "Value";
        var commandLineNameExpression = GenerateCommandLineNameExpression(member.MemberName);
        var propertyName = EscapeIdentifier(member.MemberName);

        AppendIndentedLine(
            builder,
            indentLevel,
            $"var {definitionVariable} = GetRequiredSubcommandDefinition(bindingScope, {commandLineNameExpression});");
        AppendIndentedLine(
            builder,
            indentLevel,
            $"if (bindingScope.TryGetSubcommand({definitionVariable}, out var {resultVariable}))");
        AppendIndentedLine(builder, indentLevel, "{");
        AppendIndentedLine(builder, indentLevel + 1, $"var {childVariable} = new {member.TypeName}();");
        AppendIndentedLine(
            builder,
            indentLevel + 1,
            $"((global::Kawayi.CommandLine.Abstractions.IBindable){childVariable}).Bind({resultVariable});");
        AppendIndentedLine(builder, indentLevel + 1, $"{propertyName} = {childVariable};");
        AppendIndentedLine(builder, indentLevel, "}");
        AppendIndentedLine(builder, indentLevel, "else");
        AppendIndentedLine(builder, indentLevel, "{");
        AppendIndentedLine(builder, indentLevel + 1, $"{propertyName} = default!;");
        AppendIndentedLine(builder, indentLevel, "}");
    }

    private static void AppendHelpers(StringBuilder builder, ExportTarget target, int indentLevel)
    {
        var typeNameLiteral = SymbolDisplay.FormatLiteral(
            target.TypeSymbol.ToDisplayString(FullyQualifiedTypeFormat),
            true);

        AppendIndentedLine(builder, indentLevel, string.Empty);
        AppendIndentedLine(
            builder,
            indentLevel,
            "private static global::Kawayi.CommandLine.Abstractions.IParsingResultCollection FindBindingScope(global::Kawayi.CommandLine.Abstractions.IParsingResultCollection results)");
        AppendIndentedLine(builder, indentLevel, "{");
        AppendIndentedLine(builder, indentLevel + 1, "for (var current = results; current is not null; current = current.Parent)");
        AppendIndentedLine(builder, indentLevel + 1, "{");
        AppendIndentedLine(builder, indentLevel + 2, "if (MatchesGeneratedBindingScope(current))");
        AppendIndentedLine(builder, indentLevel + 2, "{");
        AppendIndentedLine(builder, indentLevel + 3, "return current;");
        AppendIndentedLine(builder, indentLevel + 2, "}");
        AppendIndentedLine(builder, indentLevel + 1, "}");
        AppendIndentedLine(
            builder,
            indentLevel + 1,
            $"throw new global::System.InvalidOperationException(\"No parsing result scope matches bindable type \" + {typeNameLiteral} + \".\");");
        AppendIndentedLine(builder, indentLevel, "}");

        AppendIndentedLine(builder, indentLevel, string.Empty);
        AppendIndentedLine(
            builder,
            indentLevel,
            "internal static bool MatchesGeneratedBindingScope(global::Kawayi.CommandLine.Abstractions.IParsingResultCollection scope)");
        AppendIndentedLine(builder, indentLevel, "{");

        if (target.Members.IsDefaultOrEmpty)
        {
            AppendIndentedLine(builder, indentLevel + 1, "return true;");
        }
        else
        {
            AppendIndentedLine(builder, indentLevel + 1, "return");
            var members = target.Members.OrderBy(static item => item.DeclarationOrder).ToArray();
            for (var i = 0; i < members.Length; i++)
            {
                var member = members[i];
                var suffix = i == members.Length - 1 ? ";" : " &&";
                var expression = member.Kind switch
                {
                    MemberKind.Argument => $"HasTypedDefinition<global::Kawayi.CommandLine.Abstractions.ArgumentDefinition>(scope, {GenerateCommandLineNameExpression(member.MemberName)})",
                    MemberKind.Property => $"HasTypedDefinition<global::Kawayi.CommandLine.Abstractions.PropertyDefinition>(scope, {GenerateCommandLineNameExpression(member.MemberName)})",
                    MemberKind.Subcommand when member.IsGlobal => $"{member.TypeName}.MatchesGeneratedBindingScope(scope)",
                    MemberKind.Subcommand => $"HasSubcommandDefinition(scope, {GenerateCommandLineNameExpression(member.MemberName)})",
                    _ => throw new ArgumentOutOfRangeException(nameof(member.Kind))
                };

                AppendIndentedLine(builder, indentLevel + 2, expression + suffix);
            }
        }

        AppendIndentedLine(builder, indentLevel, "}");

        AppendIndentedLine(builder, indentLevel, string.Empty);
        AppendIndentedLine(
            builder,
            indentLevel,
            "private static bool HasTypedDefinition<TDefinition>(global::Kawayi.CommandLine.Abstractions.IParsingResultCollection scope, string name) where TDefinition : global::Kawayi.CommandLine.Abstractions.TypedDefinition");
        AppendIndentedLine(builder, indentLevel, "{");
        AppendIndentedLine(builder, indentLevel + 1, "foreach (var definition in scope.Scope.AvailableTypedDefinitions)");
        AppendIndentedLine(builder, indentLevel + 1, "{");
        AppendIndentedLine(
            builder,
            indentLevel + 2,
            "if (definition is TDefinition && global::System.StringComparer.Ordinal.Equals(definition.Information.Name.Value, name))");
        AppendIndentedLine(builder, indentLevel + 2, "{");
        AppendIndentedLine(builder, indentLevel + 3, "return true;");
        AppendIndentedLine(builder, indentLevel + 2, "}");
        AppendIndentedLine(builder, indentLevel + 1, "}");
        AppendIndentedLine(builder, indentLevel + 1, "return false;");
        AppendIndentedLine(builder, indentLevel, "}");

        AppendIndentedLine(builder, indentLevel, string.Empty);
        AppendIndentedLine(
            builder,
            indentLevel,
            "private static TDefinition GetRequiredTypedDefinition<TDefinition>(global::Kawayi.CommandLine.Abstractions.IParsingResultCollection scope, string name) where TDefinition : global::Kawayi.CommandLine.Abstractions.TypedDefinition");
        AppendIndentedLine(builder, indentLevel, "{");
        AppendIndentedLine(builder, indentLevel + 1, "foreach (var definition in scope.Scope.AvailableTypedDefinitions)");
        AppendIndentedLine(builder, indentLevel + 1, "{");
        AppendIndentedLine(
            builder,
            indentLevel + 2,
            "if (definition is TDefinition typedDefinition && global::System.StringComparer.Ordinal.Equals(definition.Information.Name.Value, name))");
        AppendIndentedLine(builder, indentLevel + 2, "{");
        AppendIndentedLine(builder, indentLevel + 3, "return typedDefinition;");
        AppendIndentedLine(builder, indentLevel + 2, "}");
        AppendIndentedLine(builder, indentLevel + 1, "}");
        AppendIndentedLine(
            builder,
            indentLevel + 1,
            "throw new global::System.InvalidOperationException(\"Required typed definition '\" + name + \"' was not found in the selected parsing result scope.\");");
        AppendIndentedLine(builder, indentLevel, "}");

        AppendIndentedLine(builder, indentLevel, string.Empty);
        AppendIndentedLine(
            builder,
            indentLevel,
            "private static bool HasSubcommandDefinition(global::Kawayi.CommandLine.Abstractions.IParsingResultCollection scope, string name)");
        AppendIndentedLine(builder, indentLevel, "{");
        AppendIndentedLine(builder, indentLevel + 1, "foreach (var definition in scope.Scope.AvailableSubcommands)");
        AppendIndentedLine(builder, indentLevel + 1, "{");
        AppendIndentedLine(
            builder,
            indentLevel + 2,
            "if (global::System.StringComparer.Ordinal.Equals(definition.Information.Name.Value, name))");
        AppendIndentedLine(builder, indentLevel + 2, "{");
        AppendIndentedLine(builder, indentLevel + 3, "return true;");
        AppendIndentedLine(builder, indentLevel + 2, "}");
        AppendIndentedLine(builder, indentLevel + 1, "}");
        AppendIndentedLine(builder, indentLevel + 1, "return false;");
        AppendIndentedLine(builder, indentLevel, "}");

        AppendIndentedLine(builder, indentLevel, string.Empty);
        AppendIndentedLine(
            builder,
            indentLevel,
            "private static global::Kawayi.CommandLine.Abstractions.CommandDefinition GetRequiredSubcommandDefinition(global::Kawayi.CommandLine.Abstractions.IParsingResultCollection scope, string name)");
        AppendIndentedLine(builder, indentLevel, "{");
        AppendIndentedLine(builder, indentLevel + 1, "foreach (var definition in scope.Scope.AvailableSubcommands)");
        AppendIndentedLine(builder, indentLevel + 1, "{");
        AppendIndentedLine(
            builder,
            indentLevel + 2,
            "if (global::System.StringComparer.Ordinal.Equals(definition.Information.Name.Value, name))");
        AppendIndentedLine(builder, indentLevel + 2, "{");
        AppendIndentedLine(builder, indentLevel + 3, "return definition;");
        AppendIndentedLine(builder, indentLevel + 2, "}");
        AppendIndentedLine(builder, indentLevel + 1, "}");
        AppendIndentedLine(
            builder,
            indentLevel + 1,
            "throw new global::System.InvalidOperationException(\"Required subcommand definition '\" + name + \"' was not found in the selected parsing result scope.\");");
        AppendIndentedLine(builder, indentLevel, "}");

        AppendIndentedLine(builder, indentLevel, string.Empty);
        AppendIndentedLine(
            builder,
            indentLevel,
            "private static object? GetEffectiveValue(global::Kawayi.CommandLine.Abstractions.IParsingResultCollection scope, global::Kawayi.CommandLine.Abstractions.TypedDefinition definition, string memberName)");
        AppendIndentedLine(builder, indentLevel, "{");
        AppendIndentedLine(builder, indentLevel + 1, "if (scope.TryGetValue(definition, out var explicitValue))");
        AppendIndentedLine(builder, indentLevel + 1, "{");
        AppendIndentedLine(builder, indentLevel + 2, "return explicitValue;");
        AppendIndentedLine(builder, indentLevel + 1, "}");
        AppendIndentedLine(builder, indentLevel + 1, "if (definition.DefaultValueFactory is not null)");
        AppendIndentedLine(builder, indentLevel + 1, "{");
        AppendIndentedLine(builder, indentLevel + 2, "return definition.DefaultValueFactory(scope);");
        AppendIndentedLine(builder, indentLevel + 1, "}");
        AppendIndentedLine(builder, indentLevel + 1, "if (definition.Requirement)");
        AppendIndentedLine(builder, indentLevel + 1, "{");
        AppendIndentedLine(
            builder,
            indentLevel + 2,
            "throw new global::System.InvalidOperationException(\"Required member '\" + memberName + \"' does not have an explicit value or default factory.\");");
        AppendIndentedLine(builder, indentLevel + 1, "}");
        AppendIndentedLine(builder, indentLevel + 1, "return global::Kawayi.CommandLine.Core.TypeDefaultValues.GetValue(definition.Type);");
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

        if (includeInterface && !typeSymbol.AllInterfaces.Any(static item => item.ToDisplayString() == BindableMetadataName))
        {
            builder.Append(" : global::Kawayi.CommandLine.Abstractions.IBindable");
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

    private static string GenerateCommandLineNameExpression(string memberName)
    {
        return $"global::Kawayi.CommandLine.Abstractions.CaseConverter.Pascal2Kebab({SymbolDisplay.FormatLiteral(memberName, true)})";
    }

    private static string EscapeIdentifier(string identifier)
    {
        return SyntaxFacts.GetKeywordKind(identifier) == SyntaxKind.None &&
               SyntaxFacts.GetContextualKeywordKind(identifier) == SyntaxKind.None
            ? identifier
            : "@" + identifier;
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

    private static bool GetAttributeBool(AttributeData attribute, int index, bool defaultValue)
    {
        if (attribute.ConstructorArguments.Length <= index)
        {
            return defaultValue;
        }

        return attribute.ConstructorArguments[index].Value is bool value ? value : defaultValue;
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
        return $"{sanitizedName}.ExportBinding.g.cs";
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
            ImmutableArray<MemberBinding> members,
            ImmutableArray<DiagnosticInfo> diagnostics)
        {
            TypeSymbol = typeSymbol;
            MissingPartialSymbol = missingPartialSymbol;
            HasSymbolExporter = hasSymbolExporter;
            Members = members;
            Diagnostics = diagnostics;
        }

        public INamedTypeSymbol TypeSymbol { get; }

        public INamedTypeSymbol? MissingPartialSymbol { get; }

        public bool HasSymbolExporter { get; }

        public ImmutableArray<MemberBinding> Members { get; }

        public ImmutableArray<DiagnosticInfo> Diagnostics { get; }
    }

    private sealed class MemberBinding
    {
        public MemberBinding(
            MemberKind kind,
            string memberName,
            string typeName,
            string nullableTypeName,
            int declarationOrder,
            bool isGlobal)
        {
            Kind = kind;
            MemberName = memberName;
            TypeName = typeName;
            NullableTypeName = nullableTypeName;
            DeclarationOrder = declarationOrder;
            IsGlobal = isGlobal;
        }

        public MemberKind Kind { get; }

        public string MemberName { get; }

        public string TypeName { get; }

        public string NullableTypeName { get; }

        public int DeclarationOrder { get; }

        public bool IsGlobal { get; }
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

    private enum MemberKind
    {
        Argument,
        Property,
        Subcommand
    }
}
