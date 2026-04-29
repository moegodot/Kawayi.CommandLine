// Copyright (c) 2026 MoeGodot<me@kawayi.moe>.
// Licensed under the GNU Affero General Public License v3-or-later license.

using System.Collections;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Kawayi.CommandLine.Abstractions;
using Kawayi.CommandLine.Core.Primitives;

namespace Kawayi.CommandLine.Core;

/// <summary>
/// Parses token sequences against immutable <see cref="ParsingInput"/> snapshots.
/// </summary>
public sealed class ParsingInputParser : Abstractions.IParsable<ParsingInput>
{
    private ParsingInputParser()
    {
    }

    /// <summary>
    /// Parses tokens against the provided schema snapshot.
    /// User input errors are returned as <see cref="ParsingResult"/> values, while parser or schema
    /// inconsistencies that prevent positional argument allocation throw exceptions directly.
    /// </summary>
    /// <param name="options">The parsing options for this operation.</param>
    /// <param name="arguments">The tokens to parse.</param>
    /// <param name="initialState">The immutable schema snapshot to parse against.</param>
    /// <returns>The parsing outcome.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the parser cannot allocate positional arguments even though the schema itself is expected
    /// to make that allocation possible.
    /// </exception>
    public static ParsingResult CreateParsing(ParsingOptions options, ImmutableArray<Token> arguments, ParsingInput initialState)
    {
        ArgumentNullException.ThrowIfNull(options);

        var rootScope = new ScopeParseState(initialState, null, null);
        return ContinueParsing(options, arguments, rootScope, 0);
    }

    private static ParsingResult? ParseScope(ParsingOptions options,
                                             ImmutableArray<Token> arguments,
                                             ScopeParseState scope,
                                             int startIndex)
    {
        var longOptions = BuildAliasMap(scope.Builder.Properties.Values, static definition => definition.LongName);
        var shortOptions = BuildAliasMap(scope.Builder.Properties.Values, static definition => definition.ShortName);
        var pendingArguments = ImmutableArray.CreateBuilder<Token>();

        for (var index = startIndex; index < arguments.Length; index++)
        {
            var token = arguments[index];
            var tokenText = GetTokenText(token);

            if (options.VersionFlags.Contains(token))
            {
                return new VersionFlagsDetected(CreateVersionAction(options), tokenText);
            }

            if (options.HelpFlags.Contains(token))
            {
                return new HelpFlagsDetected(CreateHelpAction(options, scope), tokenText);
            }

            switch (token)
            {
                case ShortOptionToken shortOption:
                    if (!shortOptions.TryGetValue(shortOption.Value, out var shortProperty))
                    {
                        return new UnknownArgumentDetected(tokenText, null);
                    }

                    {
                        var propertyResult = BindProperty(arguments, index, shortProperty, null, scope);

                        if (!propertyResult.Success)
                        {
                            return propertyResult.Result;
                        }

                        index = propertyResult.NextIndex;
                    }
                    break;
                case LongOptionToken longOption:
                    if (!longOptions.TryGetValue(longOption.Value, out var longProperty))
                    {
                        return new UnknownArgumentDetected(tokenText, null);
                    }

                    {
                        var propertyResult = BindProperty(arguments, index, longProperty, longOption.InlineNextValue, scope);

                        if (!propertyResult.Success)
                        {
                            return propertyResult.Result;
                        }

                        index = propertyResult.NextIndex;
                    }
                    break;
                case ArgumentOrCommandToken argumentToken:
                    if (TryMatchSubcommand(scope.Builder, argumentToken.Value, out var subcommandKey, out var subcommand))
                    {
                        var argumentBindingResult = BindPendingArguments(scope.Builder.Argument, pendingArguments, scope);

                        if (argumentBindingResult is not null)
                        {
                            return argumentBindingResult;
                        }

                        if (!scope.Builder.Subcommands.TryGetValue(subcommandKey, out var childBuilder))
                        {
                            return new GotError(new InvalidOperationException(
                                $"Subcommand '{subcommand.Information.Name.Value}' matched but does not have a builder."));
                        }

                        return CreateSubcommandResult(options,
                                                      arguments,
                                                      new ScopeParseState(childBuilder, subcommand, scope),
                                                      index + 1,
                                                      subcommand,
                                                      argumentToken);
                    }

                    pendingArguments.Add(NormalizeValueToken(argumentToken));
                    break;
                default:
                    return new UnknownArgumentDetected(tokenText, null);
            }
        }

        return BindPendingArguments(scope.Builder.Argument, pendingArguments, scope);
    }

    private static ParsingResult ContinueParsing(ParsingOptions options,
                                                 ImmutableArray<Token> arguments,
                                                 ScopeParseState scope,
                                                 int startIndex)
    {
        var parseResult = ParseScope(options, arguments, scope, startIndex);

        if (parseResult is Subcommand)
        {
            return parseResult;
        }

        return EmitBuilderDebug(options,
                                parseResult ?? FinalizeParse(options, scope),
                                scope,
                                arguments,
                                startIndex);
    }

