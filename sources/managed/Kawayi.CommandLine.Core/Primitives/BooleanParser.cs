// Copyright (c) 2026 MoeGodot<me@kawayi.moe>.
// Licensed under the GNU Affero General Public License v3-or-later license.

using System.Collections.Immutable;
using Kawayi.CommandLine.Abstractions;
using Kawayi.CommandLine.Core;

namespace Kawayi.CommandLine.Core.Primitives;

/// <summary>
/// Parses boolean values from command-line tokens.
/// </summary>
public sealed class BooleanParser : Abstractions.IParsable<bool>
{
    /// <summary>
    /// Parses a boolean value from the supplied tokens.
    /// </summary>
    /// <param name="options">The parsing options for this operation.</param>
    /// <param name="arguments">The tokens to parse.</param>
    /// <param name="initialState">The fallback value used when no token is supplied.</param>
    /// <returns>The parsing result.</returns>
    public static ParsingResult CreateParsing(ParsingOptions options,
                                              ImmutableArray<Token> arguments,
                                              bool initialState)
    {
        var selectedToken = arguments.IsDefaultOrEmpty ? null : arguments[^1].Value;

        if (arguments.IsDefaultOrEmpty)
        {
            return DebugOutput.Emit(options,
                                    new ParsingFinished<bool>(initialState),
                                    new DebugContext(nameof(BooleanParser),
                                                     Tokens: arguments,
                                                     TargetType: typeof(bool),
                                                     Expectation: "bool"));
        }

        var token = arguments[^1];

        if (bool.TryParse(token.Value, out var parsedValue))
        {
            return DebugOutput.Emit(options,
                                    new ParsingFinished<bool>(parsedValue),
                                    new DebugContext(nameof(BooleanParser),
                                                     Tokens: arguments,
                                                     TargetType: typeof(bool),
                                                     Expectation: "bool",
                                                     SelectedToken: selectedToken));
        }

        return DebugOutput.Emit(options,
                                new InvalidArgumentDetected(token.Value, "bool", null),
                                new DebugContext(nameof(BooleanParser),
                                                 Tokens: arguments,
                                                 TargetType: typeof(bool),
                                                 Expectation: "bool",
                                                 SelectedToken: selectedToken));
    }
}
