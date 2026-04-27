// Copyright (c) 2026 MoeGodot<me@kawayi.moe>.
// Licensed under the GNU Affero General Public License v3-or-later license.

using System.Collections;
using System.Collections.Immutable;
using Kawayi.CommandLine.Abstractions;
using Kawayi.CommandLine.Core.Primitives;

namespace Kawayi.CommandLine.Core;

public sealed class ParsingBuilder : IParsingBuilder, Abstractions.IParsable<ParsingInput>
{
    public ParsingBuilder(ParsingOptions parsingOptions,
                          ImmutableDictionary<string, CommandDefinition>? subcommandDefinitions = null,
                          ImmutableDictionary<string, PropertyDefinition>? properties = null,
                          IList<ArgumentDefinition>? argument = null,
                          ImmutableDictionary<string, IParsingBuilder>? subcommands = null)
    {
        ParsingOptions = parsingOptions ?? throw new ArgumentNullException(nameof(parsingOptions));
        SubcommandDefinitions = CreateBuilder(subcommandDefinitions);
        Properties = CreateBuilder(properties);
        Argument = CreateArgumentBuilder(argument);
        Subcommands = CreateBuilder(subcommands);
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

        var context = new ParseContext();
        var rootScope = new ScopeDescriptor(initialState, null, null);
        return ContinueParsing(options, arguments, initialState, rootScope, 0, context);
    }

    public static ParsingInput CreateInput(IParsingBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var subcommandDefinitions = builder.SubcommandDefinitions.ToImmutable();
        var properties = builder.Properties.ToImmutable();
        var argument = builder.Argument.ToImmutable();
        var subcommands = ImmutableDictionary.CreateBuilder<string, ParsingInput>(StringComparer.Ordinal);

        foreach (var (key, childBuilder) in builder.Subcommands)
        {
            subcommands[key] = CreateInput(childBuilder);
        }

        return new ParsingInput(
            builder.ParsingOptions,
            subcommandDefinitions,
            subcommands.ToImmutable(),
            properties,
            argument);
    }

    public ParsingInput ToInput() => CreateInput(this);

    public ParsingOptions ParsingOptions { get; }
    public ImmutableDictionary<string, CommandDefinition>.Builder SubcommandDefinitions { get; }
    public ImmutableDictionary<string, PropertyDefinition>.Builder Properties { get; }
    public ImmutableList<ArgumentDefinition>.Builder Argument { get; }
    public ImmutableDictionary<string, IParsingBuilder>.Builder Subcommands { get; }