    private static ParsingResult CreateSubcommandResult(ParsingOptions options,
                                                        ImmutableArray<Token> arguments,
                                                        ScopeParseState scope,
                                                        int startIndex,
                                                        CommandDefinition definition,
                                                        Token triggerToken)
    {
        if (scope.Parent is null)
        {
            throw new InvalidOperationException(
                $"Subcommand '{definition.Information.Name.Value}' cannot be created without a parent scope.");
        }

        var parentParseResult = FinalizeParse(options, scope.Parent);

        if (parentParseResult is not ParsingFinished parentCommand)
        {
            return parentParseResult;
        }

        ParsingResult? cachedResult = null;
        var result = new Subcommand(parentCommand,
                                    definition,
                                    () => cachedResult ??= ContinueParsing(options, arguments, scope, startIndex));

        return EmitBuilderDebug(options,
                                result,
                                scope.Parent,
                                arguments,
                                Math.Max(0, startIndex - 1),
                                triggerToken,
                                $"handoff to '{definition.Information.Name.Value}'");
    }

    private static ParsingResult EmitBuilderDebug(ParsingOptions options,
                                                  ParsingResult result,
                                                  ScopeParseState scope,
                                                  ImmutableArray<Token> arguments,
                                                  int startIndex,
                                                  Token? triggerToken = null,
                                                  string? summary = null)
    {
        return DebugOutput.Emit(options,
                                result,
                                new DebugContext(
                                    nameof(ParsingInputParser),
                                    Tokens: arguments,
                                    ActiveTokens: SliceTokens(arguments, startIndex),
                                    CommandPath: scope.CommandPath,
                                    TriggerToken: triggerToken,
                                    Summary: summary ?? BuildBuilderSummary(result)));
    }

    private static string? BuildBuilderSummary(ParsingResult result)
    {
        if (result is not ParsingFinished { UntypedResult: IParsingResultCollection collection })
        {
            return null;
        }

        var commandSummary = DescribeCommandPath(collection);
        var definitions = collection.Scope.AvailableTypedDefinitions
            .Select(static definition => definition.Information.Name.Value)
            .Where(static name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static name => name, StringComparer.Ordinal)
            .ToImmutableArray();

        var definitionSummary = definitions.IsDefaultOrEmpty ? "none" : string.Join(", ", definitions);
        return $"commands={commandSummary}; definitions={definitionSummary}";
    }

    private static string DescribeCommandPath(IParsingResultCollection collection)
    {
        var commands = new Stack<string>();
        IParsingResultCollection? current = collection;

        while (current is not null)
        {
            if (current.Command is not null)
            {
                commands.Push(current.Command.Information.Name.Value);
            }

            current = current.Parent;
        }

        return commands.Count == 0 ? "none" : string.Join(", ", commands);
    }

    private static ImmutableArray<Token> SliceTokens(ImmutableArray<Token> arguments, int startIndex)
    {
        if (arguments.IsDefaultOrEmpty || startIndex >= arguments.Length)
        {
            return [];
        }

        return arguments.Skip(Math.Max(startIndex, 0)).ToImmutableArray();
    }

    private static Action CreateHelpAction(ParsingOptions options, ScopeParseState scope)
    {
        return () => options.Output.Write(RenderHelpText(options, scope));
    }

    private static Action CreateVersionAction(ParsingOptions options)
    {
        return () => options.Output.Write(RenderVersionText(options));
    }

    private static string RenderVersionText(ParsingOptions options)
    {
        var builder = new StyledStringBuilder(options.EnableStyle);
        var styleTable = options.StyleTable;

        builder.Append(styleTable.ProgramNameStyle, options.Program.Name)
            .AppendLine($" {options.Program.Version}");

        if (!string.IsNullOrWhiteSpace(options.Program.Homepage))
        {
            builder.AppendLine(styleTable.SecondaryTextStyle, options.Program.Homepage);
        }

        return builder.ToString();
    }

    private static string RenderHelpText(ParsingOptions options, ScopeParseState scope)
    {
        var text = new StyledStringBuilder(options.EnableStyle);
        var document = scope.Document;
        var styleTable = options.StyleTable;

        text.AppendLine(styleTable.HelpTitleStyle, scope.DisplayName);

        if (!string.IsNullOrWhiteSpace(document.ConciseDescription))
        {
            text.AppendLine(styleTable.DescriptionStyle, document.ConciseDescription);
        }

        text.AppendLine();
        text.AppendLine(styleTable.UsageLabelStyle, "Usage");

        foreach (var usageLine in BuildUsageLines(scope))
        {
            text.Append("  ");
            AppendUsageLine(text, options, usageLine);
            text.AppendLine();
        }

        if (!string.IsNullOrWhiteSpace(document.HelpText))
        {
            text.AppendLine();
            text.AppendLine(styleTable.DescriptionStyle, document.HelpText);
        }

        var argumentEntries = scope.Builder.Argument
            .Select(static definition => CreateArgumentEntry(definition))
            .Where(static entry => entry is not null)
            .Cast<SectionEntry>()
            .ToImmutableArray();
        var optionEntries = scope.Builder.Properties.Values
            .Select(CreatePropertyEntry)
            .Where(static entry => entry is not null)
            .Cast<PropertySectionEntry>()
            .OrderBy(static entry => entry.SortKey, StringComparer.Ordinal)
            .ToImmutableArray();
        var subcommandEntries = scope.Builder.SubcommandDefinitions.Values
            .Select(CreateSubcommandEntry)
            .Where(static entry => entry is not null)
            .Cast<SectionEntry>()
            .OrderBy(static entry => entry.SortKey, StringComparer.Ordinal)
            .ToImmutableArray();

        AppendSection(text, options, "Arguments", argumentEntries);
        AppendOptionsSection(text, options, optionEntries);
        AppendSection(text, options, "Subcommands", subcommandEntries);

        return text.ToString();
    }

