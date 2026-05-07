// Copyright (c) 2026 GodotAsync<me@kawayi.moe>.
// Licensed under the GNU Affero General Public License v3-or-later license.

using System.Collections.Immutable;
using Kawayi.CommandLine.Core.Primitives;
using Kawayi.CommandLine.Abstractions;

namespace Kawayi.CommandLine.Core;

/// <summary>
/// Parses tokenized command-line input by using an immutable CLI schema snapshot.
/// </summary>
public sealed class CliSchemaParser
    : Abstractions.IParsable<CliSchema>
{
    /// <summary>
    /// Parses the supplied tokens by using the specified CLI schema.
    /// </summary>
    /// <param name="options">The parsing options for this operation.</param>
    /// <param name="arguments">The tokens to parse.</param>
    /// <param name="initialState">The schema snapshot to parse against.</param>
    /// <returns>The parsing result.</returns>
    public static ParsingResult CreateParsing(ParsingOptions options, ImmutableArray<Token> arguments, CliSchema initialState)
    {
        ArgumentNullException.ThrowIfNull(options);
        var (parseArguments, toProgramArguments) = SplitOptionTerminator(arguments);

        return ParseDeferred(options, parseArguments, toProgramArguments, initialState, null, null);
    }

    private static (ImmutableArray<Token> ParseArguments, ImmutableArray<Token> ToProgramArguments) SplitOptionTerminator(ImmutableArray<Token> arguments)
    {
        for (var index = 0; index < arguments.Length; index++)
        {
            if (arguments[index] is OptionTerminatorToken)
            {
                return (arguments[..index], arguments[(index + 1)..]);
            }
        }

        return (arguments, ImmutableArray<Token>.Empty);
    }

    private static ParsingResult ParseDeferred(ParsingOptions options,
                                               ImmutableArray<Token> arguments,
                                               ImmutableArray<Token> toProgramArguments,
                                               CliSchema schema,
                                               Cli? parentCommand,
                                               CommandDefinition? currentCommand)
    {
        return ParseScope(options,
                          arguments,
                          toProgramArguments,
                          schema,
                          parentCommand,
                          currentCommand,
                          deferSubcommands: true) switch
        {
            ScopeFinished finished => EmitSchemaDebug(options,
                                                      new ParsingFinished<Cli>(finished.Command),
                                                      arguments,
                                                      finished.Command,
                                                      "completed command scope"),
            ScopeDeferred deferred => EmitSchemaDebug(options,
                                                      new Subcommand(
                                                          new ParsingFinished<Cli>(deferred.ParentCommand),
                                                          deferred.Definition,
                                                          () => ContinueSubcommand(options, deferred)),
                                                      arguments,
                                                      deferred.ParentCommand,
                                                      $"deferred subcommand '{deferred.Definition.Information.Name.Value}'"),
            ScopeTerminated terminated => EmitSchemaDebug(options,
                                                          terminated.Result,
                                                          arguments,
                                                          BuildEmptyCommand(options, schema, parentCommand, currentCommand, toProgramArguments),
                                                          "terminated command scope"),
            _ => throw new InvalidOperationException("Unsupported schema parser state.")
        };
    }

    private static ParsingResult ContinueSubcommand(ParsingOptions options, ScopeDeferred deferred)
    {
        var childResult = ParseScope(options,
                                     deferred.RemainingTokens,
                                     deferred.ToProgramArguments,
                                     deferred.ChildSchema,
                                     deferred.ParentCommand,
                                     deferred.Definition,
                                     deferSubcommands: false);

        if (childResult is ScopeTerminated terminated)
        {
            return terminated.Result;
        }

        if (childResult is not ScopeFinished finished)
        {
            return new GotError(new InvalidOperationException("Nested deferred subcommand parsing is not expected in eager continuation mode."));
        }

        var parentWithChild = deferred.ParentCommand with
        {
            Subcommands = deferred.ParentCommand.Subcommands.SetItem(deferred.Definition, finished.Command)
        };

        return EmitSchemaDebug(options,
                               new ParsingFinished<Cli>(parentWithChild),
                               deferred.RemainingTokens,
                               parentWithChild,
                               $"completed subcommand '{deferred.Definition.Information.Name.Value}'");
    }

    private static ScopeResult ParseScope(ParsingOptions options,
                                          ImmutableArray<Token> arguments,
                                          ImmutableArray<Token> toProgramArguments,
                                          CliSchema schema,
                                          Cli? parentCommand,
                                          CommandDefinition? currentCommand,
                                          bool deferSubcommands)
    {
        var positionals = ImmutableArray.CreateBuilder<Token>();
        var propertyTokens = new Dictionary<PropertyDefinition, List<Token>>();

        for (var index = 0; index < arguments.Length;)
        {
            var token = arguments[index];
            WriteTrace(options, "Scan token", FormatToken(token), BuildCommandPath(parentCommand, currentCommand));

            if (TryCreateFlagResult(options, schema, token, out var flagResult))
            {
                return new ScopeTerminated(flagResult);
            }

            if (token is ArgumentOrCommandToken commandToken
                && token is not ArgumentToken
                && schema.SubcommandDefinitions.TryGetValue(commandToken, out var definition)
                && schema.Subcommands.TryGetValue(new ArgumentOrCommandToken(definition.Information.Name.Value), out var childSchema))
            {
                var parentResult = CompleteCurrentScope(options, schema, parentCommand, currentCommand, toProgramArguments, positionals.ToImmutable(), propertyTokens);

                if (parentResult is not ParsingFinished<Cli> parentFinished)
                {
                    return new ScopeTerminated(parentResult);
                }

                var remainingTokens = arguments[(index + 1)..];
                WriteTrace(options, "Subcommand", definition.Information.Name.Value, BuildCommandPath(parentFinished.Result, definition));

                if (deferSubcommands)
                {
                    return new ScopeDeferred(parentFinished.Result, definition, childSchema, remainingTokens, toProgramArguments);
                }

                var childResult = ParseScope(options,
                                             remainingTokens,
                                             toProgramArguments,
                                             childSchema,
                                             parentFinished.Result,
                                             definition,
                                             deferSubcommands: false);

                if (childResult is ScopeTerminated terminated)
                {
                    return terminated;
                }

                if (childResult is not ScopeFinished childFinished)
                {
                    return new ScopeTerminated(new GotError(new InvalidOperationException("Unexpected deferred state while parsing nested subcommands.")));
                }

                var parentWithChild = parentFinished.Result with
                {
                    Subcommands = parentFinished.Result.Subcommands.SetItem(definition, childFinished.Command)
                };

                return new ScopeFinished(parentWithChild);
            }

            if (token is OptionToken optionToken)
            {
                if (!TryGetProperty(schema, optionToken, out var property))
                {
                    if (TryConsumeDashPrefixedArgument(schema, positionals, optionToken))
                    {
                        index++;
                        continue;
                    }

                    return new ScopeTerminated(new UnknownArgumentDetected(FormatToken(optionToken), null));
                }

                index = CollectPropertyValues(options, arguments, schema, index, property, propertyTokens);
                continue;
            }

            positionals.Add(token);
            index++;
        }

        var result = CompleteCurrentScope(options, schema, parentCommand, currentCommand, toProgramArguments, positionals.ToImmutable(), propertyTokens);
        return result is ParsingFinished<Cli> finished
            ? new ScopeFinished(finished.Result)
            : new ScopeTerminated(result);
    }

    private static ParsingResult CompleteCurrentScope(ParsingOptions options,
                                                      CliSchema schema,
                                                      Cli? parentCommand,
                                                      CommandDefinition? currentCommand,
                                                      ImmutableArray<Token> toProgramArguments,
                                                      ImmutableArray<Token> positionalTokens,
                                                      Dictionary<PropertyDefinition, List<Token>> propertyTokens)
    {
        var argumentValues = ImmutableDictionary.CreateBuilder<ParameterDefinition, object?>();
        var propertyValues = ImmutableDictionary.CreateBuilder<PropertyDefinition, object?>();

        var argumentResult = ParseArguments(options, schema, positionalTokens, argumentValues);
        if (argumentResult is not null)
        {
            return argumentResult;
        }

        var propertyResult = ParseProperties(options, schema, propertyTokens, propertyValues);
        if (propertyResult is not null)
        {
            return propertyResult;
        }

        var explicitCommand = new Cli(options,
                                      schema,
                                      parentCommand,
                                      currentCommand,
                                      argumentValues.ToImmutableDictionary(static item => item.Key, static item => item.Value!),
                                      propertyValues.ToImmutableDictionary(static item => item.Key, static item => item.Value!),
                                      ImmutableDictionary<CommandDefinition, Cli>.Empty,
                                      toProgramArguments);

        var effectiveResult = ApplyEffectiveValues(options, explicitCommand, argumentValues, propertyValues);
        if (effectiveResult is not null)
        {
            return effectiveResult;
        }

        var command = explicitCommand with
        {
            Arguments = argumentValues.ToImmutableDictionary(static item => item.Key, static item => item.Value!),
            Properties = propertyValues.ToImmutableDictionary(static item => item.Key, static item => item.Value!)
        };

        WriteTrace(options,
                   "Complete scope",
                   $"arguments={command.Arguments.Count}, properties={command.Properties.Count}, subcommands={command.Subcommands.Count}",
                   BuildCommandPath(parentCommand, currentCommand));

        return new ParsingFinished<Cli>(command);
    }

    private static ParsingResult? ParseArguments(ParsingOptions options,
                                                 CliSchema schema,
                                                 ImmutableArray<Token> positionalTokens,
                                                 ImmutableDictionary<ParameterDefinition, object?>.Builder values)
    {
        var tokenIndex = 0;

        for (var argumentIndex = 0; argumentIndex < schema.Argument.Count; argumentIndex++)
        {
            var definition = schema.Argument[argumentIndex];
            var remainingTokenCount = positionalTokens.Length - tokenIndex;
            var laterMinimum = GetLaterMinimum(schema.Argument, argumentIndex + 1);
            var count = Math.Min(definition.ValueRange.Maximum, Math.Max(0, remainingTokenCount - laterMinimum));

            if (count < definition.ValueRange.Minimum)
            {
                return new InvalidArgumentDetected(definition.Information.Name.Value,
                                                   DescribeRange(definition.ValueRange),
                                                   null);
            }

            if (count == 0)
            {
                continue;
            }

            var activeTokens = positionalTokens[tokenIndex..(tokenIndex + count)];
            var parseResult = ParseTypedValue(options, activeTokens, definition.Type);

            if (parseResult is not ParsingFinished parsed)
            {
                return parseResult;
            }

            values[definition] = parsed.UntypedResult;
            tokenIndex += count;

            WriteTrace(options,
                       "Argument",
                       $"{definition.Information.Name.Value} <= {FormatTokens(activeTokens)}",
                       null);
        }

        if (tokenIndex < positionalTokens.Length)
        {
            return new UnknownArgumentDetected(FormatToken(positionalTokens[tokenIndex]), null);
        }

        return null;
    }

    private static ParsingResult? ParseProperties(ParsingOptions options,
                                                  CliSchema schema,
                                                  Dictionary<PropertyDefinition, List<Token>> propertyTokens,
                                                  ImmutableDictionary<PropertyDefinition, object?>.Builder values)
    {
        foreach (var definition in GetDistinctProperties(schema))
        {
            if (!propertyTokens.TryGetValue(definition, out var tokens))
            {
                continue;
            }

            if (tokens.Count < definition.NumArgs.Minimum || tokens.Count > definition.NumArgs.Maximum)
            {
                return new InvalidArgumentDetected(definition.Information.Name.Value,
                                                   DescribeRange(definition.NumArgs),
                                                   null);
            }

            var activeTokens = tokens.ToImmutableArray();
            var parseResult = ParseTypedValue(options, activeTokens, definition.Type);

            if (parseResult is not ParsingFinished parsed)
            {
                return parseResult;
            }

            values[definition] = parsed.UntypedResult;

            WriteTrace(options,
                       "Property",
                       $"{definition.Information.Name.Value} <= {FormatTokens(activeTokens)}",
                       null);
        }

        return null;
    }

    private static ParsingResult? ApplyEffectiveValues(ParsingOptions options,
                                                       Cli explicitCommand,
                                                       ImmutableDictionary<ParameterDefinition, object?>.Builder argumentValues,
                                                       ImmutableDictionary<PropertyDefinition, object?>.Builder propertyValues)
    {
        foreach (var definition in explicitCommand.Schema.Argument)
        {
            var result = ApplyEffectiveValue(explicitCommand, definition, argumentValues);
            if (result is not null)
            {
                return result;
            }
        }

        foreach (var definition in GetDistinctProperties(explicitCommand.Schema))
        {
            var result = ApplyEffectiveValue(explicitCommand, definition, propertyValues);
            if (result is not null)
            {
                return result;
            }
        }

        return null;
    }

    private static ParsingResult? ApplyEffectiveValue<TDefinition>(Cli explicitCommand,
                                                                   TDefinition definition,
                                                                   ImmutableDictionary<TDefinition, object?>.Builder values)
        where TDefinition : TypedDefinition
    {
        var hasExplicitValue = values.TryGetValue(definition, out var value);

        if (!hasExplicitValue && definition.DefaultValueFactory is not null)
        {
            value = definition.DefaultValueFactory();
            hasExplicitValue = true;
        }

        if (definition.Requirement && !hasExplicitValue)
        {
            return new InvalidArgumentDetected(definition.Information.Name.Value, "required value", null);
        }

        if (!hasExplicitValue)
        {
            return null;
        }

        if (definition.RequirementIfNull && value is null)
        {
            return new InvalidArgumentDetected(definition.Information.Name.Value, "non-null value", null);
        }

        if (definition.Validation is not null)
        {
            try
            {
                var reason = definition.Validation(value!);
                if (!string.IsNullOrWhiteSpace(reason))
                {
                    return new FailedValidation(definition.Information.Name.Value, reason, null);
                }
            }
            catch (Exception exception)
            {
                return new FailedValidation(definition.Information.Name.Value, exception.Message, exception);
            }
        }

        values[definition] = value;
        return null;
    }

    private static int CollectPropertyValues(ParsingOptions options,
                                             ImmutableArray<Token> arguments,
                                             CliSchema schema,
                                             int optionIndex,
                                             PropertyDefinition property,
                                             Dictionary<PropertyDefinition, List<Token>> propertyTokens)
    {
        var tokens = GetOrCreatePropertyTokenList(propertyTokens, property);
        var option = (OptionToken)arguments[optionIndex];
        var maximum = property.NumArgs.Maximum;

        if (property.Type == typeof(bool))
        {
            if (TryGetInlineValue(option, out var inlineValue))
            {
                tokens.Add(new ArgumentOrCommandToken(inlineValue));
                return optionIndex + 1;
            }

            if (optionIndex + 1 < arguments.Length
                && arguments[optionIndex + 1] is ArgumentOrCommandToken nextArgument
                && !schema.SubcommandDefinitions.ContainsKey(nextArgument)
                && bool.TryParse(nextArgument.Value, out _))
            {
                tokens.Add(nextArgument);
                return optionIndex + 2;
            }

            tokens.Add(new ArgumentOrCommandToken("true"));
            return optionIndex + 1;
        }

        if (TryGetInlineValue(option, out var inlineOptionValue))
        {
            tokens.Add(new ArgumentOrCommandToken(inlineOptionValue));
            WriteTrace(options, "Option", $"{FormatToken(option)} collected {tokens.Count} value(s)", null);
            return optionIndex + 1;
        }

        var index = optionIndex + 1;

        while (index < arguments.Length && tokens.Count < maximum)
        {
            var next = arguments[index];

            if (IsTerminatingFlag(options, next)
                || IsKnownOption(schema, next)
                || next is ArgumentOrCommandToken argument && argument is not ArgumentToken && schema.SubcommandDefinitions.ContainsKey(argument)
                || IsNumericLikeType(property.Type) && next is OptionToken && !CanParseAsValue(next, property.Type))
            {
                break;
            }

            tokens.Add(ConvertToValueToken(next));
            index++;
        }

        WriteTrace(options, "Option", $"{FormatToken(option)} collected {tokens.Count} value(s)", null);
        return index;
    }

    private static ParsingResult ParseTypedValue(ParsingOptions options, ImmutableArray<Token> tokens, Type targetType)
    {
        var effectiveTargetType = Nullable.GetUnderlyingType(targetType) ?? targetType;

        if (effectiveTargetType == typeof(string))
        {
            var value = tokens.IsDefaultOrEmpty ? string.Empty : tokens[^1].Value;
            return DebugOutput.Emit(options,
                                    new ParsingFinished<string>(value),
                                    new DebugContext(nameof(CliSchemaParser),
                                                     Tokens: tokens,
                                                     TargetType: effectiveTargetType,
                                                     Expectation: "string",
                                                     SelectedToken: value,
                                                     Summary: "materialized string value"));
        }

        if (TryCreateContainerType(effectiveTargetType, out var containerType))
        {
            return ContainerParser.CreateParsing(options, tokens, containerType);
        }

        if (effectiveTargetType.IsEnum)
        {
            return EnumParser.CreateParsing(options, tokens, effectiveTargetType, Enum.ToObject(effectiveTargetType, 0));
        }

        if (effectiveTargetType == typeof(bool))
        {
            return BooleanParser.CreateParsing(options, tokens, false);
        }

        if (effectiveTargetType == typeof(byte))
        {
            return NumberParser.CreateParsing(options, tokens, (byte)0);
        }

        if (effectiveTargetType == typeof(sbyte))
        {
            return NumberParser.CreateParsing(options, tokens, (sbyte)0);
        }

        if (effectiveTargetType == typeof(ushort))
        {
            return NumberParser.CreateParsing(options, tokens, (ushort)0);
        }

        if (effectiveTargetType == typeof(short))
        {
            return NumberParser.CreateParsing(options, tokens, (short)0);
        }

        if (effectiveTargetType == typeof(uint))
        {
            return NumberParser.CreateParsing(options, tokens, 0u);
        }

        if (effectiveTargetType == typeof(int))
        {
            return NumberParser.CreateParsing(options, tokens, 0);
        }

        if (effectiveTargetType == typeof(ulong))
        {
            return NumberParser.CreateParsing(options, tokens, 0UL);
        }

        if (effectiveTargetType == typeof(long))
        {
            return NumberParser.CreateParsing(options, tokens, 0L);
        }

        if (effectiveTargetType == typeof(float))
        {
            return FloatParser.CreateParsing(options, tokens, 0f);
        }

        if (effectiveTargetType == typeof(double))
        {
            return FloatParser.CreateParsing(options, tokens, 0d);
        }

        if (effectiveTargetType == typeof(decimal))
        {
            return FloatParser.CreateParsing(options, tokens, decimal.Zero);
        }

        if (effectiveTargetType == typeof(Guid))
        {
            return CommonParser.CreateParsing(options, tokens, Guid.Empty);
        }

        if (effectiveTargetType == typeof(Uri))
        {
            return CommonParser.CreateParsing(options, tokens, new Uri("https://placeholder.invalid"));
        }

        if (effectiveTargetType == typeof(DateTime))
        {
            return CommonParser.CreateParsing(options, tokens, default(DateTime));
        }

        if (effectiveTargetType == typeof(DateTimeOffset))
        {
            return CommonParser.CreateParsing(options, tokens, default(DateTimeOffset));
        }

        if (effectiveTargetType == typeof(DateOnly))
        {
            return CommonParser.CreateParsing(options, tokens, default(DateOnly));
        }

        if (effectiveTargetType == typeof(TimeOnly))
        {
            return CommonParser.CreateParsing(options, tokens, default(TimeOnly));
        }

        return new GotError(new NotSupportedException(
            $"Type '{targetType.FullName}' is not supported by {nameof(CliSchemaParser)}."));
    }

    private static bool TryCreateFlagResult(ParsingOptions options,
                                            CliSchema schema,
                                            Token token,
                                            out ParsingResult result)
    {
        if (IsVersionFlag(options, token))
        {
            result = new VersionFlagsDetected(() => WriteVersion(options), token);
            return true;
        }

        if (IsHelpFlag(options, token))
        {
            result = new HelpFlagsDetected(() => WriteHelp(options, schema), token);
            return true;
        }

        result = null!;
        return false;
    }

    private static void WriteVersion(ParsingOptions options)
    {
        var text = new StyledStringBuilder(options.EnableStyledOutput);
        text.Append(options.StyleTable.ProgramNameStyle, options.Program.Name)
            .Append(" ")
            .AppendLine(options.StyleTable.SecondaryTextStyle, options.Program.Version.ToString());
        options.Output.Write(text.ToString());
    }

    private static void WriteHelp(ParsingOptions options, CliSchema schema)
    {
        var text = new StyledStringBuilder(options.EnableStyledOutput);
        var styles = options.StyleTable;

        text.AppendLine(styles.HelpTitleStyle, options.Program.Name);

        if (!string.IsNullOrWhiteSpace(options.Program.Document.ConciseDescription))
        {
            text.AppendLine(styles.DescriptionStyle, options.Program.Document.ConciseDescription);
        }

        text.Append(styles.UsageLabelStyle, "Usage")
            .Append(": ")
            .Append(styles.ProgramNameStyle, options.Program.Name);

        foreach (var argument in schema.Argument.Where(static item => item.Information.Name.Visible))
        {
            text.Append(" ")
                .Append(styles.MetavarStyle, argument.ValueRange.Minimum == 0
                    ? $"[{argument.Information.Name.Value}]"
                    : $"<{argument.Information.Name.Value}>");
        }

        if (schema.Properties.Count > 0)
        {
            text.Append(" ")
                .Append(styles.UsageCommandStyle, "[options]");
        }

        if (schema.SubcommandDefinitions.Count > 0)
        {
            text.Append(" ")
                .Append(styles.UsageCommandStyle, "[command]");
        }

        text.AppendLine();

        AppendDefinitions(text,
                          styles,
                          "Arguments",
                          schema.Argument.Where(static item => item.Information.Name.Visible),
                          static item => item.Information.Name.Value,
                          static item => item.Information.Document);
        AppendDefinitions(text,
                          styles,
                          "Options",
                          GetDistinctProperties(schema).Where(static item => item.Information.Name.Visible),
                          FormatPropertySignature,
                          static item => item.Information.Document);
        AppendDefinitions(text,
                          styles,
                          "Commands",
                          GetDistinctSubcommands(schema).Where(static item => item.Information.Name.Visible),
                          static item => item.Information.Name.Value,
                          static item => item.Information.Document);

        options.Output.Write(text.ToString());
    }

    private static void AppendDefinitions<T>(StyledStringBuilder text,
                                             StyleTable styles,
                                             string title,
                                             IEnumerable<T> definitions,
                                             Func<T, string> nameSelector,
                                             Func<T, Document> documentSelector)
    {
        var materializedDefinitions = definitions.ToArray();

        if (materializedDefinitions.Length == 0)
        {
            return;
        }

        text.AppendLine()
            .AppendLine(styles.SectionHeaderStyle, title);

        foreach (var definition in materializedDefinitions)
        {
            var document = documentSelector(definition);
            text.Append("  ")
                .Append(styles.DefinitionNameStyle, nameSelector(definition));

            if (!string.IsNullOrWhiteSpace(document.ConciseDescription))
            {
                text.Append("  ")
                    .Append(styles.DescriptionStyle, document.ConciseDescription);
            }

            text.AppendLine();
        }
    }

    private static string FormatPropertySignature(PropertyDefinition definition)
    {
        var names = new List<string>();

        if (definition.Information.Name.Visible)
        {
            names.Add($"--{definition.Information.Name.Value}");
        }

        names.AddRange(definition.LongName.Values
            .Where(static item => item.Visible)
            .Select(static item => $"--{item.Value}"));
        names.AddRange(definition.ShortName.Values
            .Where(static item => item.Visible)
            .Select(static item => $"-{item.Value}"));

        var signature = names.Count == 0 ? $"--{definition.Information.Name.Value}" : string.Join(", ", names.Distinct(StringComparer.Ordinal));

        if (!string.IsNullOrWhiteSpace(definition.ValueName))
        {
            signature += $" <{definition.ValueName}>";
        }

        if (definition.PossibleValues is ICountablePossibleValues countable)
        {
            signature += $" ({string.Join("|", countable.Candidates.Cast<object?>().Select(static item => item?.ToString() ?? "null"))})";
        }
        else if (definition.PossibleValues is DescribablePossibleValues describable)
        {
            signature += $" ({describable.Description})";
        }

        return signature;
    }

    private static bool TryGetProperty(CliSchema schema, OptionToken token, out PropertyDefinition property)
    {
        return schema.Properties.TryGetValue(NormalizeOptionToken(token), out property!);
    }

    private static bool IsKnownOption(CliSchema schema, Token token)
    {
        return token is OptionToken option && TryGetProperty(schema, option, out _);
    }

    private static bool IsTerminatingFlag(ParsingOptions options, Token token)
    {
        return IsVersionFlag(options, token) || IsHelpFlag(options, token);
    }

    private static bool IsVersionFlag(ParsingOptions options, Token token)
    {
        return options.VersionFlags.Contains(NormalizeToken(token));
    }

    private static bool IsHelpFlag(ParsingOptions options, Token token)
    {
        return options.HelpFlags.Contains(NormalizeToken(token));
    }

    private static Token NormalizeToken(Token token)
    {
        return token is OptionToken option ? NormalizeOptionToken(option) : token;
    }

    private static OptionToken NormalizeOptionToken(OptionToken token)
    {
        return token switch
        {
            LongOptionToken longOption => new LongOptionToken(longOption.Value),
            ShortOptionToken shortOption => new ShortOptionToken(shortOption.Value),
            _ => token
        };
    }

    private static bool TryGetInlineValue(OptionToken token, out string value)
    {
        switch (token)
        {
            case LongOptionToken { InlineNextValue: not null } longOption:
                value = longOption.InlineNextValue;
                return true;
            case ShortOptionToken { InlineNextValue: not null } shortOption:
                value = shortOption.InlineNextValue;
                return true;
            default:
                value = string.Empty;
                return false;
        }
    }

    private static Token ConvertToValueToken(Token token)
    {
        return token switch
        {
            ArgumentOrCommandToken => token,
            ShortOptionToken { InlineNextValue: not null } shortOption => new ArgumentOrCommandToken($"-{shortOption.Value}{shortOption.InlineNextValue}"),
            ShortOptionToken shortOption => new ArgumentOrCommandToken($"-{shortOption.Value}"),
            LongOptionToken { InlineNextValue: not null } longOption => new ArgumentOrCommandToken($"--{longOption.Value}={longOption.InlineNextValue}"),
            LongOptionToken longOption => new ArgumentOrCommandToken($"--{longOption.Value}"),
            _ => new ArgumentOrCommandToken(token.Value)
        };
    }

    private static bool TryConsumeDashPrefixedArgument(CliSchema schema,
                                                       ImmutableArray<Token>.Builder positionals,
                                                       OptionToken optionToken)
    {
        if (!TryGetCurrentArgument(schema, positionals.Count, out var definition) ||
            !IsNumericLikeType(definition.Type) ||
            !CanParseAsValue(optionToken, definition.Type))
        {
            return false;
        }

        positionals.Add(ConvertToValueToken(optionToken));
        return true;
    }

    private static bool TryGetCurrentArgument(CliSchema schema, int receivedCount, out ParameterDefinition definition)
    {
        var remaining = receivedCount;

        foreach (var argument in schema.Argument)
        {
            if (remaining < argument.ValueRange.Maximum)
            {
                definition = argument;
                return true;
            }

            remaining -= argument.ValueRange.Maximum;
        }

        definition = null!;
        return false;
    }

    private static bool IsNumericLikeType(Type type)
    {
        var effectiveType = Nullable.GetUnderlyingType(type) ?? type;
        return effectiveType == typeof(byte)
            || effectiveType == typeof(sbyte)
            || effectiveType == typeof(ushort)
            || effectiveType == typeof(short)
            || effectiveType == typeof(uint)
            || effectiveType == typeof(int)
            || effectiveType == typeof(ulong)
            || effectiveType == typeof(long)
            || effectiveType == typeof(float)
            || effectiveType == typeof(double)
            || effectiveType == typeof(decimal);
    }

    private static bool CanParseAsValue(Token token, Type type)
    {
        var value = ConvertToValueToken(token).Value;
        var effectiveType = Nullable.GetUnderlyingType(type) ?? type;
        var integerStyles = NumberParser.DefaultNumberStyles;
        var floatStyles = FloatParser.DefaultNumberStyles;

        if (effectiveType == typeof(byte))
        {
            return byte.TryParse(value, integerStyles, null, out _);
        }

        if (effectiveType == typeof(sbyte))
        {
            return sbyte.TryParse(value, integerStyles, null, out _);
        }

        if (effectiveType == typeof(ushort))
        {
            return ushort.TryParse(value, integerStyles, null, out _);
        }

        if (effectiveType == typeof(short))
        {
            return short.TryParse(value, integerStyles, null, out _);
        }

        if (effectiveType == typeof(uint))
        {
            return uint.TryParse(value, integerStyles, null, out _);
        }

        if (effectiveType == typeof(int))
        {
            return int.TryParse(value, integerStyles, null, out _);
        }

        if (effectiveType == typeof(ulong))
        {
            return ulong.TryParse(value, integerStyles, null, out _);
        }

        if (effectiveType == typeof(long))
        {
            return long.TryParse(value, integerStyles, null, out _);
        }

        if (effectiveType == typeof(float))
        {
            return float.TryParse(value, floatStyles, null, out _);
        }

        if (effectiveType == typeof(double))
        {
            return double.TryParse(value, floatStyles, null, out _);
        }

        if (effectiveType == typeof(decimal))
        {
            return decimal.TryParse(value, floatStyles, null, out _);
        }

        return false;
    }

    private static bool TryCreateContainerType(Type targetType, out ContainerType containerType)
    {
        containerType = null!;

        if (!targetType.IsConstructedGenericType)
        {
            return false;
        }

        var genericDefinition = targetType.GetGenericTypeDefinition();
        var genericArguments = targetType.GetGenericArguments();

        if (genericDefinition == typeof(ImmutableDictionary<,>) || genericDefinition == typeof(ImmutableSortedDictionary<,>))
        {
            containerType = new ContainerType(targetType, genericArguments[0], genericArguments[1]);
            return true;
        }

        if (genericDefinition == typeof(ImmutableArray<>)
            || genericDefinition == typeof(ImmutableList<>)
            || genericDefinition == typeof(ImmutableQueue<>)
            || genericDefinition == typeof(ImmutableStack<>)
            || genericDefinition == typeof(ImmutableSortedSet<>)
            || genericDefinition == typeof(ImmutableHashSet<>))
        {
            containerType = new ContainerType(targetType, null, genericArguments[0]);
            return true;
        }

        return false;
    }

    private static int GetLaterMinimum(ImmutableList<ParameterDefinition> arguments, int startIndex)
    {
        var minimum = 0;

        for (var index = startIndex; index < arguments.Count; index++)
        {
            minimum = checked(minimum + arguments[index].ValueRange.Minimum);
        }

        return minimum;
    }

    private static List<Token> GetOrCreatePropertyTokenList(Dictionary<PropertyDefinition, List<Token>> propertyTokens,
                                                            PropertyDefinition property)
    {
        if (!propertyTokens.TryGetValue(property, out var tokens))
        {
            tokens = [];
            propertyTokens[property] = tokens;
        }

        return tokens;
    }

    private static IEnumerable<PropertyDefinition> GetDistinctProperties(CliSchema schema)
    {
        return schema.Properties.Values.Distinct();
    }

    private static IEnumerable<CommandDefinition> GetDistinctSubcommands(CliSchema schema)
    {
        return schema.SubcommandDefinitions.Values.Distinct();
    }

    private static Cli BuildEmptyCommand(ParsingOptions options,
                                         CliSchema schema,
                                         Cli? parentCommand,
                                         CommandDefinition? currentCommand,
                                         ImmutableArray<Token> toProgramArguments)
    {
        return new Cli(options,
                       schema,
                       parentCommand,
                       currentCommand,
                       ImmutableDictionary<ParameterDefinition, object>.Empty,
                       ImmutableDictionary<PropertyDefinition, object>.Empty,
                       ImmutableDictionary<CommandDefinition, Cli>.Empty,
                       toProgramArguments);
    }

    private static ParsingResult EmitSchemaDebug(ParsingOptions options,
                                                 ParsingResult result,
                                                 ImmutableArray<Token> arguments,
                                                 Cli command,
                                                 string summary)
    {
        return DebugOutput.Emit(options,
                                result,
                                new DebugContext(nameof(CliSchemaParser),
                                                 Tokens: arguments,
                                                 CommandPath: BuildCommandPath(command.ParentCommand, command.CurrentCommandDefinition),
                                                 TargetType: command.Schema.GeneratedFrom,
                                                 Summary: summary));
    }

    private static void WriteTrace(ParsingOptions options, string label, string value, string? commandPath)
    {
        if (!options.Debug)
        {
            return;
        }

        var text = new StyledStringBuilder(options.EnableStyledDebugOutput);
        text.AppendLine(options.StyleTable.DebugTitleStyle, "Debug Parse Trace")
            .Append(options.StyleTable.DebugLabelStyle, label)
            .Append(": ")
            .AppendLine(options.StyleTable.DebugValueStyle, value);

        if (!string.IsNullOrWhiteSpace(commandPath))
        {
            text.Append(options.StyleTable.DebugLabelStyle, "Scope")
                .Append(": ")
                .AppendLine(options.StyleTable.DebugValueStyle, commandPath);
        }

        options.DebugOutput.Write(text.ToString());
    }

    private static string BuildCommandPath(Cli? parentCommand, CommandDefinition? currentCommand)
    {
        var commands = new Stack<string>();

        if (currentCommand is not null)
        {
            commands.Push(currentCommand.Information.Name.Value);
        }

        var current = parentCommand;

        while (current is not null)
        {
            if (current.CurrentCommandDefinition is not null)
            {
                commands.Push(current.CurrentCommandDefinition.Information.Name.Value);
            }

            current = current.ParentCommand;
        }

        return commands.Count == 0 ? "<root>" : string.Join(" -> ", commands);
    }

    private static string DescribeRange(ValueRange range)
    {
        if (range.Minimum == range.Maximum)
        {
            return $"{range.Minimum} value(s)";
        }

        return range.Maximum == int.MaxValue
            ? $"at least {range.Minimum} value(s)"
            : $"{range.Minimum}..{range.Maximum} value(s)";
    }

    private static string FormatTokens(ImmutableArray<Token> tokens)
    {
        return tokens.IsDefaultOrEmpty ? "<empty>" : string.Join(" ", tokens.Select(FormatToken));
    }

    private static string FormatToken(Token token)
    {
        return token switch
        {
            ShortOptionToken { InlineNextValue: not null } shortOption => $"-{shortOption.Value}{shortOption.InlineNextValue}",
            ShortOptionToken shortOption => $"-{shortOption.Value}",
            LongOptionToken { InlineNextValue: not null } longOption => $"--{longOption.Value}={longOption.InlineNextValue}",
            LongOptionToken longOption => $"--{longOption.Value}",
            _ => token.Value
        };
    }

    private abstract record ScopeResult;

    private sealed record ScopeFinished(Cli Command) : ScopeResult;

    private sealed record ScopeDeferred(Cli ParentCommand,
                                        CommandDefinition Definition,
                                        CliSchema ChildSchema,
                                        ImmutableArray<Token> RemainingTokens,
                                        ImmutableArray<Token> ToProgramArguments) : ScopeResult;

    private sealed record ScopeTerminated(ParsingResult Result) : ScopeResult;
}
