// Copyright (c) 2026 GodotAsync<me@kawayi.moe>.
// Licensed under the GNU Affero General Public License v3-or-later license.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Kawayi.CommandLine.Abstractions;
using Kawayi.CommandLine.Extensions;

namespace Kawayi.CommandLine.Core.Tests;

public sealed class ParsingBuilderExtensionsTests
{
    private static readonly ParsingOptions OptionsA = CreateOptions("app-a");
    private static readonly ParsingOptions OptionsB = CreateOptions("app-b");

    [Test]
    public async Task Merge_Two_Empty_Builders_Returns_Empty_Builder()
    {
        var first = new ParsingBuilder(OptionsA);
        var second = new ParsingBuilder(OptionsA);

        var merged = first.Merge(second);

        await Assert.That(merged.ParsingOptions).IsEqualTo(OptionsA);
        await Assert.That(merged.Properties.Count).IsEqualTo(0);
        await Assert.That(merged.SubcommandDefinitions.Count).IsEqualTo(0);
        await Assert.That(merged.Argument.Count).IsEqualTo(0);
        await Assert.That(merged.Subcommands.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Merge_First_Empty_Second_Has_Properties_Returns_Second_Properties()
    {
        var first = new ParsingBuilder(OptionsA);
        var verbose = CreateProperty("verbose", typeof(bool));
        var second = new ParsingBuilder(
            OptionsA,
            properties: ImmutableDictionary<string, PropertyDefinition>.Empty.Add("verbose", verbose));

        var merged = first.Merge(second);

        await Assert.That(merged.Properties.Count).IsEqualTo(1);
        await Assert.That(merged.Properties["verbose"]).IsEqualTo(verbose);
    }

    [Test]
    public async Task Merge_First_Has_Properties_Second_Empty_Preserves_First_Properties()
    {
        var verbose = CreateProperty("verbose", typeof(bool));
        var first = new ParsingBuilder(
            OptionsA,
            properties: ImmutableDictionary<string, PropertyDefinition>.Empty.Add("verbose", verbose));
        var second = new ParsingBuilder(OptionsA);

        var merged = first.Merge(second);

        await Assert.That(merged.Properties.Count).IsEqualTo(1);
        await Assert.That(merged.Properties["verbose"]).IsEqualTo(verbose);
    }

    [Test]
    public async Task Merge_Non_Conflicting_Properties_Contains_Both()
    {
        var verbose = CreateProperty("verbose", typeof(bool));
        var format = CreateProperty("format", typeof(string));
        var first = new ParsingBuilder(
            OptionsA,
            properties: ImmutableDictionary<string, PropertyDefinition>.Empty.Add("verbose", verbose));
        var second = new ParsingBuilder(
            OptionsA,
            properties: ImmutableDictionary<string, PropertyDefinition>.Empty.Add("format", format));

        var merged = first.Merge(second);

        await Assert.That(merged.Properties.Count).IsEqualTo(2);
        await Assert.That(merged.Properties["verbose"]).IsEqualTo(verbose);
        await Assert.That(merged.Properties["format"]).IsEqualTo(format);
    }

    [Test]
    public async Task Merge_Conflicting_Properties_With_Override_Uses_Second()
    {
        var firstVerbose = CreateProperty("verbose", typeof(bool));
        var secondVerbose = CreateProperty("verbose", typeof(string));
        var first = new ParsingBuilder(
            OptionsA,
            properties: ImmutableDictionary<string, PropertyDefinition>.Empty.Add("verbose", firstVerbose));
        var second = new ParsingBuilder(
            OptionsA,
            properties: ImmutableDictionary<string, PropertyDefinition>.Empty.Add("verbose", secondVerbose));

        var merged = first.Merge(second, @override: true);

        await Assert.That(merged.Properties.Count).IsEqualTo(1);
        await Assert.That(merged.Properties["verbose"]).IsEqualTo(secondVerbose);
    }

    [Test]
    public async Task Merge_Conflicting_Properties_Without_Override_Throws()
    {
        var firstVerbose = CreateProperty("verbose", typeof(bool));
        var secondVerbose = CreateProperty("verbose", typeof(string));
        var first = new ParsingBuilder(
            OptionsA,
            properties: ImmutableDictionary<string, PropertyDefinition>.Empty.Add("verbose", firstVerbose));
        var second = new ParsingBuilder(
            OptionsA,
            properties: ImmutableDictionary<string, PropertyDefinition>.Empty.Add("verbose", secondVerbose));

        await Assert.That(() => first.Merge(second, @override: false)).Throws<ArgumentException>();
    }

    [Test]
    public async Task Merge_Identical_Properties_No_Conflict()
    {
        var verbose = CreateProperty("verbose", typeof(bool));
        var first = new ParsingBuilder(
            OptionsA,
            properties: ImmutableDictionary<string, PropertyDefinition>.Empty.Add("verbose", verbose));
        var second = new ParsingBuilder(
            OptionsA,
            properties: ImmutableDictionary<string, PropertyDefinition>.Empty.Add("verbose", verbose));

        var merged = first.Merge(second);

        await Assert.That(merged.Properties.Count).IsEqualTo(1);
        await Assert.That(merged.Properties["verbose"]).IsEqualTo(verbose);
    }

    [Test]
    public async Task Merge_Different_ParsingOptions_Without_Override_Throws()
    {
        var first = new ParsingBuilder(OptionsA);
        var second = new ParsingBuilder(OptionsB);

        await Assert.That(() => first.Merge(second, @override: false)).Throws<ArgumentException>();
    }

    [Test]
    public async Task Merge_Different_ParsingOptions_With_Override_Uses_Second()
    {
        var first = new ParsingBuilder(OptionsA);
        var second = new ParsingBuilder(OptionsB);

        var merged = first.Merge(second, @override: true);

        await Assert.That(merged.ParsingOptions).IsEqualTo(OptionsB);
    }

    [Test]
    public async Task Merge_First_Empty_Second_Has_Arguments_Returns_Second_Arguments()
    {
        var first = new ParsingBuilder(OptionsA);
        var path = CreateArgument("path", typeof(string), 0, 1);
        var second = new ParsingBuilder(
            OptionsA,
            argument: ImmutableList.Create(path));

        var merged = first.Merge(second);

        await Assert.That(merged.Argument.Count).IsEqualTo(1);
        await Assert.That(merged.Argument[0]).IsEqualTo(path);
    }

    [Test]
    public async Task Merge_First_Has_Arguments_Second_Empty_Preserves_First_Arguments()
    {
        var path = CreateArgument("path", typeof(string), 0, 1);
        var first = new ParsingBuilder(
            OptionsA,
            argument: ImmutableList.Create(path));
        var second = new ParsingBuilder(OptionsA);

        var merged = first.Merge(second);

        await Assert.That(merged.Argument.Count).IsEqualTo(1);
        await Assert.That(merged.Argument[0]).IsEqualTo(path);
    }

    [Test]
    public async Task Merge_Same_Arguments_Preserves_Them()
    {
        var path = CreateArgument("path", typeof(string), 0, 1);
        var first = new ParsingBuilder(
            OptionsA,
            argument: ImmutableList.Create(path));
        var second = new ParsingBuilder(
            OptionsA,
            argument: ImmutableList.Create(path));

        var merged = first.Merge(second);

        await Assert.That(merged.Argument.Count).IsEqualTo(1);
        await Assert.That(merged.Argument[0]).IsEqualTo(path);
    }

    [Test]
    public async Task Merge_Different_Arguments_With_Override_Uses_Second()
    {
        var path = CreateArgument("path", typeof(string), 0, 1);
        var count = CreateArgument("count", typeof(int), 1, 1);
        var first = new ParsingBuilder(
            OptionsA,
            argument: ImmutableList.Create(path));
        var second = new ParsingBuilder(
            OptionsA,
            argument: ImmutableList.Create(count));

        var merged = first.Merge(second, @override: true);

        await Assert.That(merged.Argument.Count).IsEqualTo(1);
        await Assert.That(merged.Argument[0]).IsEqualTo(count);
    }

    [Test]
    public async Task Merge_Different_Arguments_Without_Override_Throws()
    {
        var path = CreateArgument("path", typeof(string), 0, 1);
        var count = CreateArgument("count", typeof(int), 1, 1);
        var first = new ParsingBuilder(
            OptionsA,
            argument: ImmutableList.Create(path));
        var second = new ParsingBuilder(
            OptionsA,
            argument: ImmutableList.Create(count));

        await Assert.That(() => first.Merge(second, @override: false)).Throws<ArgumentException>();
    }

    [Test]
    public async Task Merge_First_Empty_Second_Has_SubcommandDefinitions_Returns_Second_Definitions()
    {
        var first = new ParsingBuilder(OptionsA);
        var serve = CreateCommand("serve");
        var second = new ParsingBuilder(
            OptionsA,
            subcommandDefinitions: ImmutableDictionary<string, CommandDefinition>.Empty.Add("serve", serve));

        var merged = first.Merge(second);

        await Assert.That(merged.SubcommandDefinitions.Count).IsEqualTo(1);
        await Assert.That(merged.SubcommandDefinitions["serve"]).IsEqualTo(serve);
    }

    [Test]
    public async Task Merge_Non_Conflicting_SubcommandDefinitions_Contains_Both()
    {
        var serve = CreateCommand("serve");
        var watch = CreateCommand("watch");
        var first = new ParsingBuilder(
            OptionsA,
            subcommandDefinitions: ImmutableDictionary<string, CommandDefinition>.Empty.Add("serve", serve));
        var second = new ParsingBuilder(
            OptionsA,
            subcommandDefinitions: ImmutableDictionary<string, CommandDefinition>.Empty.Add("watch", watch));

        var merged = first.Merge(second);

        await Assert.That(merged.SubcommandDefinitions.Count).IsEqualTo(2);
        await Assert.That(merged.SubcommandDefinitions["serve"]).IsEqualTo(serve);
        await Assert.That(merged.SubcommandDefinitions["watch"]).IsEqualTo(watch);
    }

    [Test]
    public async Task Merge_Conflicting_SubcommandDefinitions_Without_Override_Throws()
    {
        var serveFirst = CreateCommand("serve", "first");
        var serveSecond = CreateCommand("serve", "second");
        var first = new ParsingBuilder(
            OptionsA,
            subcommandDefinitions: ImmutableDictionary<string, CommandDefinition>.Empty.Add("serve", serveFirst));
        var second = new ParsingBuilder(
            OptionsA,
            subcommandDefinitions: ImmutableDictionary<string, CommandDefinition>.Empty.Add("serve", serveSecond));

        await Assert.That(() => first.Merge(second, @override: false)).Throws<ArgumentException>();
    }

    [Test]
    public async Task Merge_Subcommands_Recursively()
    {
        var serve = CreateCommand("serve");
        var rootProperty = CreateProperty("verbose", typeof(bool));
        var childProperty = CreateProperty("format", typeof(string));
        var rootBuilder = new ParsingBuilder(
            OptionsA,
            subcommandDefinitions: ImmutableDictionary<string, CommandDefinition>.Empty.Add("serve", serve),
            properties: ImmutableDictionary<string, PropertyDefinition>.Empty.Add("verbose", rootProperty));

        var childBuilder = new ParsingBuilder(
            OptionsA,
            properties: ImmutableDictionary<string, PropertyDefinition>.Empty.Add("format", childProperty));

        var secondRootBuilder = new ParsingBuilder(
            OptionsA,
            subcommands: ImmutableDictionary<string, IParsingBuilder>.Empty.Add("serve", childBuilder));

        var merged = rootBuilder.Merge(secondRootBuilder);

        await Assert.That(merged.Properties.Count).IsEqualTo(1);
        await Assert.That(merged.Properties["verbose"]).IsEqualTo(rootProperty);
        await Assert.That(merged.Subcommands.Count).IsEqualTo(1);
        await Assert.That(merged.Subcommands.ContainsKey("serve")).IsTrue();

        var mergedChild = merged.Subcommands["serve"];
        await Assert.That(mergedChild.Properties.Count).IsEqualTo(1);
        await Assert.That(mergedChild.Properties["format"]).IsEqualTo(childProperty);
    }

    [Test]
    public async Task Merge_Recursive_Subcommand_Conflicts_With_Override()
    {
        var serve = CreateCommand("serve");
        var firstChildProp = CreateProperty("format", typeof(string), longAliases: ["fmt"]);
        var secondChildProp = CreateProperty("format", typeof(int));
        var firstRoot = new ParsingBuilder(
            OptionsA,
            subcommandDefinitions: ImmutableDictionary<string, CommandDefinition>.Empty.Add("serve", serve),
            subcommands: ImmutableDictionary<string, IParsingBuilder>.Empty.Add(
                "serve",
                new ParsingBuilder(
                    OptionsA,
                    properties: ImmutableDictionary<string, PropertyDefinition>.Empty.Add("format", firstChildProp))));

        var secondRoot = new ParsingBuilder(
            OptionsA,
            subcommands: ImmutableDictionary<string, IParsingBuilder>.Empty.Add(
                "serve",
                new ParsingBuilder(
                    OptionsA,
                    properties: ImmutableDictionary<string, PropertyDefinition>.Empty.Add("format", secondChildProp))));

        var merged = firstRoot.Merge(secondRoot, @override: true);

        var mergedChild = merged.Subcommands["serve"];
        await Assert.That(mergedChild.Properties.Count).IsEqualTo(1);
        await Assert.That(mergedChild.Properties["format"]).IsEqualTo(secondChildProp);
    }

    [Test]
    public async Task Merge_Recursive_Subcommand_Conflicts_Without_Override_Throws()
    {
        var serve = CreateCommand("serve");
        var firstChildProp = CreateProperty("format", typeof(string));
        var secondChildProp = CreateProperty("format", typeof(int));
        var firstRoot = new ParsingBuilder(
            OptionsA,
            subcommandDefinitions: ImmutableDictionary<string, CommandDefinition>.Empty.Add("serve", serve),
            subcommands: ImmutableDictionary<string, IParsingBuilder>.Empty.Add(
                "serve",
                new ParsingBuilder(
                    OptionsA,
                    properties: ImmutableDictionary<string, PropertyDefinition>.Empty.Add("format", firstChildProp))));

        var secondRoot = new ParsingBuilder(
            OptionsA,
            subcommands: ImmutableDictionary<string, IParsingBuilder>.Empty.Add(
                "serve",
                new ParsingBuilder(
                    OptionsA,
                    properties: ImmutableDictionary<string, PropertyDefinition>.Empty.Add("format", secondChildProp))));

        await Assert.That(() => firstRoot.Merge(secondRoot, @override: false)).Throws<ArgumentException>();
    }

    [Test]
    public async Task Merge_Preserves_Non_Conflicting_Properties_When_Some_Conflict()
    {
        var verbose = CreateProperty("verbose", typeof(bool));
        var firstFormat = CreateProperty("format", typeof(string));
        var secondFormat = CreateProperty("format", typeof(int));
        var first = new ParsingBuilder(
            OptionsA,
            properties: ImmutableDictionary<string, PropertyDefinition>.Empty
                .Add("verbose", verbose)
                .Add("format", firstFormat));
        var second = new ParsingBuilder(
            OptionsA,
            properties: ImmutableDictionary<string, PropertyDefinition>.Empty
                .Add("format", secondFormat));

        var merged = first.Merge(second, @override: true);

        await Assert.That(merged.Properties.Count).IsEqualTo(2);
        await Assert.That(merged.Properties["verbose"]).IsEqualTo(verbose);
        await Assert.That(merged.Properties["format"]).IsEqualTo(secondFormat);
    }

    [Test]
    public async Task Merge_Default_Override_Is_True()
    {
        var firstProp = CreateProperty("verbose", typeof(bool));
        var secondProp = CreateProperty("verbose", typeof(string));
        var first = new ParsingBuilder(
            OptionsA,
            properties: ImmutableDictionary<string, PropertyDefinition>.Empty.Add("verbose", firstProp));
        var second = new ParsingBuilder(
            OptionsB,
            properties: ImmutableDictionary<string, PropertyDefinition>.Empty.Add("verbose", secondProp));

        var merged = first.Merge(second);

        await Assert.That(merged.ParsingOptions).IsEqualTo(OptionsB);
        await Assert.That(merged.Properties["verbose"]).IsEqualTo(secondProp);
    }

    [Test]
    public async Task Merge_Different_ParsingOptions_With_Override_False_Throws_Before_Checking_Other_Fields()
    {
        var first = new ParsingBuilder(OptionsA);
        var second = new ParsingBuilder(OptionsB);

        await Assert.That(() => first.Merge(second, @override: false)).Throws<ArgumentException>();
    }

    private static ParsingOptions CreateOptions(string appName)
    {
        return new ParsingOptions(
            new ProgramInformation(appName, new Document("summary", "help"), new Version(1, 0), "https://example.com"),
            ParsingOptions.DefaultVersionFlags,
            ParsingOptions.DefaultHelpFlags,
            TextWriter.Null,
            false,
            false,
            StyleTable.Default);
    }

    private static DefinitionInformation CreateInformation(string name)
    {
        return new DefinitionInformation(new NameWithVisibility(name, true), new Document(name, name));
    }

    private static PropertyDefinition CreateProperty(string name,
                                                     [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)]
                                                     Type type,
                                                     ImmutableArray<string> longAliases = default)
    {
        var longNames = ImmutableDictionary.CreateBuilder<string, NameWithVisibility>(StringComparer.Ordinal);

        foreach (var alias in longAliases.IsDefaultOrEmpty ? [name] : longAliases)
        {
            longNames[alias] = new NameWithVisibility(alias, true);
        }

        return new PropertyDefinition(
            CreateInformation(name),
            longNames.ToImmutable(),
            ImmutableDictionary<string, NameWithVisibility>.Empty,
            null,
            type,
            false);
    }

    private static ArgumentDefinition CreateArgument(string name,
                                                     [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)]
                                                     Type type,
                                                     int minimum,
                                                     int maximum)
    {
        return new ArgumentDefinition(
            CreateInformation(name),
            null,
            new ValueRange(minimum, maximum),
            type,
            false);
    }

    private static CommandDefinition CreateCommand(string name, string? alias = null)
    {
        var aliases = alias is not null
            ? ImmutableDictionary<string, NameWithVisibility>.Empty.Add(alias, new NameWithVisibility(alias, true))
            : ImmutableDictionary<string, NameWithVisibility>.Empty;

        return new CommandDefinition(
            new DefinitionInformation(new NameWithVisibility(name, true), new Document(name, name)),
            aliases,
            null);
    }
}