    private static void AppendSection(StyledStringBuilder text,
                                      ParsingOptions options,
                                      string title,
                                      ImmutableArray<SectionEntry> entries)
    {
        if (entries.IsDefaultOrEmpty)
        {
            return;
        }

        text.AppendLine();
        AppendSectionHeader(text, options, title);

        foreach (var entry in entries)
        {
            text.Append("  ")
                .Append(options.StyleTable.DefinitionNameStyle, entry.Name);

            if (!string.IsNullOrWhiteSpace(entry.Description))
            {
                text.Append("  ")
                    .AppendLine(options.StyleTable.DescriptionStyle, entry.Description);
            }
            else
            {
                text.AppendLine();
            }

            AppendPossibleValues(text, options, entry.PossibleValues);
        }
    }

    private static void AppendOptionsSection(StyledStringBuilder text,
                                             ParsingOptions options,
                                             ImmutableArray<PropertySectionEntry> entries)
    {
        if (entries.IsDefaultOrEmpty)
        {
            return;
        }

        text.AppendLine();
        AppendSectionHeader(text, options, "Options");

        foreach (var entry in entries)
        {
            text.Append("  ");
            AppendPropertySignature(text, options, entry);

            if (!string.IsNullOrWhiteSpace(entry.Description))
            {
                text.Append("  ")
                    .AppendLine(options.StyleTable.DescriptionStyle, entry.Description);
            }
            else
            {
                text.AppendLine();
            }

            AppendPossibleValues(text, options, entry.PossibleValues);
        }
    }

    private static void AppendSectionHeader(StyledStringBuilder text, ParsingOptions options, string title)
    {
        text.AppendLine(options.StyleTable.SectionHeaderStyle, title);
    }

    private static ImmutableArray<UsageLine> BuildUsageLines(ScopeParseState scope)
    {
        var lines = ImmutableArray.CreateBuilder<UsageLine>();
        var commandPath = scope.CommandPath;
        var hasVisibleOptions = scope.Builder.Properties.Values.Any(HasVisibleAlias);
        var visibleArguments = scope.Builder.Argument.ToImmutableArray();
        var hasVisibleSubcommands = scope.Builder.SubcommandDefinitions.Values.Any(HasVisibleCommandName);

        if (!hasVisibleOptions && visibleArguments.IsDefaultOrEmpty && !hasVisibleSubcommands)
        {
            lines.Add(new UsageLine(commandPath, []));
            return lines.ToImmutable();
        }

        if (hasVisibleOptions)
        {
            lines.Add(new UsageLine(commandPath, [new UsageToken("[options]", false)]));
        }

        if (!visibleArguments.IsDefaultOrEmpty)
        {
            var tokens = visibleArguments
                .Select(CreateArgumentUsageToken)
                .Where(static token => token is not null)
                .Cast<UsageToken>()
                .ToImmutableArray();

            if (!tokens.IsDefaultOrEmpty)
            {
                lines.Add(new UsageLine(commandPath, tokens));
            }
        }

        if (hasVisibleSubcommands)
        {
            lines.Add(new UsageLine(commandPath, [new UsageToken("<subcommand>", true)]));
        }

        return lines.ToImmutable();
    }

    private static SectionEntry? CreateArgumentEntry(ArgumentDefinition definition)
    {
        var name = GetVisibleDefinitionName(definition);

        return name is null
            ? null
            : new SectionEntry(name,
                               definition.Information.Document.ConciseDescription,
                               name,
                               GetPossibleValues(definition.Type, null));
    }

    private static PropertySectionEntry? CreatePropertyEntry(PropertyDefinition definition)
    {
        var visibleNames = GetVisiblePropertyNames(definition);
        var valueName = definition.Type == typeof(bool)
            ? definition.ValueName ?? "bool"
            : definition.ValueName ?? GetTypeDisplayName(definition.Type);

        return visibleNames.Length == 0
            ? null
            : new PropertySectionEntry(visibleNames,
                                       valueName,
                                       definition.Information.Document.ConciseDescription,
                                       visibleNames[0],
                                       GetPossibleValues(definition.Type, definition.PossibleValues));
    }

