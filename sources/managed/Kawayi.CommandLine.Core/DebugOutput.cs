// Copyright (c) 2026 MoeGodot<me@kawayi.moe>.
// Licensed under the GNU Affero General Public License v3-or-later license.

using System.Collections;
using System.Collections.Immutable;
using Kawayi.CommandLine.Abstractions;

namespace Kawayi.CommandLine.Core;

internal static class DebugOutput
{
    public static ParsingResult Emit(ParsingOptions options, ParsingResult result, DebugContext? context = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(result);

        if (!options.Debug)
        {
            return result;
        }

        options.Output.Write(Render(options, result, context));
        return result;
    }

    private static string Render(ParsingOptions options, ParsingResult result, DebugContext? context)
    {
        var text = new StyledStringBuilder(options.EnableStyle);

        text.AppendLine(options.DebugTitleStyle, "Debug Parse Result");
        AppendKeyValue(text, options, "Source", context?.Source ?? "Unknown");
        AppendKeyValue(text, options, "Result", result.GetType().Name);
        AppendState(text, options, result);

        if (!string.IsNullOrWhiteSpace(context?.CommandPath))
        {
            AppendKeyValue(text, options, "Scope", context.CommandPath);
        }

        if (context?.TargetType is not null)
        {
            AppendKeyValue(text,
                           options,
                           "Target type",
                           context.TargetType.FullName ?? context.TargetType.Name);
        }

        if (!string.IsNullOrWhiteSpace(context?.Expectation))
        {
            AppendKeyValue(text, options, "Expectation", context.Expectation);
        }

        if (context?.TriggerToken is not null)
        {
            AppendTokenValue(text, options, "Trigger token", context.TriggerToken);
        }

        if (!string.IsNullOrWhiteSpace(context?.SelectedToken))
        {
            AppendKeyValue(text, options, "Selected token", context.SelectedToken);
        }

        if (!context?.Tokens.IsDefaultOrEmpty ?? false)
        {
            AppendTokens(text, options, "Tokens", context!.Tokens);
        }

        if (!context?.ActiveTokens.IsDefaultOrEmpty ?? false)
        {
            AppendTokens(text, options, "Active tokens", context!.ActiveTokens);
        }

        switch (result)
        {
            case Subcommand subcommand:
                AppendKeyValue(text,
                               options,
                               "Command",
                               subcommand.Definition.Information.Name.Value);
                break;
            case FlagDetected flag:
                AppendKeyValue(text, options, "Trigger argument", flag.TriggerArgument);
                break;
            case UnknownArgumentDetected unknown:
                AppendKeyValue(text, options, "Unknown argument", unknown.UnknownArgument);
                break;
            case InvalidArgumentDetected invalid:
                AppendKeyValue(text, options, "Argument", invalid.Argument);
                AppendKeyValue(text, options, "Expect", invalid.Expect);
                break;
            case FailedValidation failed:
                AppendKeyValue(text, options, "Argument", failed.Argument);
                AppendKeyValue(text, options, "Reason", failed.Reason);
                break;
        }

        if (result is ParsingFinished finished)
        {
            AppendKeyValue(text,
                           options,
                           "Value type",
                           finished.UntypedResult?.GetType().FullName ?? "null");

            if (TryDescribeValue(finished.UntypedResult, out var valueSummary))
            {
                AppendKeyValue(text, options, "Value", valueSummary);
            }
        }

        if (!string.IsNullOrWhiteSpace(context?.Summary))
        {
            AppendKeyValue(text, options, "Summary", context.Summary);
        }

        if (result is GotError { Exception: not null } error)
        {
            AppendKeyValue(text,
                           options,
                           "Exception type",
                           error.Exception.GetType().FullName ?? error.Exception.GetType().Name);
            AppendKeyValue(text, options, "Exception message", error.Exception.Message);
        }

        text.AppendLine();
        return text.ToString();
    }

    private static void AppendState(StyledStringBuilder text, ParsingOptions options, ParsingResult result)
    {
        var (label, style) = result switch
        {
            Subcommand => ("deferred", options.DebugDeferredStyle),
            ShouldExit { Success: false } => ("failure", options.DebugFailureStyle),
            _ => ("success", options.DebugSuccessStyle)
        };

        text.Append(options.DebugLabelStyle, "State")
            .Append(": ")
            .AppendLine(style, label);
    }

    private static void AppendTokens(StyledStringBuilder text,
                                     ParsingOptions options,
                                     string label,
                                     ImmutableArray<Token> tokens)
    {
        text.Append(options.DebugLabelStyle, label)
            .Append(": ")
            .AppendLine(options.DebugTokenStyle, tokens.Length == 0
                ? "<empty>"
                : string.Join(" ", tokens.Select(FormatToken)));
    }

    private static void AppendTokenValue(StyledStringBuilder text,
                                         ParsingOptions options,
                                         string label,
                                         Token token)
    {
        text.Append(options.DebugLabelStyle, label)
            .Append(": ")
            .AppendLine(options.DebugTokenStyle, FormatToken(token));
    }

    private static void AppendKeyValue(StyledStringBuilder text,
                                       ParsingOptions options,
                                       string label,
                                       string value)
    {
        text.Append(options.DebugLabelStyle, label)
            .Append(": ")
            .AppendLine(options.DebugValueStyle, value);
    }

    private static string FormatToken(Token token)
    {
        return token switch
        {
            ShortOptionToken shortOption => $"-{shortOption.RawValue}",
            LongOptionToken longOption => $"--{longOption.RawValue}",
            _ => token.RawValue
        };
    }

    private static bool TryDescribeValue(object? value, out string description)
    {
        description = string.Empty;

        if (value is null)
        {
            description = "null";
            return true;
        }

        if (value is IParsingResultCollection collection)
        {
            var commandSummary = collection.Commands.Count == 0
                ? "none"
                : string.Join(", ", collection.Commands.Keys.OrderBy(static key => key, StringComparer.Ordinal));
            description = $"commands={commandSummary}";
            return true;
        }

        if (value is string stringValue)
        {
            description = stringValue;
            return true;
        }

        if (value is IEnumerable enumerable)
        {
            var items = new List<string>();

            foreach (var item in enumerable)
            {
                items.Add(item?.ToString() ?? "null");

                if (items.Count == 5)
                {
                    break;
                }
            }

            description = items.Count == 0
                ? "<empty>"
                : string.Join(", ", items);
            return true;
        }

        description = value.ToString() ?? string.Empty;
        return description.Length > 0;
    }
}

internal sealed record DebugContext(
    string Source,
    ImmutableArray<Token> Tokens = default,
    ImmutableArray<Token> ActiveTokens = default,
    string? CommandPath = null,
    Token? TriggerToken = null,
    Type? TargetType = null,
    string? Expectation = null,
    string? SelectedToken = null,
    string? Summary = null);
