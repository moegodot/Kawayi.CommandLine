// Copyright (c) 2026 MoeGodot<me@kawayi.moe>.
// Licensed under the GNU Affero General Public License v3-or-later license.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Kawayi.CommandLine.Abstractions;
using Kawayi.CommandLine.Core.Primitives;

namespace Kawayi.CommandLine.Core;

internal static class TypeProviderResolver
{
    private static readonly ConditionalWeakTable<ParsingOptions, BuiltInProviderCache> BuiltInProviders = new();

    public static ParsingResult ParseValue(ParsingOptions options,
                                           ImmutableArray<Token> input,
                                           Type targetType,
                                           string unsupportedCallerName)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(targetType);
        ArgumentException.ThrowIfNullOrWhiteSpace(unsupportedCallerName);

        var effectiveTargetType = Nullable.GetUnderlyingType(targetType) ?? targetType;
        var builtInProviders = BuiltInProviders.GetValue(options, static current => BuiltInProviderCache.Create(current));
        var visibleProviders = CreateVisibleProviders(options.TypeProviders, builtInProviders.Providers);

        if (TryParseWithExactProviders(options,
                                       input,
                                       effectiveTargetType,
                                       visibleProviders,
                                       options.TypeProviders.Providers,
                                       out var result))
        {
            return result;
        }

        if (TryParseWithExtendedProviders(options,
                                          input,
                                          effectiveTargetType,
                                          visibleProviders,
                                          options.TypeProviders.ExtendedProviders,
                                          out result))
        {
            return result;
        }

        if (TryParseWithExactProviders(options,
                                       input,
                                       effectiveTargetType,
                                       visibleProviders,
                                       builtInProviders.Providers.Providers,
                                       out result))
        {
            return result;
        }

        if (TryParseWithExtendedProviders(options,
                                          input,
                                          effectiveTargetType,
                                          visibleProviders,
                                          builtInProviders.Providers.ExtendedProviders,
                                          out result))
        {
            return result;
        }