    private static SectionEntry? CreateSubcommandEntry(CommandDefinition definition)
    {
        var visibleNames = GetVisibleCommandNames(definition);
        var displayName = visibleNames.IsDefaultOrEmpty ? null : string.Join(", ", visibleNames);
        var sortKey = visibleNames.IsDefaultOrEmpty
            ? definition.Information.Name.Value
            : visibleNames[0];

        return string.IsNullOrWhiteSpace(displayName)
            ? null
            : new SectionEntry(displayName,
                               definition.Information.Document.ConciseDescription,
                               sortKey,
                               null);
    }

    private static bool HasVisibleAlias(PropertyDefinition definition)
    {
        return definition.LongName.Values.Any(static alias => alias.Visible)
            || definition.ShortName.Values.Any(static alias => alias.Visible);
    }

    private static bool HasVisibleCommandName(CommandDefinition definition)
    {
        return !GetVisibleCommandNames(definition).IsDefaultOrEmpty;
    }

    private static string? GetVisibleDefinitionName(TypedDefinition definition)
    {
        return definition.Information.Name.Visible && !string.IsNullOrWhiteSpace(definition.Information.Name.Value)
            ? definition.Information.Name.Value
            : null;
    }

    private static UsageToken? CreateArgumentUsageToken(ArgumentDefinition definition)
    {
        var name = GetVisibleDefinitionName(definition);

        if (name is null)
        {
            return null;
        }

        var core = definition.ValueRange.Maximum > 1 ? $"<{name}>..." : $"<{name}>";

        return new UsageToken(definition.ValueRange.Minimum == 0 ? $"[{core}]" : core, true);
    }

    private static ImmutableArray<string> GetVisiblePropertyNames(PropertyDefinition definition)
    {
        var names = ImmutableArray.CreateBuilder<string>();

        foreach (var alias in definition.ShortName.Values
                     .Where(static alias => alias.Visible)
                     .Select(static alias => $"-{alias.Value}")
                     .Distinct(StringComparer.Ordinal)
                     .OrderBy(static alias => alias, StringComparer.Ordinal))
        {
            names.Add(alias);
        }

        foreach (var alias in definition.LongName.Values
                     .Where(static alias => alias.Visible)
                     .Select(static alias => $"--{alias.Value}")
                     .Distinct(StringComparer.Ordinal)
                     .OrderBy(static alias => alias, StringComparer.Ordinal))
        {
            names.Add(alias);
        }

        return names.ToImmutable();
    }

    private static ImmutableArray<string> GetVisibleCommandNames(CommandDefinition definition)
    {
        var names = ImmutableArray.CreateBuilder<string>();

        if (definition.Information.Name.Visible && !string.IsNullOrWhiteSpace(definition.Information.Name.Value))
        {
            names.Add(definition.Information.Name.Value);
        }

        foreach (var alias in definition.Alias.Values
                     .Where(static alias => alias.Visible)
                     .Select(static alias => alias.Value)
                     .Distinct(StringComparer.Ordinal)
                     .OrderBy(static alias => alias, StringComparer.Ordinal))
        {
            if ((names.Count == 0 || !string.Equals(names[0], alias, StringComparison.Ordinal))
                && !names.Contains(alias))
            {
                names.Add(alias);
            }
        }

        return names.ToImmutable();
    }

    private static void AppendUsageLine(StyledStringBuilder text, ParsingOptions options, UsageLine usageLine)
    {
        text.Append(options.StyleTable.UsageCommandStyle, usageLine.CommandPath);

        foreach (var token in usageLine.Tokens)
        {
            text.Append(" ");
            text.Append(token.IsMetavar ? options.StyleTable.MetavarStyle : options.StyleTable.SecondaryTextStyle, token.Text);
        }
    }

    private static void AppendPropertySignature(StyledStringBuilder text,
                                                ParsingOptions options,
                                                PropertySectionEntry entry)
    {
        text.Append(options.StyleTable.OptionSignatureStyle, string.Join(", ", entry.VisibleNames));

        if (!string.IsNullOrWhiteSpace(entry.ValueName))
        {
            text.Append(" ")
                .Append(options.StyleTable.MetavarStyle, $"<{entry.ValueName}>");
        }
    }

    private static void AppendPossibleValues(StyledStringBuilder text,
                                             ParsingOptions options,
                                             PossibleValues? possibleValues)
    {
        if (possibleValues is null)
        {
            return;
        }

        text.Append("    ")
            .Append(options.StyleTable.PossibleValuesLabelStyle, "Possible values:")
            .Append(" ");

        switch (possibleValues)
        {
            case DescripablePossibleValues description:
                text.AppendLine(options.StyleTable.PossibleValuesValueStyle, description.Description);
                return;
            default:
                if (TryFormatCountablePossibleValues(possibleValues, out var countableText))
                {
                    text.AppendLine(options.StyleTable.PossibleValuesValueStyle, countableText);
                    return;
                }

                text.AppendLine(options.StyleTable.PossibleValuesValueStyle, possibleValues.ToString() ?? string.Empty);
                return;
        }
    }

    private static bool TryFormatCountablePossibleValues(PossibleValues possibleValues, out string text)
    {
        text = string.Empty;

        if (possibleValues is not ICountablePossibleValues countable)
        {
            return false;
        }

        var values = new List<string>();

        foreach (var candidate in countable.Candidates)
        {
            values.Add(candidate?.ToString() ?? string.Empty);
        }

        text = string.Join(", ", values);
        return true;
    }

