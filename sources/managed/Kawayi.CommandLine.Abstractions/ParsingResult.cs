// Copyright (c) 2026 MoeGodot<me@kawayi.moe>.
// Licensed under the GNU Affero General Public License v3-or-later license.

namespace Kawayi.CommandLine.Abstractions;

/// <summary>
/// Represents the outcome of a parsing operation.
/// </summary>
public abstract record ParsingResult;

/// <summary>
/// Represents a subcommand handoff that can continue parsing on demand.
/// </summary>
/// <param name="ParentCommand">The already-parsed parent command result.</param>
/// <param name="Definition">The matched subcommand definition.</param>
/// <param name="ContinueParseAction">The deferred action that continues parsing.</param>
public sealed record Subcommand(ParsingFinished ParentCommand,
                                CommandDefinition Definition,
                                Func<ParsingResult> ContinueParseAction) : ParsingResult;

/// <summary>
/// Represents a completed parsing operation that produced a result object.
/// </summary>
/// <param name="UntypedResult">The untyped parsed result.</param>
public abstract record ParsingFinished(object? UntypedResult) : ParsingResult;

/// <summary>
/// Represents a completed parsing operation with a strongly typed result.
/// </summary>
/// <typeparam name="T">The result type.</typeparam>
/// <param name="Result">The typed parsed result.</param>
public sealed record ParsingFinished<T>(T Result) : ParsingFinished(Result);

/// <summary>
/// Represents a parsing outcome that should terminate command execution.
/// </summary>
/// <param name="Success">Whether the operation should be treated as successful.</param>
public abstract record ShouldExit(bool Success) : ParsingResult;

/// <summary>
/// Represents detection of a terminating flag.
/// </summary>
/// <param name="FlagAction">The action to execute for the flag.</param>
/// <param name="TriggerArgument">The argument that triggered the flag.</param>
public abstract record FlagDetected(Action FlagAction, Token TriggerArgument) : ShouldExit(true);

/// <summary>
/// Represents detection of a version flag.
/// </summary>
/// <param name="FlagAction">The action to execute for the version flag.</param>
/// <param name="TriggerArgument">The argument that triggered the flag.</param>
public sealed record VersionFlagsDetected(Action FlagAction, Token TriggerArgument) : FlagDetected(FlagAction, TriggerArgument);

/// <summary>
/// Represents detection of a help flag.
/// </summary>
/// <param name="FlagAction">The action to execute for the help flag.</param>
/// <param name="TriggerArgument">The argument that triggered the flag.</param>
public sealed record HelpFlagsDetected(Action FlagAction, Token TriggerArgument) : FlagDetected(FlagAction, TriggerArgument);

/// <summary>
/// Represents a parsing failure.
/// </summary>
/// <param name="Exception">The underlying exception when one exists.</param>
public record GotError(Exception? Exception) : ShouldExit(false);

/// <summary>
/// Represents a token that could not be parsed as the expected argument type.
/// </summary>
/// <param name="Argument">The supplied argument value.</param>
/// <param name="Expect">The expected value description.</param>
/// <param name="Exception">The underlying exception when one exists.</param>
public sealed record InvalidArgumentDetected(string Argument, string Expect, Exception? Exception) : GotError(Exception);

/// <summary>
/// Represents a token that did not match any known argument or option.
/// </summary>
/// <param name="UnknownArgument">The unmatched argument value.</param>
/// <param name="Exception">The underlying exception when one exists.</param>
public sealed record UnknownArgumentDetected(string UnknownArgument, Exception? Exception) : GotError(Exception);

/// <summary>
/// Represents a value that failed custom validation.
/// </summary>
/// <param name="Argument">The argument value that failed validation.</param>
/// <param name="Reason">The validation failure reason.</param>
/// <param name="Exception">The underlying exception when one exists.</param>
public sealed record FailedValidation(string Argument, string Reason, Exception? Exception) : GotError(Exception);
