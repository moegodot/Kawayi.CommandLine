// Copyright (c) 2026 MoeGodot<me@kawayi.moe>.
// Licensed under the GNU Affero General Public License v3-or-later license.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Kawayi.CommandLine.Abstractions;

namespace Kawayi.CommandLine.Core.Tests;

public sealed class TypeProviderIntegrationTests
{
    [Test]
    public async Task CliSchemaParser_Prefers_Exact_Providers_Over_Extended_Providers()
    {
        var schema = CreateSchema(CreateProperty("count", typeof(int)));
        var options = CreateOptions(new TypeProviders(
            ImmutableDictionary<Type, ITypeProvider>.Empty.Add(typeof(int), new HexIntTypeProvider()),
            [new IntOffsetExtendedTypeProvider(100)]));

        var result = CliSchemaParser.CreateParsing(
            options,
            [new LongOptionToken("count"), new ArgumentOrCommandToken("42")],
            schema);
        var command = AssertFinished(result);

        await Assert.That((int)command.Properties[schema.Properties[new LongOptionToken("count")]]).IsEqualTo(66);
    }

    [Test]
    public async Task CliSchemaParser_Uses_User_Extended_Provider_Before_BuiltIn_Exact_Provider()
    {
        var schema = CreateSchema(CreateProperty("count", typeof(int)));
        var options = CreateOptions(new TypeProviders(
            ImmutableDictionary<Type, ITypeProvider>.Empty,
            [new IntWordsExtendedTypeProvider()]));

        var result = CliSchemaParser.CreateParsing(
            options,
            [new LongOptionToken("count"), new ArgumentOrCommandToken("forty-two")],
            schema);
        var command = AssertFinished(result);

        await Assert.That((int)command.Properties[schema.Properties[new LongOptionToken("count")]]).IsEqualTo(42);
    }

    [Test]
    public async Task CliSchemaParser_Uses_Extended_Providers_For_Runtime_Types()
    {
        var schema = CreateSchema(CreateProperty("release", typeof(Version)));
        var options = CreateOptions(new TypeProviders(
            ImmutableDictionary<Type, ITypeProvider>.Empty,
            [new VersionExtendedTypeProvider()]));

        var result = CliSchemaParser.CreateParsing(
            options,
            [new LongOptionToken("release"), new ArgumentOrCommandToken("1.2.3.4")],
            schema);
        var command = AssertFinished(result);

        await Assert.That((Version)command.Properties[schema.Properties[new LongOptionToken("release")]]).IsEqualTo(new Version(1, 2, 3, 4));
    }

    [Test]
    public async Task ContainerParser_Uses_Exact_Providers_For_Element_Types()
    {
        var options = CreateOptions(new TypeProviders(
            ImmutableDictionary<Type, ITypeProvider>.Empty.Add(typeof(Version), new VersionExactTypeProvider()),
            ImmutableArray<IExtendedTypeProvider>.Empty));

        var result = ContainerParser.CreateParsing(
            options,
            [new ArgumentOrCommandToken("1.0"), new ArgumentOrCommandToken("2.1")],
            new ContainerType(typeof(ImmutableArray<Version>), null, typeof(Version)));

        if (result is not ParsingFinished { UntypedResult: ImmutableArray<Version> versions })
        {
            throw new InvalidOperationException($"Expected {nameof(ParsingFinished)}, got {result.GetType().FullName}.");
        }

        await Assert.That(versions).IsEquivalentTo([new Version(1, 0), new Version(2, 1)]);
    }

    [Test]
    public async Task ContainerParser_Continues_To_Later_Extended_Providers_When_Earlier_Providers_Do_Not_Handle_Type()
    {
        var options = CreateOptions(new TypeProviders(
            ImmutableDictionary<Type, ITypeProvider>.Empty,
            [new SkipStringOnlyExtendedTypeProvider(), new VersionExtendedTypeProvider()]));

        var result = ContainerParser.CreateParsing(
            options,
            [new ArgumentOrCommandToken("1.0"), new ArgumentOrCommandToken("2.1")],
            new ContainerType(typeof(ImmutableArray<Version>), null, typeof(Version)));

        if (result is not ParsingFinished { UntypedResult: ImmutableArray<Version> versions })
        {
            throw new InvalidOperationException($"Expected {nameof(ParsingFinished)}, got {result.GetType().FullName}.");
        }

        await Assert.That(versions).IsEquivalentTo([new Version(1, 0), new Version(2, 1)]);
    }