    private static PossibleValues? GetPossibleValues(Type type, PossibleValues? explicitPossibleValues)
    {
        if (explicitPossibleValues is not null)
        {
            return explicitPossibleValues;
        }

        return type.IsEnum
            ? new CountablePossibleValues<string>([.. Enum.GetNames(type)])
            : null;
    }

    private static string GetTypeDisplayName(Type type)
    {
        if (type == typeof(string))
        {
            return "string";
        }

        if (type == typeof(bool))
        {
            return "bool";
        }

        if (type == typeof(byte))
        {
            return "byte";
        }

        if (type == typeof(sbyte))
        {
            return "sbyte";
        }

        if (type == typeof(short))
        {
            return "short";
        }

        if (type == typeof(ushort))
        {
            return "ushort";
        }

        if (type == typeof(int))
        {
            return "int";
        }

        if (type == typeof(uint))
        {
            return "uint";
        }

        if (type == typeof(long))
        {
            return "long";
        }

        if (type == typeof(ulong))
        {
            return "ulong";
        }

        if (type == typeof(float))
        {
            return "float";
        }

        if (type == typeof(double))
        {
            return "double";
        }

        if (type == typeof(decimal))
        {
            return "decimal";
        }

        if (type == typeof(char))
        {
            return "char";
        }

        if (type == typeof(object))
        {
            return "object";
        }

        var nullableType = Nullable.GetUnderlyingType(type);
        if (nullableType is not null)
        {
            return $"{GetTypeDisplayName(nullableType)}?";
        }

        if (type.IsGenericType)
        {
            var genericTypeName = type.Name;
            var genericMarkerIndex = genericTypeName.IndexOf('`');
            if (genericMarkerIndex >= 0)
            {
                genericTypeName = genericTypeName[..genericMarkerIndex];
            }

            var genericArguments = type.GetGenericArguments()
                .Select(GetTypeDisplayName);

            return $"{genericTypeName}<{string.Join(", ", genericArguments)}>";
        }

        return type.Name;
    }

    private static ParsingResult FinalizeParse(ParsingOptions options, ScopeParseState leafScope)
    {
        var scopeChain = CollectScopeChain(leafScope);
        var nodes = new List<ParsingResultCollection>(scopeChain.Length);
        ParsingResultCollection? parentNode = null;

        foreach (var scope in scopeChain)
        {
            if (!TryCreateExplicitValues(options, scope, out var explicitValues, out var errorResult))
            {
                return errorResult!;
            }

            var node = new ParsingResultCollection(scope.Command,
                                                   parentNode,
                                                   CreateScopeMetadata(scope.Builder),
                                                   explicitValues);

            if (parentNode is not null && scope.Command is not null)
            {
                parentNode.SetDirectSubcommand(scope.Command, node);
            }

            nodes.Add(node);
            parentNode = node;
        }

        foreach (var node in nodes)
        {
            var validationResult = ValidateScope(node);

            if (validationResult is not null)
            {
                return validationResult;
            }
        }

        return new ParsingFinished<IParsingResultCollection>(nodes[^1]);
    }

    private static ImmutableArray<ScopeParseState> CollectScopeChain(ScopeParseState leafScope)
    {
        var states = new Stack<ScopeParseState>();
        var current = leafScope;

        while (current is not null)
        {
            states.Push(current);
            current = current.Parent;
        }

        return [.. states];
    }

    private static bool TryCreateExplicitValues(ParsingOptions options,
                                                ScopeParseState scope,
                                                out ImmutableDictionary<TypedDefinition, object?> explicitValues,
                                                out ParsingResult? errorResult)
    {
        var builder = ImmutableDictionary.CreateBuilder<TypedDefinition, object?>();

        foreach (var definition in GetAvailableTypedDefinitions(scope.Builder))
        {
            var valueCount = scope.GetCount(definition);
            var valueRange = GetExpectedValueRange(definition);

            if (!IsWithinRange(valueCount, valueRange))
            {
                explicitValues = ImmutableDictionary<TypedDefinition, object?>.Empty;
                errorResult = new InvalidArgumentDetected(definition.Information.Name.Value,
                                                          FormatValueRangeExpectation(valueRange),
                                                          null);
                return false;
            }

            if (!scope.TryGetValues(definition, out var rawValues))
            {
                continue;
            }

            var materialized = ParseDefinitionValue(options, definition, rawValues);

            if (materialized is not ParsingFinished finished)
            {
                explicitValues = ImmutableDictionary<TypedDefinition, object?>.Empty;
                errorResult = materialized;
                return false;
            }

            builder[definition] = finished.UntypedResult;
        }

        explicitValues = builder.ToImmutable();
        errorResult = null;
        return true;
    }

    private static ValueRange GetExpectedValueRange(TypedDefinition definition)
    {
        return definition switch
        {
            ArgumentDefinition argumentDefinition => argumentDefinition.ValueRange,
            PropertyDefinition propertyDefinition => propertyDefinition.NumArgs,
            _ => ValueRange.ZeroOrMore
        };
    }