        return new GotError(new NotSupportedException(
            $"Type '{targetType.FullName}' is not supported by {unsupportedCallerName}."));
    }

    private static bool TryParseWithExactProviders(ParsingOptions options,
                                                   ImmutableArray<Token> input,
                                                   Type targetType,
                                                   TypeProviders visibleProviders,
                                                   ImmutableDictionary<Type, ITypeProvider> providers,
                                                   out ParsingResult result)
    {
        if (!providers.TryGetValue(targetType, out var provider))
        {
            result = null!;
            return false;
        }

        if (provider is ICoreTypeProvider coreProvider)
        {
            result = coreProvider.Parse(input, visibleProviders, null);
            return true;
        }

        if (provider.TryParse(input, visibleProviders, null, out var parsedValue, out var error))
        {
            result = DebugOutput.Emit(options,
                                      new ParsingFinished<object?>(parsedValue),
                                      CreateProviderDebugContext(provider, input, targetType, null));
            return true;
        }

        result = DebugOutput.Emit(options,
                                  new InvalidArgumentDetected(GetSelectedArgument(input),
                                                              error ?? (targetType.FullName ?? targetType.Name),
                                                              null),
                                  CreateProviderDebugContext(provider,
                                                             input,
                                                             targetType,
                                                             error ?? (targetType.FullName ?? targetType.Name)));
        return true;
    }

    private static bool TryParseWithExtendedProviders(ParsingOptions options,
                                                      ImmutableArray<Token> input,
                                                      Type targetType,
                                                      TypeProviders visibleProviders,
                                                      ImmutableArray<IExtendedTypeProvider> providers,
                                                      out ParsingResult result)
    {
        foreach (var provider in providers)
        {
            if (provider is ICoreExtendedTypeProvider coreProvider)
            {
                var coreResult = coreProvider.Parse(input, visibleProviders, targetType, null);
                if (coreResult is null)
                {
                    continue;
                }

                result = coreResult;
                return true;
            }

            if (provider.TryParse(input, visibleProviders, targetType, null, out var parsedValue, out var error))
            {
                result = DebugOutput.Emit(options,
                                          new ParsingFinished<object?>(parsedValue),
                                          CreateProviderDebugContext(provider, input, targetType, null));
                return true;
            }

            if (error is null)
            {
                continue;
            }

            result = DebugOutput.Emit(options,
                                      new InvalidArgumentDetected(GetSelectedArgument(input), error, null),
                                      CreateProviderDebugContext(provider, input, targetType, error));
            return true;
        }

        result = null!;
        return false;
    }

    private static DebugContext CreateProviderDebugContext(object provider,
                                                           ImmutableArray<Token> input,
                                                           Type targetType,
                                                           string? expectation)
    {
        return new DebugContext(provider.GetType().Name,
                                Tokens: input,
                                TargetType: targetType,
                                Expectation: expectation,
                                SelectedToken: input.IsDefaultOrEmpty ? null : input[^1].Value);
    }

    private static string GetSelectedArgument(ImmutableArray<Token> input)
    {
        return input.IsDefaultOrEmpty ? string.Empty : input[^1].Value;
    }

    private static TypeProviders CreateVisibleProviders(TypeProviders customProviders, TypeProviders builtInProviders)
    {
        var exactProviders = builtInProviders.Providers.SetItems(customProviders.Providers);
        var extendedProvidersBuilder = ImmutableArray.CreateBuilder<IExtendedTypeProvider>(
            customProviders.ExtendedProviders.Length + builtInProviders.ExtendedProviders.Length);
        extendedProvidersBuilder.AddRange(customProviders.ExtendedProviders);
        extendedProvidersBuilder.AddRange(builtInProviders.ExtendedProviders);
        return new TypeProviders(exactProviders, extendedProvidersBuilder.MoveToImmutable());
    }

    private static ParsingResult CreateStringParsing(ParsingOptions options, ImmutableArray<Token> input)
    {
        var value = input.IsDefaultOrEmpty ? string.Empty : input[^1].Value;

        return DebugOutput.Emit(options,
                                new ParsingFinished<string>(value),
                                new DebugContext(nameof(TypeProviderResolver),
                                                 Tokens: input,
                                                 TargetType: typeof(string),
                                                 Expectation: "string",
                                                 SelectedToken: value,
                                                 Summary: "materialized string value"));
    }

    private interface ICoreTypeProvider : ITypeProvider
    {
        ParsingResult Parse(ImmutableArray<Token> input, TypeProviders typeProviders, string? format);
    }

    private interface ICoreExtendedTypeProvider : IExtendedTypeProvider
    {
        ParsingResult? Parse(ImmutableArray<Token> input, TypeProviders typeProviders, Type symbolType, string? format);
    }

    private sealed class DelegateCoreTypeProvider(
        ParsingOptions options,
        Func<ParsingOptions, ImmutableArray<Token>, ParsingResult> parse)
        : ICoreTypeProvider
    {
        public ParsingResult Parse(ImmutableArray<Token> input, TypeProviders typeProviders, string? format)
        {
            return parse(options, input);
        }

        public bool TryParse(ImmutableArray<Token> input,
                             TypeProviders typeProviders,
                             string? format,
                             [NotNullWhen(true)] out object? result,
                             [NotNullWhen(false)] out string? error)
        {
            var parsingResult = Parse(input, typeProviders, format);

            switch (parsingResult)
            {
                case ParsingFinished finished:
                    result = finished.UntypedResult
                        ?? throw new InvalidOperationException("Exact type providers must return a non-null parsed value.");
                    error = null;
                    return true;
                case InvalidArgumentDetected invalid:
                    result = null;
                    error = invalid.Expect;
                    return false;
                default:
                    throw new InvalidOperationException(
                        $"Exact type provider '{GetType().FullName}' returned unsupported parsing result '{parsingResult.GetType().FullName}'.");
            }
        }
    }

    private sealed class DelegateCoreExtendedTypeProvider(
        ParsingOptions options,
        Func<ParsingOptions, ImmutableArray<Token>, TypeProviders, Type, ParsingResult?> parse)
        : ICoreExtendedTypeProvider
    {
        public ParsingResult? Parse(ImmutableArray<Token> input, TypeProviders typeProviders, Type symbolType, string? format)
        {
            return parse(options, input, typeProviders, symbolType);
        }

        public bool TryParse(ImmutableArray<Token> input,
                             TypeProviders typeProviders,
                             Type symbolType,
                             string? format,
                             [NotNullWhen(true)] out object? result,
                             out string? error)
        {
            var parsingResult = Parse(input, typeProviders, symbolType, format);

            switch (parsingResult)
            {
                case null:
                    result = null;
                    error = null;
                    return false;
                case ParsingFinished finished:
                    result = finished.UntypedResult
                        ?? throw new InvalidOperationException("Extended type providers must return a non-null parsed value.");
                    error = null;
                    return true;
                case InvalidArgumentDetected invalid:
                    result = null;
                    error = invalid.Expect;
                    return false;
                default:
                    result = null;
                    error = null;
                    return false;
            }
        }
    }

    private sealed class BuiltInProviderCache(TypeProviders providers)
    {
        public TypeProviders Providers { get; } = providers;

        public static BuiltInProviderCache Create(ParsingOptions options)
        {
            var exactProviders = ImmutableDictionary.CreateBuilder<Type, ITypeProvider>();

            exactProviders[typeof(string)] = new DelegateCoreTypeProvider(options, CreateStringParsing);
            exactProviders[typeof(bool)] = new DelegateCoreTypeProvider(options, static (current, input) => BooleanParser.CreateParsing(current, input, false));
            exactProviders[typeof(byte)] = new DelegateCoreTypeProvider(options, static (current, input) => NumberParser.CreateParsing(current, input, (byte)0));
            exactProviders[typeof(sbyte)] = new DelegateCoreTypeProvider(options, static (current, input) => NumberParser.CreateParsing(current, input, (sbyte)0));
            exactProviders[typeof(ushort)] = new DelegateCoreTypeProvider(options, static (current, input) => NumberParser.CreateParsing(current, input, (ushort)0));
            exactProviders[typeof(short)] = new DelegateCoreTypeProvider(options, static (current, input) => NumberParser.CreateParsing(current, input, (short)0));
            exactProviders[typeof(uint)] = new DelegateCoreTypeProvider(options, static (current, input) => NumberParser.CreateParsing(current, input, 0u));
            exactProviders[typeof(int)] = new DelegateCoreTypeProvider(options, static (current, input) => NumberParser.CreateParsing(current, input, 0));
            exactProviders[typeof(ulong)] = new DelegateCoreTypeProvider(options, static (current, input) => NumberParser.CreateParsing(current, input, 0UL));
            exactProviders[typeof(long)] = new DelegateCoreTypeProvider(options, static (current, input) => NumberParser.CreateParsing(current, input, 0L));
            exactProviders[typeof(float)] = new DelegateCoreTypeProvider(options, static (current, input) => FloatParser.CreateParsing(current, input, 0f));
            exactProviders[typeof(double)] = new DelegateCoreTypeProvider(options, static (current, input) => FloatParser.CreateParsing(current, input, 0d));
            exactProviders[typeof(decimal)] = new DelegateCoreTypeProvider(options, static (current, input) => FloatParser.CreateParsing(current, input, decimal.Zero));
            exactProviders[typeof(Guid)] = new DelegateCoreTypeProvider(options, static (current, input) => CommonParser.CreateParsing(current, input, Guid.Empty));
            exactProviders[typeof(Uri)] = new DelegateCoreTypeProvider(options, static (current, input) => CommonParser.CreateParsing(current, input, new Uri("https://placeholder.invalid")));
            exactProviders[typeof(DateTime)] = new DelegateCoreTypeProvider(options, static (current, input) => CommonParser.CreateParsing(current, input, default(DateTime)));
            exactProviders[typeof(DateTimeOffset)] = new DelegateCoreTypeProvider(options, static (current, input) => CommonParser.CreateParsing(current, input, default(DateTimeOffset)));
            exactProviders[typeof(DateOnly)] = new DelegateCoreTypeProvider(options, static (current, input) => CommonParser.CreateParsing(current, input, default(DateOnly)));
            exactProviders[typeof(TimeOnly)] = new DelegateCoreTypeProvider(options, static (current, input) => CommonParser.CreateParsing(current, input, default(TimeOnly)));

            TypeProviders providers = new(
                exactProviders.ToImmutable(),
                [
                    new DelegateCoreExtendedTypeProvider(
                        options,
                        static (current, input, _, symbolType) => symbolType.IsEnum
                            ? EnumParser.CreateParsing(current, input, symbolType, Enum.ToObject(symbolType, 0))
                            : null),
                    new DelegateCoreExtendedTypeProvider(
                        options,
                        static (current, input, _, symbolType) => ContainerType.TryCreate(symbolType, out var containerType)
                            ? ContainerParser.CreateParsing(current, input, containerType)
                            : null)
                ]);

            return new BuiltInProviderCache(providers);
        }
    }
}
