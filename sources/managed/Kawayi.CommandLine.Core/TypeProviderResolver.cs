// Copyright (c) 2026 MoeGodot<me@kawayi.moe>.
// Licensed under the GNU Affero General Public License v3-or-later license.

using System.Collections.Immutable;
using Kawayi.CommandLine.Abstractions;
using Kawayi.CommandLine.Core.Primitives;

namespace Kawayi.CommandLine.Core;

internal interface IBuiltInTypeProvider : ITypeProvider;

internal interface IBuiltInExtendedTypeProvider : IExtendedTypeProvider;

/// <summary>
///
/// </summary>
public static class TypeProviderResolver
{
    /// <summary>
    /// Builtin type providers,see <see cref="Primitives"/> <see langword="namespace"/> and <see cref="ContainerParser"/>
    /// </summary>
    public static TypeProviders BuiltinTypeProviders { get; } = CreateBuiltinTypeProviders();

    internal static TypeProviders CreateVisibleProviders(TypeProviders customProviders)
    {
        var exactProviders = BuiltinTypeProviders.Providers.SetItems(customProviders.Providers);
        var extendedProvidersBuilder = ImmutableArray.CreateBuilder<IExtendedTypeProvider>(
            customProviders.ExtendedProviders.Length + BuiltinTypeProviders.ExtendedProviders.Length);
        extendedProvidersBuilder.AddRange(customProviders.ExtendedProviders);
        extendedProvidersBuilder.AddRange(BuiltinTypeProviders.ExtendedProviders);
        return new TypeProviders(exactProviders, extendedProvidersBuilder.MoveToImmutable());
    }

    internal static Type NormalizeTargetType(Type targetType)
    {
        ArgumentNullException.ThrowIfNull(targetType);
        return Nullable.GetUnderlyingType(targetType) ?? targetType;
    }

    internal static ParsingResult? ParseCustomExact(ParsingOptions options,
                                                    ImmutableArray<Token> input,
                                                    Type targetType,
                                                    string? format,
                                                    TypeProviders visibleProviders) =>
        ParseExact(options, input, targetType, format, visibleProviders, builtInPhase: false);

    internal static ParsingResult? ParseBuiltInExact(ParsingOptions options,
                                                     ImmutableArray<Token> input,
                                                     Type targetType,
                                                     string? format,
                                                     TypeProviders visibleProviders) =>
        ParseExact(options, input, targetType, format, visibleProviders, builtInPhase: true);

    internal static ParsingResult? ParseCustomExtended(ParsingOptions options,
                                                       ImmutableArray<Token> input,
                                                       Type targetType,
                                                       string? format,
                                                       TypeProviders visibleProviders) =>
        ParseExtended(options, input, targetType, format, visibleProviders, builtInPhase: false);

    internal static ParsingResult? ParseBuiltInExtended(ParsingOptions options,
                                                        ImmutableArray<Token> input,
                                                        Type targetType,
                                                        string? format,
                                                        TypeProviders visibleProviders) =>
        ParseExtended(options, input, targetType, format, visibleProviders, builtInPhase: true);

    internal static RawProviderResult TryParseCustomExact(ImmutableArray<Token> input,
                                                          Type targetType,
                                                          string? format,
                                                          TypeProviders visibleProviders,
                                                          out object? result,
                                                          out string? error) =>
        TryParseExact(input, targetType, format, visibleProviders, builtInPhase: false, out result, out error);

    internal static RawProviderResult TryParseBuiltInExact(ImmutableArray<Token> input,
                                                           Type targetType,
                                                           string? format,
                                                           TypeProviders visibleProviders,
                                                           out object? result,
                                                           out string? error) =>
        TryParseExact(input, targetType, format, visibleProviders, builtInPhase: true, out result, out error);

    internal static RawProviderResult TryParseCustomExtended(ImmutableArray<Token> input,
                                                             Type targetType,
                                                             string? format,
                                                             TypeProviders visibleProviders,
                                                             out object? result,
                                                             out string? error) =>
        TryParseExtended(input, targetType, format, visibleProviders, builtInPhase: false, out result, out error);

