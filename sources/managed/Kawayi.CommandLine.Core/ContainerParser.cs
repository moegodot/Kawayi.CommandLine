// Copyright (c) 2026 MoeGodot<me@kawayi.moe>.
// Licensed under the GNU Affero General Public License v3-or-later license.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Kawayi.CommandLine.Abstractions;
using Kawayi.Escapes;

namespace Kawayi.CommandLine.Core;

/// <summary>
/// Parses supported collection and dictionary container types from command-line tokens.
/// </summary>
public sealed class ContainerParser : IBuiltInExtendedTypeProvider
{
    private const char NestedErrorSeparator = '\u001F';

    private static readonly MethodInfo BuildSequenceContainerRuntimeMethod = typeof(ContainerParser)
        .GetMethod(nameof(BuildSequenceContainerRuntime), BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new InvalidOperationException($"Method '{nameof(BuildSequenceContainerRuntime)}' was not found.");

    private static readonly MethodInfo BuildDictionaryContainerForRuntimeKeyMethod = typeof(ContainerParser)
        .GetMethod(nameof(BuildDictionaryContainerForRuntimeKey), BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new InvalidOperationException($"Method '{nameof(BuildDictionaryContainerForRuntimeKey)}' was not found.");

    private static readonly MethodInfo BuildDictionaryContainerForRuntimeValueMethod = typeof(ContainerParser)
        .GetMethod(nameof(BuildDictionaryContainerForRuntimeValue), BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new InvalidOperationException($"Method '{nameof(BuildDictionaryContainerForRuntimeValue)}' was not found.");

    /// <summary>
    /// Gets the default escaping rule used for dictionary entries encoded as <c>key=value</c>.
    /// </summary>
    public static IEscapeRule DefaultDictionaryEscapeRule { get; } = new SimpleEscapeRule(
        [new(@"\", @"\\"), new(@"=", @"\=")]);

    /// <summary>
    /// Attempts to parse a supported container value from the supplied tokens.
    /// </summary>
    public bool TryParse(ImmutableArray<Token> input,
                         TypeProviders typeProviders,
                         Type symbolType,
                         string? format,
                         [NotNullWhen(true)] out object? result,
                         out string? error)
    {
        ArgumentNullException.ThrowIfNull(symbolType);

        if (!ContainerType.TryCreate(symbolType, out var containerType))
        {
            result = null;
            error = null;
            return false;
        }

        result = CreateContainer(input, typeProviders, containerType, format, out error);
        return error is null;
    }

    private static object CreateContainer(ImmutableArray<Token> input,
                                          TypeProviders typeProviders,
                                          ContainerType containerType,
                                          string? format,
                                          out string? error)
    {
        ArgumentNullException.ThrowIfNull(containerType);
        ArgumentNullException.ThrowIfNull(containerType.Container);
        ArgumentNullException.ThrowIfNull(containerType.ValueType);

        if (!containerType.Container.IsConstructedGenericType)
        {
            throw new NotSupportedException($"Container type '{containerType.Container.FullName}' must be a constructed generic type.");
        }

        var genericDefinition = containerType.Container.GetGenericTypeDefinition();

        return IsDictionaryContainer(genericDefinition)
            ? CreateDictionary(containerType, genericDefinition, input, typeProviders, format, out error)
            : CreateSequence(containerType, genericDefinition, input, typeProviders, format, out error);
    }

    private static object CreateSequence(ContainerType containerType,
                                         Type genericDefinition,
                                         ImmutableArray<Token> input,
                                         TypeProviders typeProviders,
                                         string? format,
                                         out string? error)
    {
        var parsedValues = new object?[input.Length];

        for (var index = 0; index < input.Length; index++)
        {
            if (!TryParseValue(input[index].Value, typeProviders, containerType.ValueType, format, out parsedValues[index], out error))
            {
                return null!;
            }
        }

        error = null;
        return BuildSequenceContainer(genericDefinition, containerType.ValueType, parsedValues);
    }

    private static object CreateDictionary(ContainerType containerType,
                                           Type genericDefinition,
                                           ImmutableArray<Token> input,
                                           TypeProviders typeProviders,
                                           string? format,
                                           out string? error)
    {
        if (containerType.KeyType is null)
        {
            throw new NotSupportedException($"Dictionary container '{containerType.Container.FullName}' must provide a key type.");
        }

        var parsedEntries = new ParsedDictionaryEntry[input.Length];

        for (var index = 0; index < input.Length; index++)
        {
            var token = input[index].Value;

            if (!TrySplitDictionaryEntry(token, out var rawKey, out var rawValue))
            {
                error = "key=value";
                return null!;
            }

            if (!TryParseValue(DefaultDictionaryEscapeRule.Unescape(rawKey),
                               typeProviders,
                               containerType.KeyType,
                               format,
                               out var parsedKey,
                               out error))
            {
                return null!;
            }

            if (!TryParseValue(DefaultDictionaryEscapeRule.Unescape(rawValue),
                               typeProviders,
                               containerType.ValueType,
                               format,
                               out var parsedValue,
                               out error))
            {
                return null!;
            }

            parsedEntries[index] = new(parsedKey, parsedValue);
        }

        error = null;
        return BuildDictionaryContainer(genericDefinition, containerType.KeyType, containerType.ValueType, parsedEntries);
    }

    private static object BuildSequenceContainer(Type genericDefinition,
                                                 Type valueType,
                                                 object?[] parsedValues)
    {
        if (valueType == typeof(bool))
        {
            return BuildSequenceContainer<bool>(genericDefinition, parsedValues);
        }

        if (valueType == typeof(byte))
        {
            return BuildSequenceContainer<byte>(genericDefinition, parsedValues);
        }

        if (valueType == typeof(sbyte))
        {
            return BuildSequenceContainer<sbyte>(genericDefinition, parsedValues);
        }

        if (valueType == typeof(ushort))
        {
            return BuildSequenceContainer<ushort>(genericDefinition, parsedValues);
        }

        if (valueType == typeof(short))
        {
            return BuildSequenceContainer<short>(genericDefinition, parsedValues);
        }

        if (valueType == typeof(uint))
        {
            return BuildSequenceContainer<uint>(genericDefinition, parsedValues);
        }

        if (valueType == typeof(int))
        {
            return BuildSequenceContainer<int>(genericDefinition, parsedValues);
        }

        if (valueType == typeof(ulong))
        {
            return BuildSequenceContainer<ulong>(genericDefinition, parsedValues);
        }

        if (valueType == typeof(long))
        {
            return BuildSequenceContainer<long>(genericDefinition, parsedValues);
        }

        if (valueType == typeof(float))
        {
            return BuildSequenceContainer<float>(genericDefinition, parsedValues);
        }

        if (valueType == typeof(double))
        {
            return BuildSequenceContainer<double>(genericDefinition, parsedValues);
        }

        if (valueType == typeof(decimal))
        {
            return BuildSequenceContainer<decimal>(genericDefinition, parsedValues);
        }

        if (valueType == typeof(Guid))
        {
            return BuildSequenceContainer<Guid>(genericDefinition, parsedValues);
        }

        if (valueType == typeof(string))
        {
            return BuildSequenceContainer<string>(genericDefinition, parsedValues);
        }

        if (valueType == typeof(Uri))
        {
            return BuildSequenceContainer<Uri>(genericDefinition, parsedValues);
        }

        if (valueType == typeof(DateTime))
        {
            return BuildSequenceContainer<DateTime>(genericDefinition, parsedValues);
        }

        if (valueType == typeof(DateTimeOffset))
        {
            return BuildSequenceContainer<DateTimeOffset>(genericDefinition, parsedValues);
        }

        if (valueType == typeof(DateOnly))
        {
            return BuildSequenceContainer<DateOnly>(genericDefinition, parsedValues);
        }

        if (valueType == typeof(TimeOnly))
        {
            return BuildSequenceContainer<TimeOnly>(genericDefinition, parsedValues);
        }

        return BuildSequenceContainerForRuntimeType(genericDefinition, valueType, parsedValues);
    }

    private static object BuildDictionaryContainer(Type genericDefinition,
                                                   Type keyType,
                                                   Type valueType,
                                                   ParsedDictionaryEntry[] entries)
    {
        if (keyType == typeof(bool))
        {
            return BuildDictionaryContainerForKey<bool>(genericDefinition, valueType, entries);
        }

        if (keyType == typeof(byte))
        {
            return BuildDictionaryContainerForKey<byte>(genericDefinition, valueType, entries);
        }

        if (keyType == typeof(sbyte))
        {
            return BuildDictionaryContainerForKey<sbyte>(genericDefinition, valueType, entries);
        }

        if (keyType == typeof(ushort))
        {
            return BuildDictionaryContainerForKey<ushort>(genericDefinition, valueType, entries);
        }

        if (keyType == typeof(short))
        {
            return BuildDictionaryContainerForKey<short>(genericDefinition, valueType, entries);
        }

        if (keyType == typeof(uint))
        {
            return BuildDictionaryContainerForKey<uint>(genericDefinition, valueType, entries);
        }

        if (keyType == typeof(int))
        {
            return BuildDictionaryContainerForKey<int>(genericDefinition, valueType, entries);
        }

        if (keyType == typeof(ulong))
        {
            return BuildDictionaryContainerForKey<ulong>(genericDefinition, valueType, entries);
        }

        if (keyType == typeof(long))
        {
            return BuildDictionaryContainerForKey<long>(genericDefinition, valueType, entries);
        }

        if (keyType == typeof(float))
        {
            return BuildDictionaryContainerForKey<float>(genericDefinition, valueType, entries);
        }

        if (keyType == typeof(double))
        {
            return BuildDictionaryContainerForKey<double>(genericDefinition, valueType, entries);
        }

        if (keyType == typeof(decimal))
        {
            return BuildDictionaryContainerForKey<decimal>(genericDefinition, valueType, entries);
        }

        if (keyType == typeof(Guid))
        {
            return BuildDictionaryContainerForKey<Guid>(genericDefinition, valueType, entries);
        }

        if (keyType == typeof(string))
        {
            return BuildDictionaryContainerForKey<string>(genericDefinition, valueType, entries);
        }

        if (keyType == typeof(Uri))
        {
            return BuildDictionaryContainerForKey<Uri>(genericDefinition, valueType, entries);
        }

        if (keyType == typeof(DateTime))
        {
            return BuildDictionaryContainerForKey<DateTime>(genericDefinition, valueType, entries);
        }

        if (keyType == typeof(DateTimeOffset))
        {
            return BuildDictionaryContainerForKey<DateTimeOffset>(genericDefinition, valueType, entries);
        }

        if (keyType == typeof(DateOnly))
        {
            return BuildDictionaryContainerForKey<DateOnly>(genericDefinition, valueType, entries);
        }

        if (keyType == typeof(TimeOnly))
        {
            return BuildDictionaryContainerForKey<TimeOnly>(genericDefinition, valueType, entries);
        }

        return BuildDictionaryContainerForRuntimeKeyType(genericDefinition, keyType, valueType, entries);
    }

    private static bool TryParseValue(string rawValue,
                                      TypeProviders typeProviders,
                                      Type targetType,
                                      string? format,
                                      out object? result,
                                      out string? error)
    {
        ImmutableArray<Token> input = [new ArgumentOrCommandToken(rawValue)];

        var outcome = TypeProviderResolver.TryParseCustomExact(input, targetType, format, typeProviders, out result, out error);
        if (outcome == TypeProviderResolver.RawProviderResult.Success)
        {
            return true;
        }

        if (outcome == TypeProviderResolver.RawProviderResult.Invalid)
        {
            error = CreateNestedInvalidArgument(rawValue, error!);
            return false;
        }

        outcome = TypeProviderResolver.TryParseCustomExtended(input, targetType, format, typeProviders, out result, out error);
        if (outcome == TypeProviderResolver.RawProviderResult.Success)
        {
            return true;
        }

        if (outcome == TypeProviderResolver.RawProviderResult.Invalid)
        {
            error = CreateNestedInvalidArgument(rawValue, error!);
            return false;
        }

        outcome = TypeProviderResolver.TryParseBuiltInExact(input, targetType, format, typeProviders, out result, out error);
        if (outcome == TypeProviderResolver.RawProviderResult.Success)
        {
            return true;
        }

        if (outcome == TypeProviderResolver.RawProviderResult.Invalid)
        {
            error = CreateNestedInvalidArgument(rawValue, error!);
            return false;
        }

        outcome = TypeProviderResolver.TryParseBuiltInExtended(input, targetType, format, typeProviders, out result, out error);
        if (outcome == TypeProviderResolver.RawProviderResult.Success)
        {
            return true;
        }

        if (outcome == TypeProviderResolver.RawProviderResult.Invalid)
        {
            error = CreateNestedInvalidArgument(rawValue, error!);
            return false;
        }

        throw new NotSupportedException($"Type '{targetType.FullName}' is not supported by {nameof(ContainerParser)}.");
    }

    internal static bool TryDecodeNestedInvalidArgument(string error, out string argument, out string expect)
    {
        var separatorIndex = error.IndexOf(NestedErrorSeparator);
        if (separatorIndex < 0)
        {
            argument = string.Empty;
            expect = string.Empty;
            return false;
        }

        argument = error[..separatorIndex];
        expect = error[(separatorIndex + 1)..];
        return true;
    }

    private static string CreateNestedInvalidArgument(string argument, string expect)
    {
        return $"{argument}{NestedErrorSeparator}{expect}";
    }

    private static bool TrySplitDictionaryEntry(string rawValue, out string key, out string value)
    {
        for (var index = 0; index < rawValue.Length; index++)
        {
            if (rawValue[index] != '=' || IsEscaped(rawValue, index))
            {
                continue;
            }

            key = rawValue[..index];
            value = rawValue[(index + 1)..];
            return true;
        }

        key = string.Empty;
        value = string.Empty;
        return false;
    }

    private static bool IsEscaped(string value, int index)
    {
        var backslashCount = 0;

        for (var position = index - 1; position >= 0 && value[position] == '\\'; position--)
        {
            backslashCount++;
        }

        return backslashCount % 2 == 1;
    }

    private static object BuildSequenceContainer<T>(Type genericDefinition, object?[] parsedValues)
    {
        var values = CastSequenceValues<T>(parsedValues);

        return genericDefinition == typeof(ImmutableArray<>)
            ? ImmutableArray.CreateRange(values)
            : genericDefinition == typeof(ImmutableList<>)
                ? ImmutableList.CreateRange(values)
                : genericDefinition == typeof(ImmutableQueue<>)
                    ? ImmutableQueue.CreateRange(values)
                    : genericDefinition == typeof(ImmutableStack<>)
                        ? ImmutableStack.CreateRange(values)
                        : genericDefinition == typeof(ImmutableSortedSet<>)
                            ? ImmutableSortedSet.CreateRange(values)
                            : genericDefinition == typeof(ImmutableHashSet<>)
                                ? ImmutableHashSet.CreateRange(values)
                                : throw CreateUnsupportedContainerException(genericDefinition);
    }

    [UnconditionalSuppressMessage("Trimming", "IL2060", Justification = "Container construction uses runtime generic instantiation for parsed element types.")]
    [UnconditionalSuppressMessage("Aot", "IL3050", Justification = "Container construction uses runtime generic instantiation for parsed element types.")]
    private static object BuildSequenceContainerForRuntimeType(
        Type genericDefinition,
        Type valueType,
        object?[] parsedValues)
    {
        try
        {
            return BuildSequenceContainerRuntimeMethod
                .MakeGenericMethod(valueType)
                .Invoke(null, [genericDefinition, parsedValues])!;
        }
        catch (Exception exception)
        {
            throw UnwrapInvocationException(exception);
        }
    }

    private static object BuildSequenceContainerRuntime<T>(Type genericDefinition, object?[] parsedValues)
    {
        return BuildSequenceContainer<T>(genericDefinition, parsedValues);
    }

    private static T[] CastSequenceValues<T>(object?[] parsedValues)
    {
        var values = new T[parsedValues.Length];

        for (var index = 0; index < parsedValues.Length; index++)
        {
            values[index] = (T)parsedValues[index]!;
        }

        return values;
    }

    private static object BuildDictionaryContainerForKey<TKey>(Type genericDefinition,
                                                               Type valueType,
                                                               ParsedDictionaryEntry[] entries)
        where TKey : notnull
    {
        if (valueType == typeof(bool))
        {
            return BuildDictionaryContainer<TKey, bool>(genericDefinition, entries);
        }

        if (valueType == typeof(byte))
        {
            return BuildDictionaryContainer<TKey, byte>(genericDefinition, entries);
        }

        if (valueType == typeof(sbyte))
        {
            return BuildDictionaryContainer<TKey, sbyte>(genericDefinition, entries);
        }

        if (valueType == typeof(ushort))
        {
            return BuildDictionaryContainer<TKey, ushort>(genericDefinition, entries);
        }

        if (valueType == typeof(short))
        {
            return BuildDictionaryContainer<TKey, short>(genericDefinition, entries);
        }

        if (valueType == typeof(uint))
        {
            return BuildDictionaryContainer<TKey, uint>(genericDefinition, entries);
        }

        if (valueType == typeof(int))
        {
            return BuildDictionaryContainer<TKey, int>(genericDefinition, entries);
        }

        if (valueType == typeof(ulong))
        {
            return BuildDictionaryContainer<TKey, ulong>(genericDefinition, entries);
        }

        if (valueType == typeof(long))
        {
            return BuildDictionaryContainer<TKey, long>(genericDefinition, entries);
        }

        if (valueType == typeof(float))
        {
            return BuildDictionaryContainer<TKey, float>(genericDefinition, entries);
        }

        if (valueType == typeof(double))
        {
            return BuildDictionaryContainer<TKey, double>(genericDefinition, entries);
        }

        if (valueType == typeof(decimal))
        {
            return BuildDictionaryContainer<TKey, decimal>(genericDefinition, entries);
        }

        if (valueType == typeof(Guid))
        {
            return BuildDictionaryContainer<TKey, Guid>(genericDefinition, entries);
        }

        if (valueType == typeof(string))
        {
            return BuildDictionaryContainer<TKey, string>(genericDefinition, entries);
        }

        if (valueType == typeof(Uri))
        {
            return BuildDictionaryContainer<TKey, Uri>(genericDefinition, entries);
        }

        if (valueType == typeof(DateTime))
        {
            return BuildDictionaryContainer<TKey, DateTime>(genericDefinition, entries);
        }

        if (valueType == typeof(DateTimeOffset))
        {
            return BuildDictionaryContainer<TKey, DateTimeOffset>(genericDefinition, entries);
        }

        if (valueType == typeof(DateOnly))
        {
            return BuildDictionaryContainer<TKey, DateOnly>(genericDefinition, entries);
        }

        if (valueType == typeof(TimeOnly))
        {
            return BuildDictionaryContainer<TKey, TimeOnly>(genericDefinition, entries);
        }

        return BuildDictionaryContainerForRuntimeValueType<TKey>(genericDefinition, valueType, entries);
    }

    [UnconditionalSuppressMessage("Trimming", "IL2060", Justification = "Dictionary construction uses runtime generic instantiation for parsed key types.")]
    [UnconditionalSuppressMessage("Aot", "IL3050", Justification = "Dictionary construction uses runtime generic instantiation for parsed key types.")]
    private static object BuildDictionaryContainerForRuntimeKeyType(
        Type genericDefinition,
        Type keyType,
        Type valueType,
        ParsedDictionaryEntry[] entries)
    {
        try
        {
            return BuildDictionaryContainerForRuntimeKeyMethod
                .MakeGenericMethod(keyType)
                .Invoke(null, [genericDefinition, valueType, entries])!;
        }
        catch (Exception exception)
        {
            throw UnwrapInvocationException(exception);
        }
    }

    [UnconditionalSuppressMessage("Trimming", "IL2060", Justification = "Dictionary construction uses runtime generic instantiation for parsed value types.")]
    [UnconditionalSuppressMessage("Aot", "IL3050", Justification = "Dictionary construction uses runtime generic instantiation for parsed value types.")]
    private static object BuildDictionaryContainerForRuntimeValueType<TKey>(
        Type genericDefinition,
        Type valueType,
        ParsedDictionaryEntry[] entries)
        where TKey : notnull
    {
        try
        {
            return BuildDictionaryContainerForRuntimeValueMethod
                .MakeGenericMethod(typeof(TKey), valueType)
                .Invoke(null, [genericDefinition, entries])!;
        }
        catch (Exception exception)
        {
            throw UnwrapInvocationException(exception);
        }
    }

    private static object BuildDictionaryContainerForRuntimeKey<TKey>(Type genericDefinition,
                                                                      Type valueType,
                                                                      ParsedDictionaryEntry[] entries)
        where TKey : notnull
    {
        return BuildDictionaryContainerForKey<TKey>(genericDefinition, valueType, entries);
    }

    private static object BuildDictionaryContainerForRuntimeValue<TKey, TValue>(Type genericDefinition,
                                                                                ParsedDictionaryEntry[] entries)
        where TKey : notnull
    {
        return BuildDictionaryContainer<TKey, TValue>(genericDefinition, entries);
    }

    private static object BuildDictionaryContainer<TKey, TValue>(Type genericDefinition,
                                                                 ParsedDictionaryEntry[] entries)
        where TKey : notnull
    {
        var values = CastDictionaryEntries<TKey, TValue>(entries);

        if (genericDefinition == typeof(ImmutableDictionary<,>))
        {
            var dictionary = ImmutableDictionary<TKey, TValue>.Empty;

            foreach (var (key, value) in values)
            {
                dictionary = dictionary.SetItem(key, value);
            }

            return dictionary;
        }

        if (genericDefinition == typeof(ImmutableSortedDictionary<,>))
        {
            var dictionary = ImmutableSortedDictionary<TKey, TValue>.Empty;

            foreach (var (key, value) in values)
            {
                dictionary = dictionary.SetItem(key, value);
            }

            return dictionary;
        }

        throw CreateUnsupportedContainerException(genericDefinition);
    }

    private static KeyValuePair<TKey, TValue>[] CastDictionaryEntries<TKey, TValue>(ParsedDictionaryEntry[] entries)
        where TKey : notnull
    {
        var values = new KeyValuePair<TKey, TValue>[entries.Length];

        for (var index = 0; index < entries.Length; index++)
        {
            values[index] = new KeyValuePair<TKey, TValue>((TKey)entries[index].Key!, (TValue)entries[index].Value!);
        }

        return values;
    }

    private static NotSupportedException CreateUnsupportedContainerException(Type containerType)
    {
        return new NotSupportedException($"Container '{containerType.FullName}' is not supported by {nameof(ContainerParser)}.");
    }

    private static bool IsDictionaryContainer(Type genericDefinition)
    {
        return genericDefinition == typeof(ImmutableDictionary<,>)
            || genericDefinition == typeof(ImmutableSortedDictionary<,>);
    }

    private static Exception UnwrapInvocationException(Exception exception)
    {
        return exception is TargetInvocationException { InnerException: { } inner }
            ? inner
            : exception;
    }

    private readonly record struct ParsedDictionaryEntry(object? Key, object? Value);
}
