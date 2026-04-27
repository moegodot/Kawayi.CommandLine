// Copyright (c) 2026 MoeGodot<me@kawayi.moe>.
// Licensed under the GNU Affero General Public License v3-or-later license.

namespace Kawayi.CommandLine.Abstractions;

public abstract record Token(string RawValue);

public sealed record ArgumentOrCommandToken(string RawValue):Token(RawValue);

public sealed record ShortOptionToken(string RawValue):Token(RawValue);

public sealed record LongOptionToken(string RawValue):Token(RawValue);
