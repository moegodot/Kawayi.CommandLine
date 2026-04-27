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
    bool Debug
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
                var nocolorEnv = Environment.GetEnvironmentVariable("NOCOLOR");
                var ciEnv = Environment.GetEnvironmentVariable("CI");

                var nocolor = IsTruthyEnvironmentValue(nocolorEnv);
                var ci = IsTruthyEnvironmentValue(ciEnv);

                _defaultStyle = !(nocolor || ci);
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

    public Style HelpTitleStyle { get; init; } = new(Color.Sky, Color.None, true, false, false);
    public Style ProgramNameStyle { get; init; } = new(Color.Sky, Color.None, true, false, false);
    public Style UsageLabelStyle { get; init; } = new(Color.Emerald, Color.None, true, false, false);
    public Style UsageCommandStyle { get; init; } = new(Color.White, Color.None, true, false, false);
    public Style SectionHeaderStyle { get; init; } = new(Color.Emerald, Color.None, true, false, false);
    public Style OptionSignatureStyle { get; init; } = new(Color.White, Color.None, false, false, false);
    public Style DefinitionNameStyle { get; init; } = new(Color.White, Color.None, false, false, false);
    public Style MetavarStyle { get; init; } = new(Color.Amber, Color.None, false, false, true);
    public Style DescriptionStyle { get; init; } = new(Color.Slate, Color.None, false, false, false);
    public Style PossibleValuesLabelStyle { get; init; } = new(Color.Emerald, Color.None, false, false, false);
    public Style PossibleValuesValueStyle { get; init; } = new(Color.Amber, Color.None, false, false, false);
    public Style SecondaryTextStyle { get; init; } = new(Color.Slate, Color.None, false, false, false);
    public Style DebugTitleStyle { get; init; } = new(Color.Sky, Color.None, true, false, false);
    public Style DebugSuccessStyle { get; init; } = new(Color.Emerald, Color.None, true, false, false);
    public Style DebugDeferredStyle { get; init; } = new(Color.Amber, Color.None, true, false, false);
    public Style DebugFailureStyle { get; init; } = new(Color.Rose, Color.None, true, false, false);
    public Style DebugLabelStyle { get; init; } = new(Color.Sky, Color.None, false, false, false);
    public Style DebugValueStyle { get; init; } = new(Color.White, Color.None, false, false, false);
    public Style DebugTokenStyle { get; init; } = new(Color.Amber, Color.None, false, false, true);

    public ParsingOptions(ProgramInformation programInformation)
    :this(programInformation, DefaultVersionFlags,DefaultHelpFlags,Console.Out,DefaultStyle,DefaultDebug)
    {
    }

    private static bool IsTruthyEnvironmentValue(string? value)
    {
        return (value ?? string.Empty).ToLowerInvariant() is "1" or "true" or "on" or "yes" or "y";
    }
}
