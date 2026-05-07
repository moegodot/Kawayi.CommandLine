// Copyright (c) 2026 MoeGodot<me@kawayi.moe>.
// Licensed under the GNU Affero General Public License v3-or-later license.

namespace Kawayi.CommandLine.Abstractions;

/// <summary>
/// Represents a tokenized command-line value.
/// </summary>
/// <param name="Value">The token text.</param>
public abstract record Token(string Value);

/// <summary>
/// Represents a positional argument token or a subcommand token.
/// </summary>
/// <param name="Value">The token text.</param>
public sealed record ArgumentOrCommandToken(string Value) : Token(Value);

/// <summary>
/// Represent an option token.
/// </summary>
/// <param name="Value">The option name without the leading dash.</param>
public abstract record OptionToken(string Value) : Token(Value);

/// <summary>
/// Represents a short option token such as <c>-h</c>.
/// </summary>
/// <param name="Value">The option name without the leading dash.</param>
public sealed record ShortOptionToken(string Value) : OptionToken(Value);

/// <summary>
/// Represents a long option token and preserves an optional inline value from forms like `--<paramref name="Value"/>=<paramref name="InlineNextValue"/>`.
/// </summary>
public sealed record LongOptionToken(string Value, string? InlineNextValue = null) : OptionToken(Value);
