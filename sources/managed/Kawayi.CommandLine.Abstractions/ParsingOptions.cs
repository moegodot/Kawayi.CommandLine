// Copyright (c) 2026 MoeGodot<me@kawayi.moe>.
// Licensed under the GNU Affero General Public License v3-or-later license.

using System.Collections.Immutable;

namespace Kawayi.CommandLine.Abstractions;

/// <summary>
/// Defines runtime options that affect parsing and command-line rendering.
/// </summary>
/// <param name="Program">The program metadata to expose to users.</param>
/// <param name="VersionFlags">The tokens that trigger version output.</param>
/// <param name="HelpFlags">The tokens that trigger help output.</param>
/// <param name="Output">The text writer used for user-visible output.</param>
/// <param name="EnableStyle">Whether ANSI styling is enabled.</param>
/// <param name="Debug">Whether debug output is enabled.</param>
/// <param name="StyleTable">The styles used for formatted output.</param>
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
    /// <summary>
    /// Gets the default version flags recognized by the parser.
    /// </summary>
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

    /// <summary>
    /// Gets the default help flags recognized by the parser.
    /// </summary>
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

    /// <summary>
    /// Gets whether styled output should be enabled by default for the current environment.
    /// </summary>
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

    /// <summary>
    /// Gets whether debug output should be enabled by default for the current environment.
    /// </summary>
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

    /// <summary>
    /// Initializes a new instance by using default flags, output, style, and debug settings.
    /// </summary>
    /// <param name="programInformation">The program metadata to expose to users.</param>
    public ParsingOptions(ProgramInformation programInformation)
    : this(programInformation, DefaultVersionFlags, DefaultHelpFlags, Console.Out, DefaultStyle, DefaultDebug, StyleTable.Default)
    {
    }

    /// <summary>
    /// Creates parsing options by deriving program metadata from the specified type.
    /// </summary>
    /// <typeparam name="T">The type whose assembly metadata provides program identity.</typeparam>
    /// <param name="simpleDescription">The concise program description.</param>
    /// <param name="helpText">The full help text.</param>
    /// <param name="homePage">The program homepage URL.</param>
    /// <returns>The created parsing options.</returns>
    public static ParsingOptions Create<T>(string simpleDescription, string helpText, string homePage)
    {
        return new(ProgramInformation.Create<T>(simpleDescription, helpText, homePage));
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
