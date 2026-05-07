// Copyright (c) 2026 MoeGodot<me@kawayi.moe>.
// Licensed under the GNU Affero General Public License v3-or-later license.

using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace Kawayi.CommandLine.Generator;

/// <summary>
/// Generates <c>ICliSchemaExporter</c> and <c>IParsable&lt;T&gt;</c> implementations for
/// types annotated with <c>ExportParsingAttribute</c> or <c>CommandAttribute</c>.
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class ExportParsingGenerator : IIncrementalGenerator
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

        return new ParsingTarget(model, subcommands.ToImmutable(), diagnostics.ToImmutable());
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
            GeneratorSource.GetHintName(model.TypeSymbol, "ExportParsing"),
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

        var interfaces = ImmutableArray.CreateBuilder<string>(2);
        if (!model.ImplementsCliSchemaExporter)
        {
            interfaces.Add("global::Kawayi.CommandLine.Abstractions.ICliSchemaExporter");
        }

        if (!model.HasParsableSelfInterface)
        {
            interfaces.Add($"global::Kawayi.CommandLine.Abstractions.IParsable<{GeneratorSource.BuildSelfTypeReference(model.TypeSymbol)}>");
        }

        GeneratorSource.AppendIndentedLine(
            builder,
            containingTypes.Length,
            GeneratorSource.BuildTypeDeclaration(model.TypeSymbol, interfaces.Count == 0 ? null : string.Join(", ", interfaces), includeInterface: true));
        GeneratorSource.AppendIndentedLine(builder, containingTypes.Length, "{");
        AppendExportParsingMethod(builder, target, containingTypes.Length + 1);
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

    private static void AppendExportParsingMethod(StringBuilder builder, ParsingTarget target, int indentLevel)
    {
        var model = target.Model;
        var typeNameLiteral = SymbolDisplay.FormatLiteral(
            model.TypeSymbol.ToDisplayString(GeneratorFormats.FullyQualifiedNullableType),
            true);
        var selfTypeReference = GeneratorSource.BuildSelfTypeReference(model.TypeSymbol);
        var hasPromotedGlobalSubcommands = target.Subcommands.Any(static item => item.IsGlobalSubcommand);
        var hasRegularSubcommands = target.Subcommands.Any(static item => !item.IsGlobalSubcommand);

        GeneratorSource.AppendIndentedLine(builder, indentLevel, "/// <summary>");
        GeneratorSource.AppendIndentedLine(builder, indentLevel, "/// Exports a mutable parsing builder for this command type.");
        GeneratorSource.AppendIndentedLine(builder, indentLevel, "/// </summary>");
        GeneratorSource.AppendIndentedLine(builder, indentLevel, "/// <param name=\"parsingOptions\">The parsing options to attach to the exported builder.</param>");
        GeneratorSource.AppendIndentedLine(builder, indentLevel, "/// <returns>The exported parsing builder.</returns>");
        GeneratorSource.AppendIndentedLine(
            builder,
            indentLevel,
            "public static global::Kawayi.CommandLine.Abstractions.CliSchemaBuilder ExportParsing(global::Kawayi.CommandLine.Abstractions.ParsingOptions parsingOptions)");
        GeneratorSource.AppendIndentedLine(builder, indentLevel, "{");
        GeneratorSource.AppendIndentedLine(builder, indentLevel + 1, "global::System.ArgumentNullException.ThrowIfNull(parsingOptions);");
        GeneratorSource.AppendIndentedLine(
            builder,
            indentLevel + 1,
            "var builder = new global::Kawayi.CommandLine.Abstractions.CliSchemaBuilder(global::System.Collections.Immutable.ImmutableDictionary.CreateBuilder<string, global::Kawayi.CommandLine.Abstractions.CommandDefinition>(), global::System.Collections.Immutable.ImmutableDictionary.CreateBuilder<string, global::Kawayi.CommandLine.Abstractions.CliSchemaBuilder>(), global::System.Collections.Immutable.ImmutableDictionary.CreateBuilder<string, global::Kawayi.CommandLine.Abstractions.PropertyDefinition>(), global::System.Collections.Immutable.ImmutableList.CreateBuilder<global::Kawayi.CommandLine.Abstractions.ParameterDefinition>());");
        GeneratorSource.AppendIndentedLine(builder, indentLevel + 1, "foreach (var symbol in Symbols)");
        GeneratorSource.AppendIndentedLine(builder, indentLevel + 1, "{");
        GeneratorSource.AppendIndentedLine(builder, indentLevel + 2, "switch (symbol)");
        GeneratorSource.AppendIndentedLine(builder, indentLevel + 2, "{");
        GeneratorSource.AppendIndentedLine(builder, indentLevel + 3, "case global::Kawayi.CommandLine.Abstractions.ParameterDefinition argument:");
        GeneratorSource.AppendIndentedLine(builder, indentLevel + 4, "builder.Argument.Add(argument);");
        GeneratorSource.AppendIndentedLine(builder, indentLevel + 4, "break;");
        GeneratorSource.AppendIndentedLine(builder, indentLevel + 3, "case global::Kawayi.CommandLine.Abstractions.PropertyDefinition property:");
        GeneratorSource.AppendIndentedLine(builder, indentLevel + 4, "builder.Properties[property.Information.Name.Value] = property;");
        GeneratorSource.AppendIndentedLine(builder, indentLevel + 4, "break;");
        GeneratorSource.AppendIndentedLine(builder, indentLevel + 3, "case global::Kawayi.CommandLine.Abstractions.CommandDefinition command:");
        if (hasPromotedGlobalSubcommands)
        {
            GeneratorSource.AppendIndentedLine(builder, indentLevel + 4, "if (!IsPromotedGlobalSubcommandName(command.Information.Name.Value))");
            GeneratorSource.AppendIndentedLine(builder, indentLevel + 4, "{");
            GeneratorSource.AppendIndentedLine(builder, indentLevel + 5, "builder.SubcommandDefinitions[command.Information.Name.Value] = command;");
            GeneratorSource.AppendIndentedLine(builder, indentLevel + 4, "}");
        }
        else
        {
            GeneratorSource.AppendIndentedLine(builder, indentLevel + 4, "builder.SubcommandDefinitions[command.Information.Name.Value] = command;");
        }

        GeneratorSource.AppendIndentedLine(builder, indentLevel + 4, "break;");
        GeneratorSource.AppendIndentedLine(builder, indentLevel + 3, "default:");
        GeneratorSource.AppendIndentedLine(
            builder,
            indentLevel + 4,
            $"throw new global::System.InvalidOperationException(\"Unsupported symbol type '\" + symbol.GetType().FullName + \"' was found in Symbols while exporting parsing metadata for \" + {typeNameLiteral} + \".\");");
        GeneratorSource.AppendIndentedLine(builder, indentLevel + 2, "}");
        GeneratorSource.AppendIndentedLine(builder, indentLevel + 1, "}");

        foreach (var subcommand in target.Subcommands)
        {
            if (subcommand.IsGlobalSubcommand)
            {
                GeneratorSource.AppendIndentedLine(
                    builder,
                    indentLevel + 1,
                    $"MergeGlobalSubcommand(builder, {subcommand.TypeName}.ExportParsing(parsingOptions), {SymbolDisplay.FormatLiteral(subcommand.TypeName, true)}, {SymbolDisplay.FormatLiteral(subcommand.MemberName, true)});");
            }
            else
            {
                GeneratorSource.AppendIndentedLine(
                    builder,
                    indentLevel + 1,
                    $"builder.Subcommands[GetRequiredSubcommandKey(builder, {subcommand.CommandLineName})] = {subcommand.TypeName}.ExportParsing(parsingOptions);");
            }
        }

        GeneratorSource.AppendIndentedLine(builder, indentLevel + 1, "return builder;");
        GeneratorSource.AppendIndentedLine(builder, indentLevel, "}");
        GeneratorSource.AppendIndentedLine(builder, indentLevel, string.Empty);
        GeneratorSource.AppendIndentedLine(builder, indentLevel, "/// <summary>");
        GeneratorSource.AppendIndentedLine(builder, indentLevel, "/// Parses the supplied tokens by using the generated schema for this command type.");
        GeneratorSource.AppendIndentedLine(builder, indentLevel, "/// </summary>");
        GeneratorSource.AppendIndentedLine(builder, indentLevel, "/// <param name=\"options\">The parsing options for this operation.</param>");
        GeneratorSource.AppendIndentedLine(builder, indentLevel, "/// <param name=\"arguments\">The tokens to parse.</param>");
        GeneratorSource.AppendIndentedLine(builder, indentLevel, "/// <param name=\"initialState\">The initial state supplied to satisfy the IParsable contract.</param>");
        GeneratorSource.AppendIndentedLine(builder, indentLevel, "/// <returns>The parsing result.</returns>");
        GeneratorSource.AppendIndentedLine(
            builder,
            indentLevel,
            $"public static global::Kawayi.CommandLine.Abstractions.ParsingResult CreateParsing(global::Kawayi.CommandLine.Abstractions.ParsingOptions options, global::System.Collections.Immutable.ImmutableArray<global::Kawayi.CommandLine.Abstractions.Token> arguments, {selfTypeReference} initialState)");
        GeneratorSource.AppendIndentedLine(builder, indentLevel, "{");
        GeneratorSource.AppendIndentedLine(builder, indentLevel + 1, "global::System.ArgumentNullException.ThrowIfNull(options);");
        GeneratorSource.AppendIndentedLine(builder, indentLevel + 1, "_ = initialState;");
        GeneratorSource.AppendIndentedLine(builder, indentLevel + 1, "var builder = ExportParsing(options);");
        GeneratorSource.AppendIndentedLine(builder, indentLevel + 1, "var snapshot = builder.Build();");
        GeneratorSource.AppendIndentedLine(builder,
                           indentLevel + 1,
                           "return global::Kawayi.CommandLine.Core.CliSchemaParser.CreateParsing(options, arguments, snapshot);");
        GeneratorSource.AppendIndentedLine(builder, indentLevel, "}");

        if (target.Subcommands.Length == 0)
        {
            return;
        }

        GeneratorSource.AppendIndentedLine(builder, indentLevel, string.Empty);
        if (hasRegularSubcommands)
        {
            GeneratorSource.AppendIndentedLine(
                builder,
                indentLevel,
                "private static string GetRequiredSubcommandKey(global::Kawayi.CommandLine.Abstractions.CliSchemaBuilder builder, string commandName)");
            GeneratorSource.AppendIndentedLine(builder, indentLevel, "{");
            GeneratorSource.AppendIndentedLine(
                builder,
                indentLevel + 1,
                "if (builder.SubcommandDefinitions.TryGetValue(commandName, out var definition) && definition is not null)");
            GeneratorSource.AppendIndentedLine(builder, indentLevel + 1, "{");
            GeneratorSource.AppendIndentedLine(builder, indentLevel + 2, "return definition.Information.Name.Value;");
            GeneratorSource.AppendIndentedLine(builder, indentLevel + 1, "}");
            GeneratorSource.AppendIndentedLine(
                builder,
                indentLevel + 1,
                $"throw new global::System.InvalidOperationException(\"Expected Symbols for \" + {typeNameLiteral} + \" to contain a CommandDefinition named '\" + commandName + \"' so the generated schema exporter can attach the child parser.\");");
            GeneratorSource.AppendIndentedLine(builder, indentLevel, "}");
        }

        if (!hasPromotedGlobalSubcommands)
        {
            return;
        }

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
            "private static void MergeGlobalSubcommand(global::Kawayi.CommandLine.Abstractions.CliSchemaBuilder builder, global::Kawayi.CommandLine.Abstractions.CliSchemaBuilder childBuilder, string childTypeName, string memberName)");
        GeneratorSource.AppendIndentedLine(builder, indentLevel, "{");
        GeneratorSource.AppendIndentedLine(builder, indentLevel + 1, "foreach (var argument in childBuilder.Argument)");
        GeneratorSource.AppendIndentedLine(builder, indentLevel + 1, "{");
        GeneratorSource.AppendIndentedLine(builder, indentLevel + 2, "builder.Argument.Add(argument);");
        GeneratorSource.AppendIndentedLine(builder, indentLevel + 1, "}");
        GeneratorSource.AppendIndentedLine(builder, indentLevel + 1, "foreach (var pair in childBuilder.Properties)");
        GeneratorSource.AppendIndentedLine(builder, indentLevel + 1, "{");
        GeneratorSource.AppendIndentedLine(builder, indentLevel + 2, "if (builder.Properties.ContainsKey(pair.Key))");
        GeneratorSource.AppendIndentedLine(builder, indentLevel + 2, "{");
        GeneratorSource.AppendIndentedLine(builder, indentLevel + 3, "throw new global::System.InvalidOperationException(\"Global subcommand member '\" + memberName + \"' from '\" + childTypeName + \"' cannot be promoted because property key '\" + pair.Key + \"' already exists.\");");
        GeneratorSource.AppendIndentedLine(builder, indentLevel + 2, "}");
        GeneratorSource.AppendIndentedLine(builder, indentLevel + 2, "builder.Properties[pair.Key] = pair.Value;");
        GeneratorSource.AppendIndentedLine(builder, indentLevel + 1, "}");
        GeneratorSource.AppendIndentedLine(builder, indentLevel + 1, "foreach (var pair in childBuilder.SubcommandDefinitions)");
        GeneratorSource.AppendIndentedLine(builder, indentLevel + 1, "{");
        GeneratorSource.AppendIndentedLine(builder, indentLevel + 2, "if (builder.SubcommandDefinitions.ContainsKey(pair.Key))");
        GeneratorSource.AppendIndentedLine(builder, indentLevel + 2, "{");
        GeneratorSource.AppendIndentedLine(builder, indentLevel + 3, "throw new global::System.InvalidOperationException(\"Global subcommand member '\" + memberName + \"' from '\" + childTypeName + \"' cannot be promoted because subcommand key '\" + pair.Key + \"' already exists.\");");
        GeneratorSource.AppendIndentedLine(builder, indentLevel + 2, "}");
        GeneratorSource.AppendIndentedLine(builder, indentLevel + 2, "builder.SubcommandDefinitions[pair.Key] = pair.Value;");
        GeneratorSource.AppendIndentedLine(builder, indentLevel + 1, "}");
        GeneratorSource.AppendIndentedLine(builder, indentLevel + 1, "foreach (var pair in childBuilder.Subcommands)");
        GeneratorSource.AppendIndentedLine(builder, indentLevel + 1, "{");
        GeneratorSource.AppendIndentedLine(builder, indentLevel + 2, "if (builder.Subcommands.ContainsKey(pair.Key))");
        GeneratorSource.AppendIndentedLine(builder, indentLevel + 2, "{");
        GeneratorSource.AppendIndentedLine(builder, indentLevel + 3, "throw new global::System.InvalidOperationException(\"Global subcommand member '\" + memberName + \"' from '\" + childTypeName + \"' cannot be promoted because nested builder key '\" + pair.Key + \"' already exists.\");");
        GeneratorSource.AppendIndentedLine(builder, indentLevel + 2, "}");
        GeneratorSource.AppendIndentedLine(builder, indentLevel + 2, "builder.Subcommands[pair.Key] = pair.Value;");
        GeneratorSource.AppendIndentedLine(builder, indentLevel + 1, "}");
        GeneratorSource.AppendIndentedLine(builder, indentLevel, "}");
    }

    private sealed class ParsingTarget
    {
        public ParsingTarget(CommandModel model, ImmutableArray<MemberModel> subcommands, ImmutableArray<DiagnosticInfo> diagnostics)
        {
            Model = model;
            Subcommands = subcommands;
            Diagnostics = diagnostics;
        }

        public CommandModel Model { get; }

        public ImmutableArray<MemberModel> Subcommands { get; }

        public ImmutableArray<DiagnosticInfo> Diagnostics { get; }
    }
}
