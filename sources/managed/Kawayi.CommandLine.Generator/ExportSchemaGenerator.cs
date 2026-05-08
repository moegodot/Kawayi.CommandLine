// Copyright (c) 2026 MoeGodot<me@kawayi.moe>.
// Licensed under the GNU Affero General Public License v3-or-later license.

using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace Kawayi.CommandLine.Generator;

/// <summary>
/// Generates <c>ICliSchemaExporter</c> implementations for
/// types annotated with <c>ExportParsingAttribute</c> or <c>CommandAttribute</c>.
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class ExportSchemaGenerator : IIncrementalGenerator
{
    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var explicitTargets = context.SyntaxProvider.ForAttributeWithMetadataName(
            MetadataNames.ExportParsingAttribute,
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

    private static ParsingTarget? CreateTarget(
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
        var subcommands = ImmutableArray.CreateBuilder<MemberModel>();
        string? baseSchemaExporterTypeName = null;

        foreach (var member in model.Members.Where(static item => item.Kind == MemberKind.Subcommand))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!SupportsCliSchemaExporter(member.PropertySymbol.Type, cancellationToken, new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default)))
            {
                diagnostics.Add(new DiagnosticInfo(GeneratorDescriptors.InvalidSubcommandExporter, member.Location, [member.MemberName]));
                continue;
            }

            subcommands.Add(member);
        }

        var baseType = model.TypeSymbol.BaseType;
        if (baseType is not null &&
            baseType.SpecialType != SpecialType.System_Object &&
            SupportsCliSchemaExporter(baseType, cancellationToken, new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default)))
        {
            baseSchemaExporterTypeName = baseType.ToDisplayString(GeneratorFormats.FullyQualifiedType);
        }

        return new ParsingTarget(model, subcommands.ToImmutable(), diagnostics.ToImmutable(), baseSchemaExporterTypeName);
    }

    private static bool SupportsCliSchemaExporter(
        ITypeSymbol typeSymbol,
        CancellationToken cancellationToken,
        HashSet<ITypeSymbol> visited)
    {
        if (typeSymbol is not INamedTypeSymbol namedType)
        {
            return false;
        }

        if (CommandModel.ImplementsInterface(namedType, MetadataNames.CliSchemaExporter))
        {
            return true;
        }

        if (!CommandModel.HasAttribute(namedType, MetadataNames.ExportParsingAttribute) &&
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

        foreach (var subcommand in model.Members.Where(static item => item.Kind == MemberKind.Subcommand))
        {
            if (!SupportsCliSchemaExporter(subcommand.PropertySymbol.Type, cancellationToken, visited))
            {
                return false;
            }
        }

        return true;
    }

    private static void Emit(SourceProductionContext context, ParsingTarget target)
    {
        var model = target.Model;
        if (model.MissingPartialSymbol is not null)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                GeneratorDescriptors.ParsingNonPartial,
                model.MissingPartialSymbol.Locations.FirstOrDefault(),
                model.MissingPartialSymbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)));
            return;
        }

        if (!model.HasSymbolProvider)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                GeneratorDescriptors.MissingSymbolExporterForParsing,
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
            GeneratorSource.GetHintName(model.TypeSymbol, "ExportSchema"),
            SourceText.From(GenerateSource(target), Encoding.UTF8));
    }

    private static string GenerateSource(ParsingTarget target)
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

        var interfaces = ImmutableArray.CreateBuilder<string>(1);
        if (!model.ImplementsCliSchemaExporter)
        {
            interfaces.Add("global::Kawayi.CommandLine.Abstractions.ICliSchemaExporter");
        }

        GeneratorSource.AppendIndentedLine(
            builder,
            containingTypes.Length,
            GeneratorSource.BuildTypeDeclaration(model.TypeSymbol, interfaces.Count == 0 ? null : string.Join(", ", interfaces), includeInterface: true));
        GeneratorSource.AppendIndentedLine(builder, containingTypes.Length, "{");
        AppendExportSchemaMethod(builder, target, containingTypes.Length + 1);
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

    private static void AppendExportSchemaMethod(StringBuilder builder, ParsingTarget target, int indentLevel)
    {
        var model = target.Model;
        var typeNameLiteral = SymbolDisplay.FormatLiteral(
            model.TypeSymbol.ToDisplayString(GeneratorFormats.FullyQualifiedNullableType),
            true);
        var hasPromotedGlobalSubcommands = target.Subcommands.Any(static item => item.IsGlobalSubcommand);
        var hasRegularSubcommands = target.Subcommands.Any(static item => !item.IsGlobalSubcommand);
        var hasInheritedSchema = target.BaseSchemaExporterTypeName is not null;

        GeneratorSource.AppendIndentedLine(builder, indentLevel, "/// <summary>");
        GeneratorSource.AppendIndentedLine(builder, indentLevel, "/// Exports an immutable parsing schema for this command type.");
        GeneratorSource.AppendIndentedLine(builder, indentLevel, "/// </summary>");
        GeneratorSource.AppendIndentedLine(builder, indentLevel, "/// <param name=\"parsingOptions\">The parsing options used while exporting the schema.</param>");
        GeneratorSource.AppendIndentedLine(builder, indentLevel, "/// <returns>The exported parsing schema.</returns>");
        GeneratorSource.AppendIndentedLine(
            builder,
            indentLevel,
            "public static global::Kawayi.CommandLine.Abstractions.CliSchema ExportSchema(global::Kawayi.CommandLine.Abstractions.ParsingOptions parsingOptions)");
        GeneratorSource.AppendIndentedLine(builder, indentLevel, "{");
        GeneratorSource.AppendIndentedLine(builder, indentLevel + 1, "global::System.ArgumentNullException.ThrowIfNull(parsingOptions);");
        GeneratorSource.AppendIndentedLine(
            builder,
            indentLevel + 1,
            "var arguments = global::System.Collections.Immutable.ImmutableList.CreateBuilder<global::Kawayi.CommandLine.Abstractions.ParameterDefinition>();");
        GeneratorSource.AppendIndentedLine(
            builder,
            indentLevel + 1,
            "var propertyDefinitions = global::System.Collections.Immutable.ImmutableDictionary.CreateBuilder<string, global::Kawayi.CommandLine.Abstractions.PropertyDefinition>();");
        GeneratorSource.AppendIndentedLine(
            builder,
            indentLevel + 1,
            "var subcommandDefinitionsByName = global::System.Collections.Immutable.ImmutableDictionary.CreateBuilder<string, global::Kawayi.CommandLine.Abstractions.CommandDefinition>();");
        GeneratorSource.AppendIndentedLine(builder, indentLevel + 1, "foreach (var symbol in Symbols)");
        GeneratorSource.AppendIndentedLine(builder, indentLevel + 1, "{");
        GeneratorSource.AppendIndentedLine(builder, indentLevel + 2, "switch (symbol)");
        GeneratorSource.AppendIndentedLine(builder, indentLevel + 2, "{");
        GeneratorSource.AppendIndentedLine(builder, indentLevel + 3, "case global::Kawayi.CommandLine.Abstractions.ParameterDefinition argument:");
        GeneratorSource.AppendIndentedLine(builder, indentLevel + 4, "arguments.Add(argument);");
        GeneratorSource.AppendIndentedLine(builder, indentLevel + 4, "break;");
        GeneratorSource.AppendIndentedLine(builder, indentLevel + 3, "case global::Kawayi.CommandLine.Abstractions.PropertyDefinition property:");
        GeneratorSource.AppendIndentedLine(builder, indentLevel + 4, "propertyDefinitions[property.Information.Name.Value] = property;");
        GeneratorSource.AppendIndentedLine(builder, indentLevel + 4, "break;");
        GeneratorSource.AppendIndentedLine(builder, indentLevel + 3, "case global::Kawayi.CommandLine.Abstractions.CommandDefinition command:");
        if (hasPromotedGlobalSubcommands)
        {
            GeneratorSource.AppendIndentedLine(builder, indentLevel + 4, "if (!IsPromotedGlobalSubcommandName(command.Information.Name.Value))");
            GeneratorSource.AppendIndentedLine(builder, indentLevel + 4, "{");
            GeneratorSource.AppendIndentedLine(builder, indentLevel + 5, "subcommandDefinitionsByName[command.Information.Name.Value] = command;");
            GeneratorSource.AppendIndentedLine(builder, indentLevel + 4, "}");
        }
        else
        {
            GeneratorSource.AppendIndentedLine(builder, indentLevel + 4, "subcommandDefinitionsByName[command.Information.Name.Value] = command;");
        }

        GeneratorSource.AppendIndentedLine(builder, indentLevel + 4, "break;");
        GeneratorSource.AppendIndentedLine(builder, indentLevel + 3, "default:");
        GeneratorSource.AppendIndentedLine(
            builder,
            indentLevel + 4,
            $"throw new global::System.InvalidOperationException(\"Unsupported symbol type '\" + symbol.GetType().FullName + \"' was found in Symbols while exporting schema metadata for \" + {typeNameLiteral} + \".\");");
        GeneratorSource.AppendIndentedLine(builder, indentLevel + 2, "}");
        GeneratorSource.AppendIndentedLine(builder, indentLevel + 1, "}");

        GeneratorSource.AppendIndentedLine(
            builder,
            indentLevel + 1,
            "var subcommandDefinitions = global::System.Collections.Immutable.ImmutableDictionary.CreateBuilder<global::Kawayi.CommandLine.Abstractions.ArgumentOrCommandToken, global::Kawayi.CommandLine.Abstractions.CommandDefinition>();");
        GeneratorSource.AppendIndentedLine(builder, indentLevel + 1, "foreach (var command in subcommandDefinitionsByName.Values)");
        GeneratorSource.AppendIndentedLine(builder, indentLevel + 1, "{");
        GeneratorSource.AppendIndentedLine(builder, indentLevel + 2, "AddSubcommandTokens(subcommandDefinitions, command);");
        GeneratorSource.AppendIndentedLine(builder, indentLevel + 1, "}");

        GeneratorSource.AppendIndentedLine(
            builder,
            indentLevel + 1,
            "var properties = global::System.Collections.Immutable.ImmutableDictionary.CreateBuilder<global::Kawayi.CommandLine.Abstractions.OptionToken, global::Kawayi.CommandLine.Abstractions.PropertyDefinition>();");
        GeneratorSource.AppendIndentedLine(builder, indentLevel + 1, "foreach (var property in propertyDefinitions.Values)");
        GeneratorSource.AppendIndentedLine(builder, indentLevel + 1, "{");
        GeneratorSource.AppendIndentedLine(builder, indentLevel + 2, "AddPropertyTokens(properties, property);");
        GeneratorSource.AppendIndentedLine(builder, indentLevel + 1, "}");

        GeneratorSource.AppendIndentedLine(
            builder,
            indentLevel + 1,
            "var subcommands = global::System.Collections.Immutable.ImmutableDictionary.CreateBuilder<global::Kawayi.CommandLine.Abstractions.ArgumentOrCommandToken, global::Kawayi.CommandLine.Abstractions.CliSchema>();");

        foreach (var subcommand in target.Subcommands.Where(static item => !item.IsGlobalSubcommand))
        {
            GeneratorSource.AppendIndentedLine(builder, indentLevel + 1, "{");
            GeneratorSource.AppendIndentedLine(builder, indentLevel + 2, $"var childCommandName = {subcommand.CommandLineName};");
            GeneratorSource.AppendIndentedLine(
                builder,
                indentLevel + 2,
                "if (!subcommandDefinitionsByName.TryGetValue(childCommandName, out var childDefinition) || childDefinition is null)");
            GeneratorSource.AppendIndentedLine(builder, indentLevel + 2, "{");
            GeneratorSource.AppendIndentedLine(
                builder,
                indentLevel + 3,
                $"throw new global::System.InvalidOperationException(\"Expected Symbols for \" + {typeNameLiteral} + \" to contain a CommandDefinition named '\" + childCommandName + \"' so the generated schema exporter can attach the child schema.\");");
            GeneratorSource.AppendIndentedLine(builder, indentLevel + 2, "}");
            GeneratorSource.AppendIndentedLine(
                builder,
                indentLevel + 2,
                $"subcommands[new global::Kawayi.CommandLine.Abstractions.ArgumentOrCommandToken(childDefinition.Information.Name.Value)] = {subcommand.TypeName}.ExportSchema(parsingOptions);");
            GeneratorSource.AppendIndentedLine(builder, indentLevel + 1, "}");
        }

        GeneratorSource.AppendIndentedLine(
            builder,
            indentLevel + 1,
            $"var schema = new global::Kawayi.CommandLine.Abstractions.CliSchema(typeof({model.TypeSymbol.ToDisplayString(GeneratorFormats.FullyQualifiedType)}), subcommandDefinitions.ToImmutable(), subcommands.ToImmutable(), properties.ToImmutable(), arguments.ToImmutable());");

        foreach (var subcommand in target.Subcommands.Where(static item => item.IsGlobalSubcommand))
        {
            GeneratorSource.AppendIndentedLine(
                builder,
                indentLevel + 1,
                $"schema = MergePromotedGlobalSubcommand(schema, {subcommand.TypeName}.ExportSchema(parsingOptions), {SymbolDisplay.FormatLiteral(subcommand.TypeName, true)}, {SymbolDisplay.FormatLiteral(subcommand.MemberName, true)});");
        }

        if (hasInheritedSchema)
        {
            GeneratorSource.AppendIndentedLine(
                builder,
                indentLevel + 1,
                $"return MergeInheritedSchema({target.BaseSchemaExporterTypeName}.ExportSchema(parsingOptions), schema, {typeNameLiteral}, {SymbolDisplay.FormatLiteral(target.BaseSchemaExporterTypeName!, true)});");
        }
        else
        {
            GeneratorSource.AppendIndentedLine(builder, indentLevel + 1, "return schema;");
        }

        GeneratorSource.AppendIndentedLine(builder, indentLevel, "}");
        AppendSchemaHelpers(builder, indentLevel, target, typeNameLiteral, hasPromotedGlobalSubcommands, hasInheritedSchema);
    }

    private static void AppendSchemaHelpers(StringBuilder builder,
                                            int indentLevel,
                                            ParsingTarget target,
                                            string typeNameLiteral,
                                            bool hasPromotedGlobalSubcommands,
                                            bool hasInheritedSchema)
    {
        GeneratorSource.AppendIndentedLine(builder, indentLevel, string.Empty);
        GeneratorSource.AppendIndentedLine(
            builder,
            indentLevel,
            "private static void AddPropertyTokens(global::System.Collections.Immutable.ImmutableDictionary<global::Kawayi.CommandLine.Abstractions.OptionToken, global::Kawayi.CommandLine.Abstractions.PropertyDefinition>.Builder builder, global::Kawayi.CommandLine.Abstractions.PropertyDefinition property)");
        GeneratorSource.AppendIndentedLine(builder, indentLevel, "{");
        GeneratorSource.AppendIndentedLine(builder, indentLevel + 1, "AddUniqueDefinition(builder, new global::Kawayi.CommandLine.Abstractions.LongOptionToken(property.Information.Name.Value), property);");
        GeneratorSource.AppendIndentedLine(builder, indentLevel + 1, "foreach (var alias in property.LongName.Values)");
        GeneratorSource.AppendIndentedLine(builder, indentLevel + 1, "{");
        GeneratorSource.AppendIndentedLine(builder, indentLevel + 2, "AddUniqueDefinition(builder, new global::Kawayi.CommandLine.Abstractions.LongOptionToken(alias.Value), property);");
        GeneratorSource.AppendIndentedLine(builder, indentLevel + 1, "}");
        GeneratorSource.AppendIndentedLine(builder, indentLevel + 1, "foreach (var alias in property.ShortName.Values)");
        GeneratorSource.AppendIndentedLine(builder, indentLevel + 1, "{");
        GeneratorSource.AppendIndentedLine(builder, indentLevel + 2, "AddUniqueDefinition(builder, new global::Kawayi.CommandLine.Abstractions.ShortOptionToken(alias.Value), property);");
        GeneratorSource.AppendIndentedLine(builder, indentLevel + 1, "}");
        GeneratorSource.AppendIndentedLine(builder, indentLevel, "}");

        GeneratorSource.AppendIndentedLine(builder, indentLevel, string.Empty);
        GeneratorSource.AppendIndentedLine(
            builder,
            indentLevel,
            "private static void AddSubcommandTokens(global::System.Collections.Immutable.ImmutableDictionary<global::Kawayi.CommandLine.Abstractions.ArgumentOrCommandToken, global::Kawayi.CommandLine.Abstractions.CommandDefinition>.Builder builder, global::Kawayi.CommandLine.Abstractions.CommandDefinition command)");
        GeneratorSource.AppendIndentedLine(builder, indentLevel, "{");
        GeneratorSource.AppendIndentedLine(builder, indentLevel + 1, "AddUniqueDefinition(builder, new global::Kawayi.CommandLine.Abstractions.ArgumentOrCommandToken(command.Information.Name.Value), command);");
        GeneratorSource.AppendIndentedLine(builder, indentLevel + 1, "foreach (var alias in command.Alias.Values)");
        GeneratorSource.AppendIndentedLine(builder, indentLevel + 1, "{");
        GeneratorSource.AppendIndentedLine(builder, indentLevel + 2, "AddUniqueDefinition(builder, new global::Kawayi.CommandLine.Abstractions.ArgumentOrCommandToken(alias.Value), command);");
        GeneratorSource.AppendIndentedLine(builder, indentLevel + 1, "}");
        GeneratorSource.AppendIndentedLine(builder, indentLevel, "}");

        GeneratorSource.AppendIndentedLine(builder, indentLevel, string.Empty);
        GeneratorSource.AppendIndentedLine(
            builder,
            indentLevel,
            "private static void AddUniqueDefinition<TKey, TValue>(global::System.Collections.Immutable.ImmutableDictionary<TKey, TValue>.Builder builder, TKey key, TValue value)");
        GeneratorSource.AppendIndentedLine(builder, indentLevel + 1, "where TKey : notnull");
        GeneratorSource.AppendIndentedLine(builder, indentLevel + 1, "where TValue : class");
        GeneratorSource.AppendIndentedLine(builder, indentLevel, "{");
        GeneratorSource.AppendIndentedLine(builder, indentLevel + 1, "if (builder.TryGetValue(key, out var existingValue))");
        GeneratorSource.AppendIndentedLine(builder, indentLevel + 1, "{");
        GeneratorSource.AppendIndentedLine(builder, indentLevel + 2, "if (!global::System.Object.ReferenceEquals(existingValue, value))");
        GeneratorSource.AppendIndentedLine(builder, indentLevel + 2, "{");
        GeneratorSource.AppendIndentedLine(builder, indentLevel + 3, "throw new global::System.InvalidOperationException($\"Token '{key}' is mapped to more than one definition.\");");
        GeneratorSource.AppendIndentedLine(builder, indentLevel + 2, "}");
        GeneratorSource.AppendIndentedLine(builder, indentLevel + 2, "return;");
        GeneratorSource.AppendIndentedLine(builder, indentLevel + 1, "}");
        GeneratorSource.AppendIndentedLine(builder, indentLevel + 1, "builder[key] = value;");
        GeneratorSource.AppendIndentedLine(builder, indentLevel, "}");

        if (hasPromotedGlobalSubcommands)
        {
            GeneratorSource.AppendIndentedLine(builder, indentLevel, string.Empty);
            GeneratorSource.AppendIndentedLine(builder, indentLevel, "private static bool IsPromotedGlobalSubcommandName(string commandName)");
            GeneratorSource.AppendIndentedLine(builder, indentLevel, "{");
            GeneratorSource.AppendIndentedLine(builder, indentLevel + 1, "return");
            var globalSubcommands = target.Subcommands.Where(static item => item.IsGlobalSubcommand).ToArray();
            for (var i = 0; i < globalSubcommands.Length; i++)
            {
                var suffix = i == globalSubcommands.Length - 1 ? ";" : " ||";
                GeneratorSource.AppendIndentedLine(
                    builder,
                    indentLevel + 2,
                    $"global::System.StringComparer.Ordinal.Equals(commandName, {globalSubcommands[i].CommandLineName}){suffix}");
            }

            GeneratorSource.AppendIndentedLine(builder, indentLevel, "}");
            GeneratorSource.AppendIndentedLine(builder, indentLevel, string.Empty);
            GeneratorSource.AppendIndentedLine(
                builder,
                indentLevel,
                "private static global::Kawayi.CommandLine.Abstractions.CliSchema MergePromotedGlobalSubcommand(global::Kawayi.CommandLine.Abstractions.CliSchema schema, global::Kawayi.CommandLine.Abstractions.CliSchema childSchema, string childTypeName, string memberName)");
            GeneratorSource.AppendIndentedLine(builder, indentLevel, "{");
            GeneratorSource.AppendIndentedLine(builder, indentLevel + 1, "try");
            GeneratorSource.AppendIndentedLine(builder, indentLevel + 1, "{");
            GeneratorSource.AppendIndentedLine(
                builder,
                indentLevel + 2,
                "var merged = global::Kawayi.CommandLine.Extensions.CliSchemaExtensions.Merge(schema, childSchema, allowOverride: false);");
            GeneratorSource.AppendIndentedLine(builder, indentLevel + 2, "return merged with { GeneratedFrom = schema.GeneratedFrom };");
            GeneratorSource.AppendIndentedLine(builder, indentLevel + 1, "}");
            GeneratorSource.AppendIndentedLine(builder, indentLevel + 1, "catch (global::System.InvalidOperationException exception)");
            GeneratorSource.AppendIndentedLine(builder, indentLevel + 1, "{");
            GeneratorSource.AppendIndentedLine(
                builder,
                indentLevel + 2,
                "throw new global::System.InvalidOperationException(\"Global subcommand member '\" + memberName + \"' from '\" + childTypeName + \"' cannot be promoted: \" + exception.Message, exception);");
            GeneratorSource.AppendIndentedLine(builder, indentLevel + 1, "}");
            GeneratorSource.AppendIndentedLine(builder, indentLevel, "}");
        }

        if (!hasInheritedSchema)
        {
            return;
        }

        GeneratorSource.AppendIndentedLine(builder, indentLevel, string.Empty);
        GeneratorSource.AppendIndentedLine(
            builder,
            indentLevel,
            "private static global::Kawayi.CommandLine.Abstractions.CliSchema MergeInheritedSchema(global::Kawayi.CommandLine.Abstractions.CliSchema baseSchema, global::Kawayi.CommandLine.Abstractions.CliSchema ownSchema, string ownTypeName, string baseTypeName)");
        GeneratorSource.AppendIndentedLine(builder, indentLevel, "{");
        GeneratorSource.AppendIndentedLine(builder, indentLevel + 1, "try");
        GeneratorSource.AppendIndentedLine(builder, indentLevel + 1, "{");
        GeneratorSource.AppendIndentedLine(
            builder,
            indentLevel + 2,
            "var merged = global::Kawayi.CommandLine.Extensions.CliSchemaExtensions.Merge(baseSchema, ownSchema, allowOverride: false);");
        GeneratorSource.AppendIndentedLine(builder, indentLevel + 2, "return merged with { GeneratedFrom = ownSchema.GeneratedFrom ?? baseSchema.GeneratedFrom };");
        GeneratorSource.AppendIndentedLine(builder, indentLevel + 1, "}");
        GeneratorSource.AppendIndentedLine(builder, indentLevel + 1, "catch (global::System.InvalidOperationException exception)");
        GeneratorSource.AppendIndentedLine(builder, indentLevel + 1, "{");
        GeneratorSource.AppendIndentedLine(
            builder,
            indentLevel + 2,
            "throw new global::System.InvalidOperationException(\"Inherited command schema conflict while exporting '\" + ownTypeName + \"' against '\" + baseTypeName + \"': \" + exception.Message, exception);");
        GeneratorSource.AppendIndentedLine(builder, indentLevel + 1, "}");
        GeneratorSource.AppendIndentedLine(builder, indentLevel, "}");
    }

    private sealed class ParsingTarget
    {
        public ParsingTarget(CommandModel model, ImmutableArray<MemberModel> subcommands, ImmutableArray<DiagnosticInfo> diagnostics, string? baseSchemaExporterTypeName)
        {
            Model = model;
            Subcommands = subcommands;
            Diagnostics = diagnostics;
            BaseSchemaExporterTypeName = baseSchemaExporterTypeName;
        }

        public CommandModel Model { get; }

        public ImmutableArray<MemberModel> Subcommands { get; }

        public ImmutableArray<DiagnosticInfo> Diagnostics { get; }

        public string? BaseSchemaExporterTypeName { get; }
    }
}
