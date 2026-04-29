// Copyright (c) 2026 MoeGodot<me@kawayi.moe>.
// Licensed under the GNU Affero General Public License v3-or-later license.

namespace Kawayi.CommandLine.Abstractions;

public abstract record Token(string Value);

public sealed record ArgumentOrCommandToken(string Value):Token(Value);

public sealed record ShortOptionToken(string Value):Token(Value);

/// <summary>
/// Represents a long option token and preserves an optional inline value from forms like `--<paramref name="Value"/>=<paramref name="InlineNextValue"/>`.
/// </summary>
public sealed record LongOptionToken(string Value, string? InlineNextValue = null):Token(Value);
