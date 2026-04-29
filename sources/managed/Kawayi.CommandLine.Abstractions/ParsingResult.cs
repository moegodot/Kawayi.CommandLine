// Copyright (c) 2026 MoeGodot<me@kawayi.moe>.
// Licensed under the GNU Affero General Public License v3-or-later license.

namespace Kawayi.CommandLine.Abstractions;

public abstract record ParsingResult;

public sealed record Subcommand(ParsingFinished ParentCommand,
                                CommandDefinition Definition,
                                Func<ParsingResult> ContinueParseAction) : ParsingResult;

public abstract record ParsingFinished(object UntypedResult) : ParsingResult;

public sealed record ParsingFinished<T>(T Result) : ParsingFinished(Result);

public abstract record ShouldExit(bool Success) : ParsingResult;

public abstract record FlagDetected(Action FlagAction, string TriggerArgument) : ShouldExit(true);

public sealed record VersionFlagsDetected(Action FlagAction, string TriggerArgument) : FlagDetected(FlagAction, TriggerArgument);

public sealed record HelpFlagsDetected(Action FlagAction, string TriggerArgument) : FlagDetected(FlagAction, TriggerArgument);

public record GotError(Exception? Exception) : ShouldExit(false);

public sealed record InvalidArgumentDetected(string Argument, string Expect, Exception? Exception) : GotError(Exception);

public sealed record UnknownArgumentDetected(string UnknownArgument, Exception? Exception) : GotError(Exception);

public sealed record FailedValidation(string Argument, string Reason, Exception? Exception) : GotError(Exception);