    private static bool IsWithinRange(int valueCount, ValueRange valueRange)
    {
        return valueCount >= valueRange.Minimum && valueCount <= valueRange.Maximum;
    }

    private static string FormatValueRangeExpectation(ValueRange valueRange)
    {
        if (valueRange.Minimum == valueRange.Maximum)
        {
            return $"exactly {valueRange.Minimum} value(s)";
        }

        if (valueRange.Maximum == int.MaxValue)
        {
            return $"at least {valueRange.Minimum} value(s)";
        }

        if (valueRange.Minimum == 0)
        {
            return $"at most {valueRange.Maximum} value(s)";
        }

        return $"between {valueRange.Minimum} and {valueRange.Maximum} value(s)";
    }

    private static ParsingResult? ValidateScope(IParsingResultCollection scope)
    {
        foreach (var definition in scope.Scope.AvailableTypedDefinitions)
        {
            object? value;

            try
            {
                value = ResolveEffectiveValue(scope, definition);
            }
            catch (InvalidOperationException exception)
            {
                return new InvalidArgumentDetected(definition.Information.Name.Value,
                                                   definition.Type.FullName ?? definition.Type.Name,
                                                   exception);
            }
            catch (Exception exception)
            {
                return new GotError(exception);
            }

            if (definition.Validation is null)
            {
                continue;
            }

            string? validationResult;

            try
            {
                validationResult = definition.Validation(value!);
            }
            catch (Exception exception)
            {
                return new GotError(exception);
            }

            if (validationResult is not null)
            {
                return new FailedValidation(definition.Information.Name.Value, validationResult, null);
            }
        }

        return null;
    }

    private static object? ResolveEffectiveValue(IParsingResultCollection scope, TypedDefinition definition)
    {
        if (scope.TryGetValue(definition, out var explicitValue))
        {
            return explicitValue;
        }

        if (definition.DefaultValueFactory is not null)
        {
            return definition.DefaultValueFactory(scope);
        }

        if (definition.Requirement)
        {
            throw new InvalidOperationException(
                $"Required definition '{definition.Information.Name.Value}' does not have an explicit value or default factory.");
        }

        return GetClrDefault(definition.Type);
    }

    private static PropertyBindingResult BindProperty(ImmutableArray<Token> arguments,
                                                      int optionIndex,
                                                      PropertyDefinition property,
                                                      string? inlineValue,
                                                      ScopeParseState scope)
    {
        if (inlineValue is not null)
        {
            scope.AddPropertyValue(property, new ArgumentOrCommandToken(inlineValue));
            return PropertyBindingResult.CreateSuccess(optionIndex);
        }

        var valueIndex = optionIndex + 1;

        if (valueIndex >= arguments.Length)
        {
            return PropertyBindingResult.Failure(new InvalidArgumentDetected(
                property.Information.Name.Value,
                property.Type.FullName ?? property.Type.Name,
                null));
        }

        scope.AddPropertyValue(property, NormalizeValueToken(arguments[valueIndex]));
        return PropertyBindingResult.CreateSuccess(valueIndex);
    }

    private static ParsingResult ParseDefinitionValue(ParsingOptions options,
                                                      TypedDefinition definition,
                                                      ImmutableArray<Token> rawValues)
    {
        if (TryCreateContainerType(definition.Type, out var containerType))
        {
            return Containers.CreateParsing(options, rawValues, containerType);
        }

        if (definition.Type.IsEnum)
        {
            return EnumParser.CreateParsing(options, rawValues, definition.Type, GetClrDefault(definition.Type)!);
        }

        if (definition.Type == typeof(bool))
        {
            return BooleanParser.CreateParsing(options, rawValues, false);
        }

        if (definition.Type == typeof(byte))
        {
            return NumberParser.CreateParsing(options, rawValues, (byte)0);
        }

        if (definition.Type == typeof(sbyte))
        {
            return NumberParser.CreateParsing(options, rawValues, (sbyte)0);
        }

        if (definition.Type == typeof(ushort))
        {
            return NumberParser.CreateParsing(options, rawValues, (ushort)0);
        }

        if (definition.Type == typeof(short))
        {
            return NumberParser.CreateParsing(options, rawValues, (short)0);
        }

        if (definition.Type == typeof(uint))
        {
            return NumberParser.CreateParsing(options, rawValues, 0u);
        }

        if (definition.Type == typeof(int))
        {
            return NumberParser.CreateParsing(options, rawValues, 0);
        }

        if (definition.Type == typeof(ulong))
        {
            return NumberParser.CreateParsing(options, rawValues, 0UL);
        }

        if (definition.Type == typeof(long))
        {
            return NumberParser.CreateParsing(options, rawValues, 0L);
        }

        if (definition.Type == typeof(float))
        {
            return FloatParser.CreateParsing(options, rawValues, 0f);
        }

        if (definition.Type == typeof(double))
        {
            return FloatParser.CreateParsing(options, rawValues, 0d);
        }

        if (definition.Type == typeof(decimal))
        {
            return FloatParser.CreateParsing(options, rawValues, decimal.Zero);
        }

        if (definition.Type == typeof(Guid))
        {
            return CommonParser.CreateParsing(options, rawValues, Guid.Empty);
        }

        if (definition.Type == typeof(string))
        {
            return CommonParser.CreateParsing(options, rawValues, string.Empty);
        }

        if (definition.Type == typeof(Uri))
        {
            return CommonParser.CreateParsing(options, rawValues, new Uri("https://placeholder.invalid"));
        }

        if (definition.Type == typeof(DateTime))
        {
            return CommonParser.CreateParsing(options, rawValues, default(DateTime));
        }

        if (definition.Type == typeof(DateTimeOffset))
        {
            return CommonParser.CreateParsing(options, rawValues, default(DateTimeOffset));
        }

        if (definition.Type == typeof(DateOnly))
        {
            return CommonParser.CreateParsing(options, rawValues, default(DateOnly));
        }

        if (definition.Type == typeof(TimeOnly))
        {
            return CommonParser.CreateParsing(options, rawValues, default(TimeOnly));
        }

        return new GotError(new NotSupportedException(
            $"Type '{definition.Type.FullName}' is not supported by {nameof(ParsingInputParser)}."));
    }