    [Test]
    public async Task ContainerParser_Returns_InvalidArgument_When_Extended_Provider_Rejects_A_Handled_Type()
    {
        var options = CreateOptions(new TypeProviders(
            ImmutableDictionary<Type, ITypeProvider>.Empty,
            [new SkipStringOnlyExtendedTypeProvider(), new VersionExtendedTypeProvider()]));

        var result = ContainerParser.CreateParsing(
            options,
            [new ArgumentOrCommandToken("not-a-version")],
            new ContainerType(typeof(ImmutableArray<Version>), null, typeof(Version)));

        if (result is not InvalidArgumentDetected invalid)
        {
            throw new InvalidOperationException($"Expected {nameof(InvalidArgumentDetected)}, got {result.GetType().FullName}.");
        }

        await Assert.That(invalid.Argument).IsEqualTo("not-a-version");
        await Assert.That(invalid.Expect).IsEqualTo("Version");
        await Assert.That(invalid.Exception).IsNull();
    }

    [Test]
    public async Task CliSchemaParser_Passes_Visible_Providers_To_Exact_Providers()
    {
        var schema = CreateSchema(CreateProperty("count", typeof(int)));
        var options = CreateOptions(new TypeProviders(
            ImmutableDictionary<Type, ITypeProvider>.Empty.Add(typeof(int), new DelegatingIntTypeProvider()),
            ImmutableArray<IExtendedTypeProvider>.Empty));

        var result = CliSchemaParser.CreateParsing(
            options,
            [new LongOptionToken("count"), new ArgumentOrCommandToken("42")],
            schema);
        var command = AssertFinished(result);

        await Assert.That((int)command.Properties[schema.Properties[new LongOptionToken("count")]]).IsEqualTo(42);
    }

    private static CliSchema CreateSchema(PropertyDefinition property)
    {
        var builder = new CliSchemaBuilder(
            ImmutableDictionary.CreateBuilder<string, CommandDefinition>(),
            ImmutableDictionary.CreateBuilder<string, CliSchemaBuilder>(),
            ImmutableDictionary.CreateBuilder<string, PropertyDefinition>(),
            ImmutableList.CreateBuilder<ParameterDefinition>());

        builder.Properties[property.Information.Name.Value] = property;
        return builder.Build();
    }

    private static PropertyDefinition CreateProperty(string name, Type type)
    {
        return new PropertyDefinition(
            new DefinitionInformation(new NameWithVisibility(name, true), new Document("summary", "help")),
            ImmutableDictionary<string, NameWithVisibility>.Empty,
            ImmutableDictionary<string, NameWithVisibility>.Empty,
            null,
            type,
            false);
    }

    private static ParsingOptions CreateOptions(TypeProviders typeProviders)
    {
        return new ParsingOptions(
            new ProgramInformation("test", new Document("summary", "help"), new Version(1, 2, 3), "https://example.com"),
            ParsingOptions.DefaultVersionFlags,
            ParsingOptions.DefaultHelpFlags,
            TextWriter.Null,
            TextWriter.Null,
            false,
            false,
            false,
            StyleTable.Default,
            typeProviders);
    }

    private static Cli AssertFinished(ParsingResult result)
    {
        return result is ParsingFinished { UntypedResult: Cli command }
            ? command
            : throw new InvalidOperationException($"Expected {nameof(ParsingFinished)}, got {result.GetType().FullName}.");
    }

    private sealed class HexIntTypeProvider : ITypeProvider
    {
        public bool TryParse(ImmutableArray<Token> input,
                             TypeProviders typeProviders,
                             string? format,
                             [NotNullWhen(true)] out object? result,
                             [NotNullWhen(false)] out string? error)
        {
            if (!input.IsDefaultOrEmpty
                && int.TryParse(input[^1].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var value))
            {
                result = value;
                error = null;
                return true;
            }

            result = null;
            error = "hex int";
            return false;
        }
    }

