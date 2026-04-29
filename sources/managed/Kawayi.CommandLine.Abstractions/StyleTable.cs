// Copyright (c) 2026 MoeGodot<me@kawayi.moe>.
// Licensed under the GNU Affero General Public License v3-or-later license.

namespace Kawayi.CommandLine.Abstractions;

/// <summary>
/// Collects the styles used by command-line rendering.
/// </summary>
public sealed record StyleTable
{
    /// <summary>
    /// Gets the default style table.
    /// </summary>
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

    /// <summary>
    /// Gets the style used for the top-level help title.
    /// </summary>
    public required Style HelpTitleStyle { get; init; }
    /// <summary>
    /// Gets the style used for the program name.
    /// </summary>
    public required Style ProgramNameStyle { get; init; }
    /// <summary>
    /// Gets the style used for the usage label.
    /// </summary>
    public required Style UsageLabelStyle { get; init; }
    /// <summary>
    /// Gets the style used for usage command segments.
    /// </summary>
    public required Style UsageCommandStyle { get; init; }
    /// <summary>
    /// Gets the style used for section headers.
    /// </summary>
    public required Style SectionHeaderStyle { get; init; }
    /// <summary>
    /// Gets the style used for option signatures.
    /// </summary>
    public required Style OptionSignatureStyle { get; init; }
    /// <summary>
    /// Gets the style used for definition names.
    /// </summary>
    public required Style DefinitionNameStyle { get; init; }
    /// <summary>
    /// Gets the style used for metavariables.
    /// </summary>
    public required Style MetavarStyle { get; init; }
    /// <summary>
    /// Gets the style used for descriptions.
    /// </summary>
    public required Style DescriptionStyle { get; init; }
    /// <summary>
    /// Gets the style used for the possible-values label.
    /// </summary>
    public required Style PossibleValuesLabelStyle { get; init; }
    /// <summary>
    /// Gets the style used for possible-values text.
    /// </summary>
    public required Style PossibleValuesValueStyle { get; init; }
    /// <summary>
    /// Gets the style used for secondary text.
    /// </summary>
    public required Style SecondaryTextStyle { get; init; }
    /// <summary>
    /// Gets the style used for debug titles.
    /// </summary>
    public required Style DebugTitleStyle { get; init; }
    /// <summary>
    /// Gets the style used for successful debug states.
    /// </summary>
    public required Style DebugSuccessStyle { get; init; }
    /// <summary>
    /// Gets the style used for deferred debug states.
    /// </summary>
    public required Style DebugDeferredStyle { get; init; }
    /// <summary>
    /// Gets the style used for failed debug states.
    /// </summary>
    public required Style DebugFailureStyle { get; init; }
    /// <summary>
    /// Gets the style used for debug labels.
    /// </summary>
    public required Style DebugLabelStyle { get; init; }
    /// <summary>
    /// Gets the style used for debug values.
    /// </summary>
    public required Style DebugValueStyle { get; init; }
    /// <summary>
    /// Gets the style used for debug tokens.
    /// </summary>
    public required Style DebugTokenStyle { get; init; }
}
