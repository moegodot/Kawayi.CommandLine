// Copyright (c) 2026 MoeGodot<me@kawayi.moe>.
// Licensed under the GNU Affero General Public License v3-or-later license.

namespace Kawayi.CommandLine.Abstractions;

public sealed record StyleTable
{
    public static StyleTable Default { get; } = new()
    {
        HelpTitleStyle = new Style(Color.Sky, Color.None, true, false, false),
        ProgramNameStyle = new Style(Color.Sky, Color.None, true, false, false),
        UsageLabelStyle = new Style(Color.Emerald, Color.None, true, false, false),
        UsageCommandStyle = new Style(Color.White, Color.None, true, false, false),
        SectionHeaderStyle = new Style(Color.Emerald, Color.None, true, false, false),
        OptionSignatureStyle = new Style(Color.White, Color.None, false, false, false),
        DefinitionNameStyle = new Style(Color.White, Color.None, false, false, false),
        MetavarStyle = new Style(Color.Amber, Color.None, false, false, true),
        DescriptionStyle = new Style(Color.Slate, Color.None, false, false, false),
        PossibleValuesLabelStyle = new Style(Color.Emerald, Color.None, false, false, false),
        PossibleValuesValueStyle = new Style(Color.Amber, Color.None, false, false, false),
        SecondaryTextStyle = new Style(Color.Slate, Color.None, false, false, false),
        DebugTitleStyle = new Style(Color.Sky, Color.None, true, false, false),
        DebugSuccessStyle = new Style(Color.Emerald, Color.None, true, false, false),
        DebugDeferredStyle = new Style(Color.Amber, Color.None, true, false, false),
        DebugFailureStyle = new Style(Color.Rose, Color.None, true, false, false),
        DebugLabelStyle = new Style(Color.Sky, Color.None, false, false, false),
        DebugValueStyle = new Style(Color.White, Color.None, false, false, false),
        DebugTokenStyle = new Style(Color.Amber, Color.None, false, false, true)
    };

    public required Style HelpTitleStyle { get; init; }
    public required Style ProgramNameStyle { get; init; }
    public required Style UsageLabelStyle { get; init; }
    public required Style UsageCommandStyle { get; init; }
    public required Style SectionHeaderStyle { get; init; }
    public required Style OptionSignatureStyle { get; init; }
    public required Style DefinitionNameStyle { get; init; }
    public required Style MetavarStyle { get; init; }
    public required Style DescriptionStyle { get; init; }
    public required Style PossibleValuesLabelStyle { get; init; }
    public required Style PossibleValuesValueStyle { get; init; }
    public required Style SecondaryTextStyle { get; init; }
    public required Style DebugTitleStyle { get; init; }
    public required Style DebugSuccessStyle { get; init; }
    public required Style DebugDeferredStyle { get; init; }
    public required Style DebugFailureStyle { get; init; }
    public required Style DebugLabelStyle { get; init; }
    public required Style DebugValueStyle { get; init; }
    public required Style DebugTokenStyle { get; init; }
}
