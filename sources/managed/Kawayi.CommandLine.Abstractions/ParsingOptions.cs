// Copyright (c) 2026 MoeGodot<me@kawayi.moe>.
// Licensed under the GNU Affero General Public License v3-or-later license.

using System.Collections.Immutable;

namespace Kawayi.CommandLine.Abstractions;

public sealed record ParsingOptions(
    ProgramInformation Program,
    ImmutableHashSet<Token> VersionFlags,
    ImmutableHashSet<Token> HelpFlags,
    TextWriter Output
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

    public ParsingOptions(ProgramInformation programInformation)
    :this(programInformation, DefaultVersionFlags,DefaultHelpFlags,Console.Out)
    {
    }
}