    private static ImmutableDictionary<string, PropertyDefinition> BuildAliasMap(
        IEnumerable<PropertyDefinition> definitions,
        Func<PropertyDefinition, ImmutableDictionary<string, NameWithVisibility>> selector)
    {
        var builder = ImmutableDictionary.CreateBuilder<string, PropertyDefinition>(StringComparer.Ordinal);

        foreach (var definition in definitions)
        {
            foreach (var (key, alias) in selector(definition))
            {
                builder[key] = definition;
                builder[alias.Value] = definition;
            }
        }

        return builder.ToImmutable();
    }

    private static bool TryMatchSubcommand(ParsingInput builder,
                                           string candidate,
                                           out string subcommandKey,
                                           out CommandDefinition subcommand)
    {
        if (builder.SubcommandDefinitions.TryGetValue(candidate, out var matchedSubcommand) &&
            matchedSubcommand is not null)
        {
            subcommand = matchedSubcommand;
            subcommandKey = candidate;
            return true;
        }

        foreach (var (key, value) in builder.SubcommandDefinitions)
        {
            if (value.Information.Name.Value != candidate
                && !value.Alias.ContainsKey(candidate)
                && !value.Alias.Values.Any(alias => alias.Value == candidate))
            {
                continue;
            }

            subcommand = value;
            subcommandKey = key;
            return true;
        }

        subcommandKey = string.Empty;
        subcommand = null!;
        return false;
    }

    private static ParsingResult? BindPendingArguments(IReadOnlyList<ArgumentDefinition> arguments,
                                                       ImmutableArray<Token>.Builder pendingArguments,
                                                       ScopeParseState scope)
    {
        if (pendingArguments.Count == 0)
        {
            return null;
        }

        var suffixMinimum = new long[arguments.Count + 1];
        var suffixMaximum = new long[arguments.Count + 1];

        for (var index = arguments.Count - 1; index >= 0; index--)
        {
            suffixMinimum[index] = suffixMinimum[index + 1] + arguments[index].ValueRange.Minimum;
            suffixMaximum[index] = suffixMaximum[index + 1] + arguments[index].ValueRange.Maximum;
        }

        var remainingTokens = pendingArguments.Count;
        var tokenIndex = 0;
        var totalMinimum = suffixMinimum[0];
        var totalMaximum = suffixMaximum[0];
        var hasFeasibleTokenCount = remainingTokens >= totalMinimum && remainingTokens <= totalMaximum;

        for (var index = 0; index < arguments.Count && remainingTokens > 0; index++)
        {
            var currentArgument = arguments[index];
            var suffixMinimumAfterCurrent = suffixMinimum[index + 1];
            var suffixMaximumAfterCurrent = suffixMaximum[index + 1];
            var maximumAssignable = Math.Min(currentArgument.ValueRange.Maximum,
                                             Math.Max(0L, remainingTokens - suffixMinimumAfterCurrent));

            if (hasFeasibleTokenCount)
            {
                var minimumAssignable = Math.Max(currentArgument.ValueRange.Minimum,
                                                 remainingTokens - suffixMaximumAfterCurrent);

                if (minimumAssignable > maximumAssignable)
                {
                    throw new InvalidOperationException(
                        $"Unable to allocate positional arguments for '{currentArgument.Information.Name.Value}'.");
                }
            }

            var assignCount = checked((int)maximumAssignable);

            for (var valueIndex = 0; valueIndex < assignCount; valueIndex++)
            {
                scope.AddArgumentValue(currentArgument, pendingArguments[tokenIndex++]);
            }

            remainingTokens -= assignCount;
        }

        if (remainingTokens > 0)
        {
            return new UnknownArgumentDetected(GetTokenText(pendingArguments[tokenIndex]), null);
        }

        return null;
    }

    private static ParsingScopeMetadata CreateScopeMetadata(ParsingInput builder)
    {
        return new ParsingScopeMetadata(GetAvailableSubcommands(builder), GetAvailableTypedDefinitions(builder));
    }

