// Copyright (c) 2026 MoeGodot<me@kawayi.moe>.
// Licensed under the GNU Affero General Public License v3-or-later license.

using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace Kawayi.CommandLine.Generator;

/// <summary>
/// Generates <c>IBindable</c> implementations for types annotated with
/// <c>BindableAttribute</c> or <c>CommandAttribute</c>.
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class ExportBindingGenerator : IIncrementalGenerator
{
    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var explicitTargets = context.SyntaxProvider.ForAttributeWithMetadataName(
            MetadataNames.BindableAttribute,
            predicate: static (node, _) => node is Microsoft.CodeAnalysis.CSharp.Syntax.TypeDeclarationSyntax,
            transform: static (syntaxContext, cancellationToken) =>
                CreateTarget(syntaxContext, skipCommandTargets: true, cancellationToken));

        var commandTargets = context.SyntaxProvider.ForAttributeWithMetadataName(
            MetadataNames.CommandAttribute,
            predicate: static (node, _) => node is Microsoft.CodeAnalysis.CSharp.Syntax.TypeDeclarationSyntax,
            transform: static (syntaxContext, cancellationToken) =>
                CreateTarget(syntaxContext, skipCommandTargets: false, cancellationToken));

        context.RegisterSourceOutput(explicitTargets.Where(static target => target is not null),
                                     static (productionContext, target) => Emit(productionContext, target!));
        context.RegisterSourceOutput(commandTargets.Where(static target => target is not null),
                                     static (productionContext, target) => Emit(productionContext, target!));
    }

    private static BindingTarget? CreateTarget(
        GeneratorAttributeSyntaxContext context,
        bool skipCommandTargets,
        CancellationToken cancellationToken)
    {
        if (context.TargetSymbol is not INamedTypeSymbol typeSymbol)
        {
            return null;
        }

        if (skipCommandTargets && CommandModel.HasAttribute(typeSymbol, MetadataNames.CommandAttribute))
        {
            return null;
        }

        var model = CommandModel.Create(typeSymbol, cancellationToken);
        var diagnostics = ImmutableArray.CreateBuilder<DiagnosticInfo>();
        var members = ImmutableArray.CreateBuilder<MemberModel>();

        foreach (var member in model.Members)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!CanAssign(member.PropertySymbol))
            {
                diagnostics.Add(new DiagnosticInfo(GeneratorDescriptors.UnassignableMember, member.Location, [member.MemberName]));
                continue;
            }

            if (member.Kind == MemberKind.Subcommand)
            {
                if (!SupportsBinding(member.PropertySymbol.Type, cancellationToken, new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default)))
                {
                    diagnostics.Add(new DiagnosticInfo(GeneratorDescriptors.InvalidSubcommandBindable, member.Location, [member.MemberName]));
                    continue;
                }

                if (!HasAccessibleParameterlessConstructor(member.PropertySymbol.Type))
                {
                    diagnostics.Add(new DiagnosticInfo(GeneratorDescriptors.MissingSubcommandConstructor, member.Location, [member.MemberName]));
                    continue;
                }
            }

            members.Add(member);
        }

        return new BindingTarget(model, members.ToImmutable(), diagnostics.ToImmutable());
    }

    private static bool CanAssign(IPropertySymbol propertySymbol)
    {
        return propertySymbol.SetMethod is { IsInitOnly: false };
    }

    private static bool SupportsBinding(
        ITypeSymbol typeSymbol,
        CancellationToken cancellationToken,
        HashSet<ITypeSymbol> visited)
    {
        if (typeSymbol is not INamedTypeSymbol namedType)
        {
            return false;
        }

        if (CommandModel.ImplementsInterface(namedType, MetadataNames.Bindable))
        {
            return true;
        }

        if (!CommandModel.HasAttribute(namedType, MetadataNames.BindableAttribute) &&
            !CommandModel.HasAttribute(namedType, MetadataNames.CommandAttribute))
        {
            return false;
        }

        if (!visited.Add(namedType))
        {
            return true;
        }

        var model = CommandModel.Create(namedType, cancellationToken);
        if (model.MissingPartialSymbol is not null ||
            !model.HasSymbolProvider ||
            !model.CanGenerateSymbols)
        {
            return false;
        }

        foreach (var member in model.Members)
        {
            if (!CanAssign(member.PropertySymbol))
            {
                return false;
            }

            if (member.Kind != MemberKind.Subcommand)
            {
                continue;
            }

            if (!HasAccessibleParameterlessConstructor(member.PropertySymbol.Type) ||
                !SupportsBinding(member.PropertySymbol.Type, cancellationToken, visited))
            {
                return false;
            }
        }

        return true;
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

    private static void Emit(SourceProductionContext context, BindingTarget target)
    {
        var model = target.Model;
        if (model.MissingPartialSymbol is not null)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                GeneratorDescriptors.BindingNonPartial,
                model.MissingPartialSymbol.Locations.FirstOrDefault(),
                model.MissingPartialSymbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)));
            return;
        }

        if (!model.HasSymbolProvider)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                GeneratorDescriptors.MissingSymbolExporterForBinding,
                model.TypeSymbol.Locations.FirstOrDefault(),
                model.TypeSymbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)));
        }

        foreach (var diagnostic in target.Diagnostics)
        {
            context.ReportDiagnostic(Diagnostic.Create(diagnostic.Descriptor, diagnostic.Location, diagnostic.Arguments));
        }

        if (!model.HasSymbolProvider ||
            !model.CanGenerateSymbols ||
            target.Diagnostics.Any(static diagnostic => diagnostic.Descriptor.DefaultSeverity == DiagnosticSeverity.Error))
        {
            return;
        }

        context.AddSource(
            GeneratorSource.GetHintName(model.TypeSymbol, "ExportBinding"),
            SourceText.From(GenerateSource(target), Encoding.UTF8));
    }

    private static string GenerateSource(BindingTarget target)
    {
        var model = target.Model;
        var builder = new StringBuilder();
        builder.AppendLine("// <auto-generated/>");
        builder.AppendLine("#nullable enable");

        if (!model.TypeSymbol.ContainingNamespace.IsGlobalNamespace)
        {
            builder.Append("namespace ").Append(model.TypeSymbol.ContainingNamespace.ToDisplayString()).AppendLine();
            builder.AppendLine("{");
        }

        var containingTypes = GeneratorSource.GetContainingTypes(model.TypeSymbol);
        for (var i = 0; i < containingTypes.Length; i++)
        {
            GeneratorSource.AppendIndentedLine(builder, i, GeneratorSource.BuildTypeDeclaration(containingTypes[i], null, includeInterface: false));
            GeneratorSource.AppendIndentedLine(builder, i, "{");
        }

        var interfaceList = model.ImplementsBindable
            ? null
            : "global::Kawayi.CommandLine.Abstractions.IBindable";
        GeneratorSource.AppendIndentedLine(builder, containingTypes.Length, GeneratorSource.BuildTypeDeclaration(model.TypeSymbol, interfaceList, includeInterface: true));
        GeneratorSource.AppendIndentedLine(builder, containingTypes.Length, "{");
        AppendBindMethod(builder, target, containingTypes.Length + 1);
        AppendHelpers(builder, containingTypes.Length + 1);
        GeneratorSource.AppendIndentedLine(builder, containingTypes.Length, "}");

        for (var i = containingTypes.Length - 1; i >= 0; i--)
        {
            GeneratorSource.AppendIndentedLine(builder, i, "}");
        }

        if (!model.TypeSymbol.ContainingNamespace.IsGlobalNamespace)
        {
            builder.AppendLine("}");
        }

        return builder.ToString();
    }

    private static void AppendBindMethod(StringBuilder builder, BindingTarget target, int indentLevel)
    {
        GeneratorSource.AppendIndentedLine(builder, indentLevel, "/// <inheritdoc />");
        GeneratorSource.AppendIndentedLine(
            builder,
            indentLevel,
            "public void Bind(global::Kawayi.CommandLine.Abstractions.Cli results)");
        GeneratorSource.AppendIndentedLine(builder, indentLevel, "{");
        GeneratorSource.AppendIndentedLine(builder, indentLevel + 1, "global::System.ArgumentNullException.ThrowIfNull(results);");

        foreach (var member in target.Members.OrderBy(static item => item.DeclarationOrder))
        {
            switch (member.Kind)
            {
                case MemberKind.Argument:
                    AppendTypedMemberBinding(builder, member, "global::Kawayi.CommandLine.Abstractions.ParameterDefinition", indentLevel + 1);
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

        GeneratorSource.AppendIndentedLine(builder, indentLevel, "}");
    }

    private static void AppendTypedMemberBinding(
        StringBuilder builder,
        MemberModel member,
        string definitionType,
        int indentLevel)
    {
        var definitionVariable = member.MemberName + "Definition";

        GeneratorSource.AppendIndentedLine(
            builder,
            indentLevel,
            $"var {definitionVariable} = GetRequiredTypedDefinition<{definitionType}>(results, {member.CommandLineName});");
        GeneratorSource.AppendIndentedLine(
            builder,
            indentLevel,
            $"{GeneratorSource.EscapeIdentifier(member.MemberName)} = ({member.NullableTypeName})GetEffectiveValue(results, {definitionVariable}, {SymbolDisplay.FormatLiteral(member.MemberName, true)})!;");
    }

    private static void AppendSubcommandBinding(StringBuilder builder, MemberModel member, int indentLevel)
    {
        var propertyName = GeneratorSource.EscapeIdentifier(member.MemberName);

        if (member.IsGlobalSubcommand)
        {
            var globalChildVariable = member.MemberName + "Value";
            GeneratorSource.AppendIndentedLine(builder, indentLevel, $"var {globalChildVariable} = new {member.TypeName}();");
            GeneratorSource.AppendIndentedLine(
                builder,
                indentLevel,
                $"((global::Kawayi.CommandLine.Abstractions.IBindable){globalChildVariable}).Bind(results);");
            GeneratorSource.AppendIndentedLine(builder, indentLevel, $"{propertyName} = {globalChildVariable};");
            return;
        }

        var definitionVariable = member.MemberName + "Definition";
        var resultVariable = member.MemberName + "Results";
        var childVariable = member.MemberName + "Value";

        GeneratorSource.AppendIndentedLine(
            builder,
            indentLevel,
            $"var {definitionVariable} = GetRequiredSubcommandDefinition(results, {member.CommandLineName});");
        GeneratorSource.AppendIndentedLine(
            builder,
            indentLevel,
            $"if (results.Subcommands.TryGetValue({definitionVariable}, out var {resultVariable}))");
        GeneratorSource.AppendIndentedLine(builder, indentLevel, "{");
        GeneratorSource.AppendIndentedLine(builder, indentLevel + 1, $"var {childVariable} = new {member.TypeName}();");
        GeneratorSource.AppendIndentedLine(
            builder,
            indentLevel + 1,
            $"((global::Kawayi.CommandLine.Abstractions.IBindable){childVariable}).Bind({resultVariable});");
        GeneratorSource.AppendIndentedLine(builder, indentLevel + 1, $"{propertyName} = {childVariable};");
        GeneratorSource.AppendIndentedLine(builder, indentLevel, "}");
        GeneratorSource.AppendIndentedLine(builder, indentLevel, "else");
        GeneratorSource.AppendIndentedLine(builder, indentLevel, "{");
        GeneratorSource.AppendIndentedLine(builder, indentLevel + 1, $"{propertyName} = default!;");
        GeneratorSource.AppendIndentedLine(builder, indentLevel, "}");
    }

    private static void AppendHelpers(StringBuilder builder, int indentLevel)
    {
        GeneratorSource.AppendIndentedLine(builder, indentLevel, string.Empty);
        GeneratorSource.AppendIndentedLine(
            builder,
            indentLevel,
            "private static TDefinition GetRequiredTypedDefinition<TDefinition>(global::Kawayi.CommandLine.Abstractions.Cli scope, string name) where TDefinition : global::Kawayi.CommandLine.Abstractions.TypedDefinition");
        GeneratorSource.AppendIndentedLine(builder, indentLevel, "{");
        GeneratorSource.AppendIndentedLine(builder, indentLevel + 1, "foreach (var definition in GetAvailableTypedDefinitions(scope))");
        GeneratorSource.AppendIndentedLine(builder, indentLevel + 1, "{");
        GeneratorSource.AppendIndentedLine(
            builder,
            indentLevel + 2,
            "if (definition is TDefinition typedDefinition && global::System.StringComparer.Ordinal.Equals(definition.Information.Name.Value, name))");
        GeneratorSource.AppendIndentedLine(builder, indentLevel + 2, "{");
        GeneratorSource.AppendIndentedLine(builder, indentLevel + 3, "return typedDefinition;");
        GeneratorSource.AppendIndentedLine(builder, indentLevel + 2, "}");
        GeneratorSource.AppendIndentedLine(builder, indentLevel + 1, "}");
        GeneratorSource.AppendIndentedLine(
            builder,
            indentLevel + 1,
            "throw new global::System.InvalidOperationException(\"Required typed definition '\" + name + \"' was not found in the provided parsing result scope.\");");
        GeneratorSource.AppendIndentedLine(builder, indentLevel, "}");

        GeneratorSource.AppendIndentedLine(builder, indentLevel, string.Empty);
        GeneratorSource.AppendIndentedLine(
            builder,
            indentLevel,
            "private static global::Kawayi.CommandLine.Abstractions.CommandDefinition GetRequiredSubcommandDefinition(global::Kawayi.CommandLine.Abstractions.Cli scope, string name)");
        GeneratorSource.AppendIndentedLine(builder, indentLevel, "{");
        GeneratorSource.AppendIndentedLine(builder, indentLevel + 1, "foreach (var definition in global::System.Linq.Enumerable.Distinct(scope.Schema.SubcommandDefinitions.Values))");
        GeneratorSource.AppendIndentedLine(builder, indentLevel + 1, "{");
        GeneratorSource.AppendIndentedLine(
            builder,
            indentLevel + 2,
            "if (global::System.StringComparer.Ordinal.Equals(definition.Information.Name.Value, name))");
        GeneratorSource.AppendIndentedLine(builder, indentLevel + 2, "{");
        GeneratorSource.AppendIndentedLine(builder, indentLevel + 3, "return definition;");
        GeneratorSource.AppendIndentedLine(builder, indentLevel + 2, "}");
        GeneratorSource.AppendIndentedLine(builder, indentLevel + 1, "}");
        GeneratorSource.AppendIndentedLine(
            builder,
            indentLevel + 1,
            "throw new global::System.InvalidOperationException(\"Required subcommand definition '\" + name + \"' was not found in the provided parsing result scope.\");");
        GeneratorSource.AppendIndentedLine(builder, indentLevel, "}");

        GeneratorSource.AppendIndentedLine(builder, indentLevel, string.Empty);
        GeneratorSource.AppendIndentedLine(
            builder,
            indentLevel,
            "private static object? GetEffectiveValue(global::Kawayi.CommandLine.Abstractions.Cli scope, global::Kawayi.CommandLine.Abstractions.TypedDefinition definition, string memberName)");
        GeneratorSource.AppendIndentedLine(builder, indentLevel, "{");
        GeneratorSource.AppendIndentedLine(builder, indentLevel + 1, "if (TryGetValue(scope, definition, out var explicitValue))");
        GeneratorSource.AppendIndentedLine(builder, indentLevel + 1, "{");
        GeneratorSource.AppendIndentedLine(builder, indentLevel + 2, "EnsureEffectiveValueSatisfiesRequirement(definition, explicitValue, memberName);");
        GeneratorSource.AppendIndentedLine(builder, indentLevel + 2, "return explicitValue;");
        GeneratorSource.AppendIndentedLine(builder, indentLevel + 1, "}");
        GeneratorSource.AppendIndentedLine(builder, indentLevel + 1, "if (definition.DefaultValueFactory is not null)");
        GeneratorSource.AppendIndentedLine(builder, indentLevel + 1, "{");
        GeneratorSource.AppendIndentedLine(builder, indentLevel + 2, "var defaultValue = definition.DefaultValueFactory(scope);");
        GeneratorSource.AppendIndentedLine(builder, indentLevel + 2, "EnsureEffectiveValueSatisfiesRequirement(definition, defaultValue, memberName);");
        GeneratorSource.AppendIndentedLine(builder, indentLevel + 2, "return defaultValue;");
        GeneratorSource.AppendIndentedLine(builder, indentLevel + 1, "}");
        GeneratorSource.AppendIndentedLine(builder, indentLevel + 1, "if (definition.Requirement)");
        GeneratorSource.AppendIndentedLine(builder, indentLevel + 1, "{");
        GeneratorSource.AppendIndentedLine(
            builder,
            indentLevel + 2,
            "throw new global::System.InvalidOperationException(\"Required member '\" + memberName + \"' does not have an explicit value or default factory.\");");
        GeneratorSource.AppendIndentedLine(builder, indentLevel + 1, "}");
        GeneratorSource.AppendIndentedLine(builder, indentLevel + 1, "var clrDefault = global::Kawayi.CommandLine.Core.TypeDefaultValues.GetValue(definition.Type);");
        GeneratorSource.AppendIndentedLine(builder, indentLevel + 1, "EnsureEffectiveValueSatisfiesRequirement(definition, clrDefault, memberName);");
        GeneratorSource.AppendIndentedLine(builder, indentLevel + 1, "return clrDefault;");
        GeneratorSource.AppendIndentedLine(builder, indentLevel, "}");

        GeneratorSource.AppendIndentedLine(builder, indentLevel, string.Empty);
        GeneratorSource.AppendIndentedLine(
            builder,
            indentLevel,
            "private static global::System.Collections.Generic.IEnumerable<global::Kawayi.CommandLine.Abstractions.TypedDefinition> GetAvailableTypedDefinitions(global::Kawayi.CommandLine.Abstractions.Cli scope)");
        GeneratorSource.AppendIndentedLine(builder, indentLevel, "{");
        GeneratorSource.AppendIndentedLine(builder, indentLevel + 1, "foreach (var definition in scope.Schema.Argument)");
        GeneratorSource.AppendIndentedLine(builder, indentLevel + 1, "{");
        GeneratorSource.AppendIndentedLine(builder, indentLevel + 2, "yield return definition;");
        GeneratorSource.AppendIndentedLine(builder, indentLevel + 1, "}");
        GeneratorSource.AppendIndentedLine(builder, indentLevel + 1, "foreach (var definition in global::System.Linq.Enumerable.Distinct(scope.Schema.Properties.Values))");
        GeneratorSource.AppendIndentedLine(builder, indentLevel + 1, "{");
        GeneratorSource.AppendIndentedLine(builder, indentLevel + 2, "yield return definition;");
        GeneratorSource.AppendIndentedLine(builder, indentLevel + 1, "}");
        GeneratorSource.AppendIndentedLine(builder, indentLevel, "}");

        GeneratorSource.AppendIndentedLine(builder, indentLevel, string.Empty);
        GeneratorSource.AppendIndentedLine(
            builder,
            indentLevel,
            "private static bool TryGetValue(global::Kawayi.CommandLine.Abstractions.Cli scope, global::Kawayi.CommandLine.Abstractions.TypedDefinition definition, out object? value)");
        GeneratorSource.AppendIndentedLine(builder, indentLevel, "{");
        GeneratorSource.AppendIndentedLine(builder, indentLevel + 1, "switch (definition)");
        GeneratorSource.AppendIndentedLine(builder, indentLevel + 1, "{");
        GeneratorSource.AppendIndentedLine(builder, indentLevel + 2, "case global::Kawayi.CommandLine.Abstractions.ParameterDefinition argument:");
        GeneratorSource.AppendIndentedLine(builder, indentLevel + 3, "return scope.Arguments.TryGetValue(argument, out value);");
        GeneratorSource.AppendIndentedLine(builder, indentLevel + 2, "case global::Kawayi.CommandLine.Abstractions.PropertyDefinition property:");
        GeneratorSource.AppendIndentedLine(builder, indentLevel + 3, "return scope.Properties.TryGetValue(property, out value);");
        GeneratorSource.AppendIndentedLine(builder, indentLevel + 2, "default:");
        GeneratorSource.AppendIndentedLine(builder, indentLevel + 3, "value = null;");
        GeneratorSource.AppendIndentedLine(builder, indentLevel + 3, "return false;");
        GeneratorSource.AppendIndentedLine(builder, indentLevel + 1, "}");
        GeneratorSource.AppendIndentedLine(builder, indentLevel, "}");

        GeneratorSource.AppendIndentedLine(builder, indentLevel, string.Empty);
        GeneratorSource.AppendIndentedLine(
            builder,
            indentLevel,
            "private static void EnsureEffectiveValueSatisfiesRequirement(global::Kawayi.CommandLine.Abstractions.TypedDefinition definition, object? value, string memberName)");
        GeneratorSource.AppendIndentedLine(builder, indentLevel, "{");
        GeneratorSource.AppendIndentedLine(builder, indentLevel + 1, "if (!definition.Requirement && definition.RequirementIfNull && value is null)");
        GeneratorSource.AppendIndentedLine(builder, indentLevel + 1, "{");
        GeneratorSource.AppendIndentedLine(
            builder,
            indentLevel + 2,
            "throw new global::System.InvalidOperationException(\"Required member '\" + memberName + \"' does not have an explicit value or default factory.\");");
        GeneratorSource.AppendIndentedLine(builder, indentLevel + 1, "}");
        GeneratorSource.AppendIndentedLine(builder, indentLevel, "}");
    }

    private sealed class BindingTarget
    {
        public BindingTarget(CommandModel model, ImmutableArray<MemberModel> members, ImmutableArray<DiagnosticInfo> diagnostics)
        {
            Model = model;
            Members = members;
            Diagnostics = diagnostics;
        }

        public CommandModel Model { get; }

        public ImmutableArray<MemberModel> Members { get; }

        public ImmutableArray<DiagnosticInfo> Diagnostics { get; }
    }
}
