// Copyright (c) 2026 MoeGodot<me@kawayi.moe>.
// Licensed under the GNU Affero General Public License v3-or-later license.

namespace Kawayi.CommandLine.Abstractions;

public record ProgramInformation(string Name,
                                 Document Document,
                                 Version Version,
                                 string Homepage)
{
    public static ProgramInformation Create<T>(string simpleDescription, string helpText, string homePage)
    {
        var name = typeof(T).Assembly.FullName ?? typeof(T).FullName ?? typeof(T).Name;
        var version = typeof(T).Assembly.GetName().Version ?? Version.Parse("1.0.0.0");
        var document = new Document(simpleDescription, helpText);
        return new(name, document, version, homePage);
    }
}
