// Copyright (c) 2026 MoeGodot<me@kawayi.moe>.
// Licensed under the GNU Affero General Public License v3-or-later license.

namespace Kawayi.CommandLine.Abstractions;

/// <summary>
/// Represents program metadata used by the command-line runtime.
/// </summary>
/// <param name="Name">The program name.</param>
/// <param name="Document">The program documentation.</param>
/// <param name="Version">The program version.</param>
/// <param name="Homepage">The program homepage URL.</param>
public record ProgramInformation(string Name,
                                 Document Document,
                                 Version Version,
                                 string Homepage)
{
    /// <summary>
    /// Creates program metadata by deriving identity and version information from a type's assembly.
    /// </summary>
    /// <typeparam name="T">The type whose assembly metadata provides program identity.</typeparam>
    /// <param name="simpleDescription">The concise program description.</param>
    /// <param name="helpText">The full help text.</param>
    /// <param name="homePage">The program homepage URL.</param>
    /// <returns>The created program metadata.</returns>
    public static ProgramInformation Create<T>(string simpleDescription, string helpText, string homePage)
    {
        var name = typeof(T).Assembly.FullName ?? typeof(T).FullName ?? typeof(T).Name;
        var version = typeof(T).Assembly.GetName().Version ?? Version.Parse("1.0.0.0");
        var document = new Document(simpleDescription, helpText);
        return new(name, document, version, homePage);
    }
}