    internal static RawProviderResult TryParseBuiltInExtended(ImmutableArray<Token> input,
                                                              Type targetType,
                                                              string? format,
                                                              TypeProviders visibleProviders,
                                                              out object? result,
                                                              out string? error) =>
        TryParseExtended(input, targetType, format, visibleProviders, builtInPhase: true, out result, out error);

    internal static ParsingResult CreateUnsupportedTypeResult(Type targetType, string unsupportedCallerName)
    {
        ArgumentNullException.ThrowIfNull(targetType);
        ArgumentException.ThrowIfNullOrWhiteSpace(unsupportedCallerName);

        return new GotError(new NotSupportedException(
            $"Type '{targetType.FullName}' is not supported by {unsupportedCallerName}."));
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

    private static ParsingResult? ParseExact(ParsingOptions options,
                                             ImmutableArray<Token> input,
                                             Type targetType,
                                             string? format,
                                             TypeProviders visibleProviders,
                                             bool builtInPhase)
    {
        try
        {
            var status = TryParseExact(input, targetType, format, visibleProviders, builtInPhase, out var parsedValue, out var error);
            return status switch
            {
                RawProviderResult.Success => DebugOutput.Emit(options,
                                                              new ParsingFinished<object?>(parsedValue),
                                                              CreateProviderDebugContext(
                                                                  visibleProviders.Providers[NormalizeTargetType(targetType)],
                                                                  input,
                                                                  NormalizeTargetType(targetType),
                                                                  null)),
                RawProviderResult.Invalid => DebugOutput.Emit(options,
                                                              new InvalidArgumentDetected(GetSelectedArgument(input),
                                                                                          error ?? (targetType.FullName ?? targetType.Name),
                                                                                          null),
                                                              CreateProviderDebugContext(
                                                                  visibleProviders.Providers[NormalizeTargetType(targetType)],
                                                                  input,
                                                                  NormalizeTargetType(targetType),
                                                                  error ?? (targetType.FullName ?? targetType.Name))),
                _ => null
            };
        }
        catch (Exception exception)
        {
            if (!visibleProviders.Providers.TryGetValue(NormalizeTargetType(targetType), out var provider)
                || (provider is IBuiltInTypeProvider) != builtInPhase)
            {
                return null;
            }

            return DebugOutput.Emit(options,
                                    new GotError(exception),
                                    CreateProviderDebugContext(provider,
                                                               input,
                                                               NormalizeTargetType(targetType),
                                                               exception.Message));
        }
    }

    private static ParsingResult? ParseExtended(ParsingOptions options,
                                                ImmutableArray<Token> input,
                                                Type targetType,
                                                string? format,
                                                TypeProviders visibleProviders,
                                                bool builtInPhase)
    {
        foreach (var provider in visibleProviders.ExtendedProviders)
        {
            if ((provider is IBuiltInExtendedTypeProvider) != builtInPhase)
            {
                continue;
            }

            try
            {
                if (provider.TryParse(input, visibleProviders, NormalizeTargetType(targetType), format, out var parsedValue, out var error))
                {
                    return DebugOutput.Emit(options,
                                            new ParsingFinished<object?>(parsedValue),
                                            CreateProviderDebugContext(provider,
                                                                       input,
                                                                       NormalizeTargetType(targetType),
                                                                       null));
                }

                if (error is not null)
                {
                    var argument = GetSelectedArgument(input);
                    var expectation = error;

                    if (provider is ContainerParser
                        && ContainerParser.TryDecodeNestedInvalidArgument(error, out var nestedArgument, out var nestedExpectation))
                    {
                        argument = nestedArgument;
                        expectation = nestedExpectation;
                    }

                    return DebugOutput.Emit(options,
                                            new InvalidArgumentDetected(argument, expectation, null),
                                            CreateProviderDebugContext(provider,
                                                                       input,
                                                                       NormalizeTargetType(targetType),
                                                                       expectation));
                }
            }
            catch (Exception exception)
            {
                return DebugOutput.Emit(options,
                                        new GotError(exception),
                                        CreateProviderDebugContext(provider,
                                                                   input,
                                                                   NormalizeTargetType(targetType),
                                                                   exception.Message));
            }
        }

        return null;
    }

    private static RawProviderResult TryParseExact(ImmutableArray<Token> input,
                                                   Type targetType,
                                                   string? format,
                                                   TypeProviders visibleProviders,
                                                   bool builtInPhase,
                                                   out object? result,
                                                   out string? error)
    {
        var effectiveTargetType = NormalizeTargetType(targetType);

        if (!visibleProviders.Providers.TryGetValue(effectiveTargetType, out var provider)
            || (provider is IBuiltInTypeProvider) != builtInPhase)
        {
            result = null;
            error = null;
            return RawProviderResult.NotHandled;
        }

        if (provider.TryParse(input, visibleProviders, format, out result, out error))
        {
            return RawProviderResult.Success;
        }

        result = null;
        return RawProviderResult.Invalid;
    }

    private static RawProviderResult TryParseExtended(ImmutableArray<Token> input,
                                                      Type targetType,
                                                      string? format,
                                                      TypeProviders visibleProviders,
                                                      bool builtInPhase,
                                                      out object? result,
                                                      out string? error)
    {
        foreach (var provider in visibleProviders.ExtendedProviders)
        {
            if ((provider is IBuiltInExtendedTypeProvider) != builtInPhase)
            {
                continue;
            }

            if (provider.TryParse(input, visibleProviders, NormalizeTargetType(targetType), format, out result, out error))
            {
                return RawProviderResult.Success;
            }

            if (error is not null)
            {
                result = null;
                return RawProviderResult.Invalid;
            }
        }

        result = null;
        error = null;
        return RawProviderResult.NotHandled;
    }

    private static TypeProviders CreateBuiltinTypeProviders()
    {
        var exactProviders = ImmutableDictionary.CreateBuilder<Type, ITypeProvider>();

        exactProviders[typeof(string)] = new CommonParser(typeof(string));
        exactProviders[typeof(bool)] = new BooleanParser();
        exactProviders[typeof(byte)] = new NumberParser(typeof(byte));
        exactProviders[typeof(sbyte)] = new NumberParser(typeof(sbyte));
        exactProviders[typeof(ushort)] = new NumberParser(typeof(ushort));
        exactProviders[typeof(short)] = new NumberParser(typeof(short));
        exactProviders[typeof(uint)] = new NumberParser(typeof(uint));
        exactProviders[typeof(int)] = new NumberParser(typeof(int));
        exactProviders[typeof(ulong)] = new NumberParser(typeof(ulong));
        exactProviders[typeof(long)] = new NumberParser(typeof(long));
        exactProviders[typeof(float)] = new FloatParser(typeof(float));
        exactProviders[typeof(double)] = new FloatParser(typeof(double));
        exactProviders[typeof(decimal)] = new FloatParser(typeof(decimal));
        exactProviders[typeof(Guid)] = new CommonParser(typeof(Guid));
        exactProviders[typeof(Uri)] = new CommonParser(typeof(Uri));
        exactProviders[typeof(DateTime)] = new CommonParser(typeof(DateTime));
        exactProviders[typeof(DateTimeOffset)] = new CommonParser(typeof(DateTimeOffset));
        exactProviders[typeof(DateOnly)] = new CommonParser(typeof(DateOnly));
        exactProviders[typeof(TimeOnly)] = new CommonParser(typeof(TimeOnly));

        return new TypeProviders(
            exactProviders.ToImmutable(),
            [
                new EnumParser(),
                new ContainerParser()
            ]);
    }

    internal enum RawProviderResult
    {
        NotHandled,
        Success,
        Invalid
    }
}
