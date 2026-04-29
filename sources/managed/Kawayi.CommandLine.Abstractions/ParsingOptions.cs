// Copyright (c) 2026 MoeGodot<me@kawayi.moe>.
// Licensed under the GNU Affero General Public License v3-or-later license.

using System.Collections.Immutable;

namespace Kawayi.CommandLine.Abstractions;

public sealed record ParsingOptions(
    ProgramInformation Program,
    ImmutableHashSet<Token> VersionFlags,
    ImmutableHashSet<Token> HelpFlags,
    TextWriter Output,
    bool EnableStyle,
    bool Debug,
    StyleTable StyleTable
)
{
    public static ImmutableHashSet<Token> DefaultVersionFlags
    {
        get
        {
            if (field is not null)
            {
                return field;
            }

            field = ImmutableHashSet.CreateRange<Token>(
                [
                    new ShortOptionToken("V"),
                    new LongOptionToken("version"),
                    new ArgumentOrCommandToken("version")
                ]
            );

            return field;
        }
    } = null!;

    public static ImmutableHashSet<Token> DefaultHelpFlags
    {
        get
        {
            if (field is not null)
            {
                return field;
            }

            field = ImmutableHashSet.CreateRange<Token>(
                [
                    new ShortOptionToken("h"),
                    new LongOptionToken("help"),
                    new ArgumentOrCommandToken("help")
                ]
            );

            return field;
        }
    } = null!;

    private static bool? _defaultStyle = null;

    public static bool DefaultStyle
    {
        get
        {
            if (_defaultStyle is null)
            {
                var noColorEnv = Environment.GetEnvironmentVariable("NO_COLOR");
                var legacyNoColorEnv = Environment.GetEnvironmentVariable("NOCOLOR");
                var ciEnv = Environment.GetEnvironmentVariable("CI");

                var noColor = IsPresentEnvironmentValue(noColorEnv);
                var legacyNoColor = IsTruthyEnvironmentValue(legacyNoColorEnv);
                var ci = IsTruthyEnvironmentValue(ciEnv);

                _defaultStyle = !(noColor || legacyNoColor || ci);
            }

            return _defaultStyle.Value;
        }
    }

    private static bool? _defaultDebug = null;

    public static bool DefaultDebug
    {
        get
        {
            if (_defaultDebug is null)
            {
                _defaultDebug = IsTruthyEnvironmentValue(Environment.GetEnvironmentVariable("CLI_DEBUG"));
            }

            return _defaultDebug.Value;
        }
    }

    public ParsingOptions(ProgramInformation programInformation)
    :this(programInformation, DefaultVersionFlags, DefaultHelpFlags, Console.Out, DefaultStyle, DefaultDebug, StyleTable.Default)
    {
    }

    private static bool IsTruthyEnvironmentValue(string? value)
    {
        return (value ?? string.Empty).ToLowerInvariant() is "1" or "true" or "on" or "yes" or "y";
    }

    private static bool IsPresentEnvironmentValue(string? value)
    {
        return !string.IsNullOrEmpty(value);
    }
}