    private static ParsingResult? ParseScope(ParsingOptions options,
                                             ImmutableArray<Token> arguments,
                                             ParsingInput builder,
                                             ScopeDescriptor scope,
                                             int startIndex,
                                             ParseContext context)
    {
        context.RegisterDefinitions(builder.Properties.Values);
        context.RegisterDefinitions(builder.Argument);

        var longOptions = BuildAliasMap(builder.Properties.Values, static definition => definition.LongName);
        var shortOptions = BuildAliasMap(builder.Properties.Values, static definition => definition.ShortName);
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
                    if (!shortOptions.TryGetValue(shortOption.RawValue, out var shortProperty))
                    {
                        return new UnknownArgumentDetected(tokenText, null);
                    }

                    {
                        var propertyResult = BindProperty(arguments, index, shortProperty, context);

                        if (!propertyResult.Success)
                        {
                            return propertyResult.Result;
                        }

                        index = propertyResult.NextIndex;
                    }
                    break;
                case LongOptionToken longOption:
                    if (!longOptions.TryGetValue(longOption.RawValue, out var longProperty))
                    {
                        return new UnknownArgumentDetected(tokenText, null);
                    }

                    {
                        var propertyResult = BindProperty(arguments, index, longProperty, context);

                        if (!propertyResult.Success)
                        {
                            return propertyResult.Result;
                        }

                        index = propertyResult.NextIndex;
                    }
                    break;
                case ArgumentOrCommandToken argumentToken:
                    if (TryMatchSubcommand(builder, argumentToken.RawValue, out var subcommandKey, out var subcommand))
                    {
                        var argumentBindingResult = BindPendingArguments(builder.Argument, pendingArguments, context);

                        if (argumentBindingResult is not null)
                        {
                            return argumentBindingResult;
                        }

                        context.Commands[subcommandKey] = subcommand;

                        if (!builder.Subcommands.TryGetValue(subcommandKey, out var childBuilder))
                        {
                            return new GotError(new InvalidOperationException(
                                $"Subcommand '{subcommand.Information.Name.Value}' matched but does not have a builder."));
                        }

                        return CreateSubcommandResult(options,
                                                      arguments,
                                                      childBuilder,
                                                      new ScopeDescriptor(childBuilder, subcommand, scope),
                                                      index + 1,
                                                      context,
                                                      subcommand,
                                                      argumentToken);
                    }

                    pendingArguments.Add(NormalizeValueToken(argumentToken));
                    break;
                default:
                    return new UnknownArgumentDetected(tokenText, null);
            }
        }

        return BindPendingArguments(builder.Argument, pendingArguments, context);
    }

    private static ParsingResult ContinueParsing(ParsingOptions options,
                                                 ImmutableArray<Token> arguments,
                                                 ParsingInput builder,
                                                 ScopeDescriptor scope,
                                                 int startIndex,
                                                 ParseContext context)
    {
        var parseResult = ParseScope(options, arguments, builder, scope, startIndex, context);

        if (parseResult is Subcommand)
        {
            return parseResult;
        }

        return EmitBuilderDebug(options,
                                parseResult ?? FinalizeParse(options, context),
                                scope,
                                arguments,
                                startIndex,
                                context);
    }

    private static Subcommand CreateSubcommandResult(ParsingOptions options,
                                                     ImmutableArray<Token> arguments,
                                                     ParsingInput builder,
                                                     ScopeDescriptor scope,
                                                     int startIndex,
                                                     ParseContext context,
                                                     CommandDefinition definition,
                                                     Token triggerToken)
    {
        ParsingResult? cachedResult = null;
        var result = new Subcommand(definition, () => cachedResult ??= ContinueParsing(options,
            arguments,
            builder,
            scope,
            startIndex,
            context));

        return (Subcommand)EmitBuilderDebug(options,
                                            result,
                                            scope.Parent ?? scope,
                                            arguments,
                                            Math.Max(0, startIndex - 1),
                                            context,
                                            triggerToken,
                                            $"handoff to '{definition.Information.Name.Value}'");
    }

    private static ParsingResult EmitBuilderDebug(ParsingOptions options,
                                                  ParsingResult result,
                                                  ScopeDescriptor scope,
                                                  ImmutableArray<Token> arguments,
                                                  int startIndex,
                                                  ParseContext context,
                                                  Token? triggerToken = null,
                                                  string? summary = null)
    {
        return DebugOutput.Emit(options,
                                result,
                                new DebugContext(
                                    nameof(ParsingBuilder),
                                    Tokens: arguments,
                                    ActiveTokens: SliceTokens(arguments, startIndex),
                                    CommandPath: scope.CommandPath,
                                    TriggerToken: triggerToken,
                                    Summary: summary ?? BuildBuilderSummary(result, context)));
    }

    private static string? BuildBuilderSummary(ParsingResult result, ParseContext context)
    {
        if (result is not ParsingFinished)
        {
            return null;
        }

        var commands = context.Commands.Keys
            .OrderBy(static key => key, StringComparer.Ordinal)
            .ToImmutableArray();
        var definitions = context.Definitions
            .Select(static definition => definition.Information.Name.Value)
            .Where(static name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static name => name, StringComparer.Ordinal)
            .ToImmutableArray();

        var commandSummary = commands.IsDefaultOrEmpty ? "none" : string.Join(", ", commands);
        var definitionSummary = definitions.IsDefaultOrEmpty ? "none" : string.Join(", ", definitions);

        return $"commands={commandSummary}; definitions={definitionSummary}";
    }

    private static ImmutableArray<Token> SliceTokens(ImmutableArray<Token> arguments, int startIndex)
    {
        if (arguments.IsDefaultOrEmpty || startIndex >= arguments.Length)
        {
            return [];
        }

        return arguments.Skip(Math.Max(startIndex, 0)).ToImmutableArray();
    }

    private static Action CreateHelpAction(ParsingOptions options, ScopeDescriptor scope)
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

        builder.Append(options.ProgramNameStyle, options.Program.Name)
            .AppendLine($" {options.Program.Version}");

        if (!string.IsNullOrWhiteSpace(options.Program.Homepage))
        {
            builder.AppendLine(options.SecondaryTextStyle, options.Program.Homepage);
        }

        return builder.ToString();
    }

    private static string RenderHelpText(ParsingOptions options, ScopeDescriptor scope)
    {
        var text = new StyledStringBuilder(options.EnableStyle);
        var document = scope.Document;

        text.AppendLine(options.HelpTitleStyle, scope.DisplayName);

        if (!string.IsNullOrWhiteSpace(document.ConciseDescription))
        {
            text.AppendLine(options.DescriptionStyle, document.ConciseDescription);
        }

        text.AppendLine();
        text.AppendLine(options.UsageLabelStyle, "Usage");

        foreach (var usageLine in BuildUsageLines(scope))
        {
            text.Append("  ");
            AppendUsageLine(text, options, usageLine);
            text.AppendLine();
        }

        if (!string.IsNullOrWhiteSpace(document.HelpText))
        {
            text.AppendLine();
            text.AppendLine(options.DescriptionStyle, document.HelpText);
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
                .Append(options.DefinitionNameStyle, entry.Name);

            if (!string.IsNullOrWhiteSpace(entry.Description))
            {
                text.Append("  ")
                    .AppendLine(options.DescriptionStyle, entry.Description);
            }
            else
            {
                text.AppendLine();
            }
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
                    .AppendLine(options.DescriptionStyle, entry.Description);
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
        text.AppendLine(options.SectionHeaderStyle, title);
    }

    private static ImmutableArray<UsageLine> BuildUsageLines(ScopeDescriptor scope)
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
                               name);
    }

    private static PropertySectionEntry? CreatePropertyEntry(PropertyDefinition definition)
    {
        var visibleNames = GetVisiblePropertyNames(definition);
        var valueName = definition.Type == typeof(bool)
            ? null
            : GetVisibleDefinitionName(definition) ?? definition.Information.Name.Value;

        return visibleNames.Length == 0
            ? null
            : new PropertySectionEntry(visibleNames,
                                       valueName,
                                       definition.Information.Document.ConciseDescription,
                                       visibleNames[0],
                                       definition.PossibleValues);
    }

    private static SectionEntry? CreateSubcommandEntry(CommandDefinition definition)
    {
        var name = definition.Information.Name.Visible ? definition.Information.Name.Value : null;

        return string.IsNullOrWhiteSpace(name)
            ? null
            : new SectionEntry(name,
                               definition.Information.Document.ConciseDescription,
                               name);
    }

    private static bool HasVisibleAlias(PropertyDefinition definition)
    {
        return definition.LongName.Values.Any(static alias => alias.Visible)
            || definition.ShortName.Values.Any(static alias => alias.Visible);
    }

    private static bool HasVisibleCommandName(CommandDefinition definition)
    {
        return definition.Information.Name.Visible
            && !string.IsNullOrWhiteSpace(definition.Information.Name.Value);
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

        var core = definition.Arity.Maximum > 1 ? $"<{name}>..." : $"<{name}>";

        return new UsageToken(definition.Arity.Minimum == 0 ? $"[{core}]" : core, true);
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

    private static void AppendUsageLine(StyledStringBuilder text, ParsingOptions options, UsageLine usageLine)
    {
        text.Append(options.UsageCommandStyle, usageLine.CommandPath);

        foreach (var token in usageLine.Tokens)
        {
            text.Append(" ");
            text.Append(token.IsMetavar ? options.MetavarStyle : options.SecondaryTextStyle, token.Text);
        }
    }

    private static void AppendPropertySignature(StyledStringBuilder text,
                                                ParsingOptions options,
                                                PropertySectionEntry entry)
    {
        text.Append(options.OptionSignatureStyle, string.Join(", ", entry.VisibleNames));

        if (!string.IsNullOrWhiteSpace(entry.ValueName))
        {
            text.Append(" ")
                .Append(options.MetavarStyle, $"<{entry.ValueName}>");
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
            .Append(options.PossibleValuesLabelStyle, "Possible values:")
            .Append(" ");

        switch (possibleValues)
        {
            case DescripablePossibleValues description:
                text.AppendLine(options.PossibleValuesValueStyle, description.Description);
                return;
            default:
                if (TryFormatCountablePossibleValues(possibleValues, out var countableText))
                {
                    text.AppendLine(options.PossibleValuesValueStyle, countableText);
                    return;
                }

                text.AppendLine(options.PossibleValuesValueStyle, possibleValues.ToString() ?? string.Empty);
                return;
        }
    }

    private static bool TryFormatCountablePossibleValues(PossibleValues possibleValues, out string text)
    {
        text = string.Empty;

        if (!possibleValues.GetType().IsGenericType
            || possibleValues.GetType().GetGenericTypeDefinition() != typeof(CountablePossibleValues<>))
        {
            return false;
        }

        var candidatesProperty = possibleValues.GetType().GetProperty(nameof(CountablePossibleValues<int>.Candidates));

        if (candidatesProperty?.GetValue(possibleValues) is not IEnumerable candidates)
        {
            return false;
        }

        var values = new List<string>();

        foreach (var candidate in candidates)
        {
            values.Add(candidate?.ToString() ?? string.Empty);
        }

        text = string.Join(", ", values);
        return true;
    }

    private static ParsingResult FinalizeParse(ParsingOptions options, ParseContext context)
    {
        var explicitValues = ImmutableDictionary.CreateBuilder<TypedDefinition, object?>();

        foreach (var definition in context.Definitions)
        {
            if (definition is ArgumentDefinition argumentDefinition
                && context.GetCount(definition) < argumentDefinition.Arity.Minimum)
            {
                return new InvalidArgumentDetected(argumentDefinition.Information.Name.Value,
                                                   $"at least {argumentDefinition.Arity.Minimum} value(s)",
                                                   null);
            }

            if (!context.TryGetValues(definition, out var rawValues))
            {
                continue;
            }

            var materialized = ParseDefinitionValue(options, definition, rawValues);

            if (materialized is not ParsingFinished finished)
            {
                return materialized;
            }

            explicitValues[definition] = finished.UntypedResult;
        }

        var collection = new ParsingResultCollection(context.Commands.ToImmutable(), explicitValues.ToImmutable());

        foreach (var definition in context.Definitions)
        {
            object value;

            try
            {
                value = collection.GetValue(definition);
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
                validationResult = definition.Validation(value);
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

        return new ParsingFinished<IParsingResultCollection>(collection);
    }

    private static PropertyBindingResult BindProperty(ImmutableArray<Token> arguments,
                                                      int optionIndex,
                                                      PropertyDefinition property,
                                                      ParseContext context)
    {
        if (property.Type == typeof(bool))
        {
            context.SetBooleanProperty(property);
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

        context.AddPropertyValue(property, NormalizeValueToken(arguments[valueIndex]));
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
            $"Type '{definition.Type.FullName}' is not supported by {nameof(ParsingBuilder)}."));
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
        if (builder.SubcommandDefinitions.TryGetValue(candidate, out subcommand))
        {
            subcommandKey = candidate;
            return true;
        }

        foreach (var (key, value) in builder.SubcommandDefinitions)
        {
            if (value.Information.Name.Value != candidate)
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
                                                       ParseContext context)
    {
        if (pendingArguments.Count == 0)
        {
            return null;
        }

        var suffixMinimum = new long[arguments.Count + 1];
        var suffixMaximum = new long[arguments.Count + 1];

        for (var index = arguments.Count - 1; index >= 0; index--)
        {
            suffixMinimum[index] = suffixMinimum[index + 1] + arguments[index].Arity.Minimum;
            suffixMaximum[index] = suffixMaximum[index + 1] + arguments[index].Arity.Maximum;
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
            var maximumAssignable = Math.Min(currentArgument.Arity.Maximum,
                                             Math.Max(0L, remainingTokens - suffixMinimumAfterCurrent));

            if (hasFeasibleTokenCount)
            {
                var minimumAssignable = Math.Max(currentArgument.Arity.Minimum,
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
                context.AddArgumentValue(currentArgument, pendingArguments[tokenIndex++]);
            }

            remainingTokens -= assignCount;
        }

        if (remainingTokens > 0)
        {
            return new UnknownArgumentDetected(GetTokenText(pendingArguments[tokenIndex]), null);
        }

        return null;
    }

    private static Token NormalizeValueToken(Token token)
    {
        return new ArgumentOrCommandToken(GetTokenText(token));
    }

    private static string GetTokenText(Token token)
    {
        return token switch
        {
            ShortOptionToken shortOption => $"-{shortOption.RawValue}",
            LongOptionToken longOption => $"--{longOption.RawValue}",
            _ => token.RawValue
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

    private sealed class ParseContext
    {
        private readonly Dictionary<TypedDefinition, ImmutableArray<Token>.Builder> _values = [];
        private readonly HashSet<TypedDefinition> _definitionSet = [];

        public ImmutableDictionary<string, CommandDefinition>.Builder Commands { get; }
            = ImmutableDictionary.CreateBuilder<string, CommandDefinition>(StringComparer.Ordinal);

        public IList<TypedDefinition> Definitions { get; } = [];

        public void RegisterDefinitions(IEnumerable<PropertyDefinition> definitions)
        {
            foreach (var definition in definitions)
            {
                RegisterDefinition(definition);
            }
        }

        public void RegisterDefinitions(IEnumerable<ArgumentDefinition> definitions)
        {
            foreach (var definition in definitions)
            {
                RegisterDefinition(definition);
            }
        }

        public void SetBooleanProperty(PropertyDefinition definition)
        {
            RegisterDefinition(definition);
            _values[definition] = ImmutableArray.CreateBuilder<Token>(1);
            _values[definition].Add(new ArgumentOrCommandToken(bool.TrueString));
        }

        public void AddPropertyValue(PropertyDefinition definition, Token value)
        {
            RegisterDefinition(definition);
            GetOrCreateValues(definition).Add(value);
        }

        public void AddArgumentValue(ArgumentDefinition definition, Token value)
        {
            RegisterDefinition(definition);
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

        private void RegisterDefinition(TypedDefinition definition)
        {
            if (_definitionSet.Add(definition))
            {
                Definitions.Add(definition);
            }
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

    private static ImmutableDictionary<string, T>.Builder CreateBuilder<T>(
        ImmutableDictionary<string, T>? source)
    {
        var builder = ImmutableDictionary.CreateBuilder<string, T>(StringComparer.Ordinal);

        if (source is not null)
        {
            foreach (var (key, value) in source)
            {
                builder[key] = value;
            }
        }

        return builder;
    }

    private static ImmutableList<ArgumentDefinition>.Builder CreateArgumentBuilder(
        IEnumerable<ArgumentDefinition>? argument)
    {
        var builder = ImmutableList.CreateBuilder<ArgumentDefinition>();

        if (argument is not null)
        {
            builder.AddRange(argument);
        }

        return builder;
    }

    private sealed record ScopeDescriptor(ParsingInput Builder,
                                          CommandDefinition? Command,
                                          ScopeDescriptor? Parent)
    {
        public string DisplayName => Command?.Information.Name.Value ?? Builder.ParsingOptions.Program.Name;

        public Document Document => Command?.Information.Document ?? Builder.ParsingOptions.Program.Document;

        public string CommandPath
        {
            get
            {
                var segments = new Stack<string>();
                ScopeDescriptor? current = this;

                while (current is not null)
                {
                    segments.Push(current.Command?.Information.Name.Value ?? current.Builder.ParsingOptions.Program.Name);
                    current = current.Parent;
                }

                return string.Join(' ', segments);
            }
        }
    }

    private sealed record SectionEntry(string Name, string Description, string SortKey);

    private sealed record PropertySectionEntry(ImmutableArray<string> VisibleNames,
                                               string? ValueName,
                                               string Description,
                                               string SortKey,
                                               PossibleValues? PossibleValues);

    private readonly record struct UsageLine(string CommandPath, ImmutableArray<UsageToken> Tokens);

    private readonly record struct UsageToken(string Text, bool IsMetavar);
}