    private static ImmutableArray<CommandDefinition> GetAvailableSubcommands(ParsingInput builder)
    {
        return builder.SubcommandDefinitions
            .OrderBy(static pair => pair.Key, StringComparer.Ordinal)
            .Select(static pair => pair.Value)
            .ToImmutableArray();
    }

    private static ImmutableArray<TypedDefinition> GetAvailableTypedDefinitions(ParsingInput builder)
    {
        var definitions = ImmutableArray.CreateBuilder<TypedDefinition>();
        definitions.AddRange(builder.Argument);
        definitions.AddRange(builder.Properties
            .OrderBy(static pair => pair.Key, StringComparer.Ordinal)
            .Select(static pair => (TypedDefinition)pair.Value));
        return definitions.ToImmutable();
    }

    private static object? GetClrDefault(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] Type type)
    {
        return TypeDefaultValues.GetValue(type);
    }

    private static Token NormalizeValueToken(Token token)
    {
        return new ArgumentOrCommandToken(GetTokenText(token));
    }

    private static string GetTokenText(Token token)
    {
        return token switch
        {
            ShortOptionToken shortOption => $"-{shortOption.Value}",
            LongOptionToken longOption => $"--{longOption.Value}",
            _ => token.Value
        };
    }

    private static bool TryCreateContainerType(Type type, out ContainerType containerType)
    {
        containerType = null!;

        if (!type.IsConstructedGenericType)
        {
            return false;
        }

        var genericDefinition = type.GetGenericTypeDefinition();
        var genericArguments = type.GetGenericArguments();

        if (genericDefinition == typeof(ImmutableDictionary<,>)
            || genericDefinition == typeof(ImmutableSortedDictionary<,>))
        {
            containerType = new ContainerType(type, genericArguments[0], genericArguments[1]);
            return true;
        }

        if (genericDefinition == typeof(ImmutableArray<>)
            || genericDefinition == typeof(ImmutableList<>)
            || genericDefinition == typeof(ImmutableQueue<>)
            || genericDefinition == typeof(ImmutableStack<>)
            || genericDefinition == typeof(ImmutableSortedSet<>)
            || genericDefinition == typeof(ImmutableHashSet<>))
        {
            containerType = new ContainerType(type, null, genericArguments[0]);
            return true;
        }

        return false;
    }

    private sealed class ScopeParseState
    {
        private readonly Dictionary<TypedDefinition, ImmutableArray<Token>.Builder> _values = [];

        public ScopeParseState(ParsingInput builder, CommandDefinition? command, ScopeParseState? parent)
        {
            Builder = builder;
            Command = command;
            Parent = parent;
        }

        public ParsingInput Builder { get; }

        public CommandDefinition? Command { get; }

        public ScopeParseState? Parent { get; }

        public string DisplayName => Command?.Information.Name.Value ?? Builder.ParsingOptions.Program.Name;

        public Document Document => Command?.Information.Document ?? Builder.ParsingOptions.Program.Document;

        public string CommandPath
        {
            get
            {
                var segments = new Stack<string>();
                ScopeParseState? current = this;

                while (current is not null)
                {
                    segments.Push(current.Command?.Information.Name.Value ?? current.Builder.ParsingOptions.Program.Name);
                    current = current.Parent;
                }

                return string.Join(' ', segments);
            }
        }

        public void AddPropertyValue(PropertyDefinition definition, Token value)
        {
            GetOrCreateValues(definition).Add(value);
        }

        public void AddArgumentValue(ArgumentDefinition definition, Token value)
        {
            GetOrCreateValues(definition).Add(value);
        }

        public int GetCount(TypedDefinition definition)
        {
            return _values.TryGetValue(definition, out var values) ? values.Count : 0;
        }

        public bool TryGetValues(TypedDefinition definition, out ImmutableArray<Token> values)
        {
            if (_values.TryGetValue(definition, out var builder))
            {
                values = builder.ToImmutable();
                return true;
            }

            values = default;
            return false;
        }

        private ImmutableArray<Token>.Builder GetOrCreateValues(TypedDefinition definition)
        {
            if (_values.TryGetValue(definition, out var values))
            {
                return values;
            }

            values = ImmutableArray.CreateBuilder<Token>();
            _values[definition] = values;
            return values;
        }
    }

    private readonly record struct PropertyBindingResult(bool Success, int NextIndex, ParsingResult? Result)
    {
        public static PropertyBindingResult CreateSuccess(int nextIndex) => new(true, nextIndex, null);

        public static PropertyBindingResult Failure(ParsingResult result) => new(false, -1, result);
    }

    private sealed record SectionEntry(string Name,
                                       string Description,
                                       string SortKey,
                                       PossibleValues? PossibleValues);

    private sealed record PropertySectionEntry(ImmutableArray<string> VisibleNames,
                                               string? ValueName,
                                               string Description,
                                               string SortKey,
                                               PossibleValues? PossibleValues);

    private readonly record struct UsageLine(string CommandPath, ImmutableArray<UsageToken> Tokens);

    private readonly record struct UsageToken(string Text, bool IsMetavar);
}