    private sealed class IntOffsetExtendedTypeProvider(int offset) : IExtendedTypeProvider
    {
        public bool TryParse(ImmutableArray<Token> input,
                             TypeProviders typeProviders,
                             Type symbolType,
                             string? format,
                             [NotNullWhen(true)] out object? result,
                             out string? error)
        {
            if (symbolType != typeof(int))
            {
                result = null;
                error = null;
                return false;
            }

            if (!input.IsDefaultOrEmpty
                && int.TryParse(input[^1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
            {
                result = value + offset;
                error = null;
                return true;
            }

            result = null;
            error = "offset int";
            return false;
        }
    }

    private sealed class IntWordsExtendedTypeProvider : IExtendedTypeProvider
    {
        public bool TryParse(ImmutableArray<Token> input,
                             TypeProviders typeProviders,
                             Type symbolType,
                             string? format,
                             [NotNullWhen(true)] out object? result,
                             out string? error)
        {
            if (symbolType != typeof(int))
            {
                result = null;
                error = null;
                return false;
            }

            if (!input.IsDefaultOrEmpty
                && string.Equals(input[^1].Value, "forty-two", StringComparison.Ordinal))
            {
                result = 42;
                error = null;
                return true;
            }

            result = null;
            error = "word int";
            return false;
        }
    }

    private sealed class VersionExactTypeProvider : ITypeProvider
    {
        public bool TryParse(ImmutableArray<Token> input,
                             TypeProviders typeProviders,
                             string? format,
                             [NotNullWhen(true)] out object? result,
                             [NotNullWhen(false)] out string? error)
        {
            if (!input.IsDefaultOrEmpty && Version.TryParse(input[^1].Value, out var version) && version is not null)
            {
                result = version;
                error = null;
                return true;
            }

            result = null;
            error = "Version";
            return false;
        }
    }

    private sealed class DelegatingIntTypeProvider : ITypeProvider
    {
        public bool TryParse(ImmutableArray<Token> input,
                             TypeProviders typeProviders,
                             string? format,
                             [NotNullWhen(true)] out object? result,
                             [NotNullWhen(false)] out string? error)
        {
            if (!typeProviders.Providers.TryGetValue(typeof(string), out var stringProvider))
            {
                result = null;
                error = "string";
                return false;
            }

            if (!stringProvider.TryParse(input, typeProviders, format, out var rawValue, out _)
                || rawValue is not string rawText)
            {
                result = null;
                error = "string";
                return false;
            }

            if (int.TryParse(rawText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
            {
                result = value;
                error = null;
                return true;
            }

            result = null;
            error = "int";
            return false;
        }
    }

    private sealed class VersionExtendedTypeProvider : IExtendedTypeProvider
    {
        public bool TryParse(ImmutableArray<Token> input,
                             TypeProviders typeProviders,
                             Type symbolType,
                             string? format,
                             [NotNullWhen(true)] out object? result,
                             out string? error)
        {
            if (symbolType != typeof(Version))
            {
                result = null;
                error = null;
                return false;
            }

            if (!input.IsDefaultOrEmpty && Version.TryParse(input[^1].Value, out var version) && version is not null)
            {
                result = version;
                error = null;
                return true;
            }

            result = null;
            error = "Version";
            return false;
        }
    }

    private sealed class SkipStringOnlyExtendedTypeProvider : IExtendedTypeProvider
    {
        public bool TryParse(ImmutableArray<Token> input,
                             TypeProviders typeProviders,
                             Type symbolType,
                             string? format,
                             [NotNullWhen(true)] out object? result,
                             out string? error)
        {
            if (symbolType != typeof(string))
            {
                result = null;
                error = null;
                return false;
            }

            if (!input.IsDefaultOrEmpty)
            {
                result = input[^1].Value;
                error = null;
                return true;
            }

            result = string.Empty;
            error = null;
            return true;
        }
    }
}
