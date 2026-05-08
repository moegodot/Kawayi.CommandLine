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
public class ContainerParser
{
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
    /// Parses a supported container value from the supplied tokens.
    /// </summary>
    /// <param name="options">The parsing options for this operation.</param>
    /// <param name="arguments">The tokens to parse.</param>
    /// <param name="initialState">The container type descriptor to populate.</param>
    /// <param name="format">The optional format hint used for element parsing.</param>
    /// <returns>The parsing result.</returns>
    public static ParsingResult CreateParsing(ParsingOptions options,
                                              ImmutableArray<Token> arguments,
                                              ContainerType initialState,
                                              string? format = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(initialState);
        ArgumentNullException.ThrowIfNull(initialState.Container);
        ArgumentNullException.ThrowIfNull(initialState.ValueType);

        if (!initialState.Container.IsConstructedGenericType)
        {
            return EmitContainerDebug(options,
                                      CreateUnsupportedContainerResult(initialState.Container),
                                      arguments,
                                      initialState);
        }

        var genericDefinition = initialState.Container.GetGenericTypeDefinition();

        var result = IsDictionaryContainer(genericDefinition)
            ? CreateDictionaryParsing(options, arguments, initialState, genericDefinition, format)
            : CreateSequenceParsing(options, arguments, initialState, genericDefinition, format);

        return EmitContainerDebug(options, result, arguments, initialState);
    }

    private static ParsingResult CreateSequenceParsing(ParsingOptions options,
                                                       ImmutableArray<Token> arguments,
                                                       ContainerType containerType,
                                                       Type genericDefinition,
                                                       string? format)
    {
        var parsedValues = new object?[arguments.Length];

        for (var index = 0; index < arguments.Length; index++)
        {
            var result = ParseValue(options, arguments[index].Value, containerType.ValueType, format);

            if (result is not ParsingFinished finished)
            {
                return result;
            }

            parsedValues[index] = finished.UntypedResult;
        }

        return BuildSequenceContainer(genericDefinition, containerType.ValueType, parsedValues);
    }

    private static ParsingResult CreateDictionaryParsing(ParsingOptions options,
                                                         ImmutableArray<Token> arguments,
                                                         ContainerType containerType,
                                                         Type genericDefinition,
                                                         string? format)
    {
        if (containerType.KeyType is null)
        {
            return new GotError(new NotSupportedException(
                $"Dictionary container '{containerType.Container.FullName}' must provide a key type."));
        }

        var parsedEntries = new ParsedDictionaryEntry[arguments.Length];

        for (var index = 0; index < arguments.Length; index++)
        {
            var token = arguments[index];

            if (!TrySplitDictionaryEntry(token.Value, out var rawKey, out var rawValue))
            {
                return new InvalidArgumentDetected(token.Value, "key=value", null);
            }

            var keyResult = ParseValue(options,
                                       DefaultDictionaryEscapeRule.Unescape(rawKey),
                                       containerType.KeyType,
                                       format);

            if (keyResult is not ParsingFinished parsedKey)
            {
                return keyResult;
            }

            var valueResult = ParseValue(options,
                                         DefaultDictionaryEscapeRule.Unescape(rawValue),
                                         containerType.ValueType,
                                         format);

            if (valueResult is not ParsingFinished parsedValue)
            {
                return valueResult;
            }

            parsedEntries[index] = new(parsedKey.UntypedResult, parsedValue.UntypedResult);
        }

        return BuildDictionaryContainer(genericDefinition,
                                        containerType.KeyType,
                                        containerType.ValueType,
                                        parsedEntries);
    }

    private static ParsingResult BuildSequenceContainer(Type genericDefinition,
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

    private static ParsingResult BuildDictionaryContainer(Type genericDefinition,
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

    private static ParsingResult ParseValue(ParsingOptions options,
                                            string rawValue,
                                            Type targetType,
                                            string? format)
    {
        ImmutableArray<Token> arguments = [new ArgumentOrCommandToken(rawValue)];
        return TypeProviderResolver.ParseValue(options, arguments, targetType, format, nameof(ContainerParser));
    }

    private static ParsingResult EmitContainerDebug(ParsingOptions options,
                                                    ParsingResult result,
                                                    ImmutableArray<Token> arguments,
                                                    ContainerType containerType)
    {
        return DebugOutput.Emit(options,
                                result,
                                new DebugContext(nameof(ContainerParser),
                                                 Tokens: arguments,
                                                 TargetType: containerType.Container,
                                                 Expectation: containerType.Container.FullName
                                                     ?? containerType.Container.Name));
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

    private static ParsingResult BuildSequenceContainer<T>(Type genericDefinition, object?[] parsedValues)
    {
        try
        {
            var values = CastSequenceValues<T>(parsedValues);

            object? result = genericDefinition == typeof(ImmutableArray<>)
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
                                    : null;

            return result is not null
                ? new ParsingFinished<object>(result)
                : CreateUnsupportedContainerResult(genericDefinition);
        }
        catch (Exception exception)
        {
            return new GotError(UnwrapInvocationException(exception));
        }
    }

    [UnconditionalSuppressMessage("Trimming", "IL2060", Justification = "Container construction uses runtime generic instantiation for parsed element types.")]
    [UnconditionalSuppressMessage("Aot", "IL3050", Justification = "Container construction uses runtime generic instantiation for parsed element types.")]
    private static ParsingResult BuildSequenceContainerForRuntimeType(
        Type genericDefinition,
        Type valueType,
        object?[] parsedValues)
    {
        try
        {
            return (ParsingResult)BuildSequenceContainerRuntimeMethod
                .MakeGenericMethod(valueType)
                .Invoke(null, [genericDefinition, parsedValues])!;
        }
        catch (Exception exception)
        {
            return new GotError(UnwrapInvocationException(exception));
        }
    }

    private static ParsingResult BuildSequenceContainerRuntime<T>(Type genericDefinition, object?[] parsedValues)
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

    private static ParsingResult BuildDictionaryContainerForKey<TKey>(Type genericDefinition,
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
    private static ParsingResult BuildDictionaryContainerForRuntimeKeyType(
        Type genericDefinition,
        Type keyType,
        Type valueType,
        ParsedDictionaryEntry[] entries)
    {
        try
        {
            return (ParsingResult)BuildDictionaryContainerForRuntimeKeyMethod
                .MakeGenericMethod(keyType)
                .Invoke(null, [genericDefinition, valueType, entries])!;
        }
        catch (Exception exception)
        {
            return new GotError(UnwrapInvocationException(exception));
        }
    }

    [UnconditionalSuppressMessage("Trimming", "IL2060", Justification = "Dictionary construction uses runtime generic instantiation for parsed value types.")]
    [UnconditionalSuppressMessage("Aot", "IL3050", Justification = "Dictionary construction uses runtime generic instantiation for parsed value types.")]
    private static ParsingResult BuildDictionaryContainerForRuntimeValueType<TKey>(
        Type genericDefinition,
        Type valueType,
        ParsedDictionaryEntry[] entries)
        where TKey : notnull
    {
        try
        {
            return (ParsingResult)BuildDictionaryContainerForRuntimeValueMethod
                .MakeGenericMethod(typeof(TKey), valueType)
                .Invoke(null, [genericDefinition, entries])!;
        }
        catch (Exception exception)
        {
            return new GotError(UnwrapInvocationException(exception));
        }
    }

    private static ParsingResult BuildDictionaryContainerForRuntimeKey<TKey>(Type genericDefinition,
                                                                             Type valueType,
                                                                             ParsedDictionaryEntry[] entries)
        where TKey : notnull
    {
        return BuildDictionaryContainerForKey<TKey>(genericDefinition, valueType, entries);
    }

    private static ParsingResult BuildDictionaryContainerForRuntimeValue<TKey, TValue>(Type genericDefinition,
                                                                                       ParsedDictionaryEntry[] entries)
        where TKey : notnull
    {
        return BuildDictionaryContainer<TKey, TValue>(genericDefinition, entries);
    }

    private static ParsingResult BuildDictionaryContainer<TKey, TValue>(Type genericDefinition,
                                                                        ParsedDictionaryEntry[] entries)
        where TKey : notnull
    {
        try
        {
            var values = CastDictionaryEntries<TKey, TValue>(entries);

            if (genericDefinition == typeof(ImmutableDictionary<,>))
            {
                var dictionary = ImmutableDictionary<TKey, TValue>.Empty;

                foreach (var (key, value) in values)
                {
                    dictionary = dictionary.SetItem(key, value);
                }

                return new ParsingFinished<object>(dictionary);
            }

            if (genericDefinition == typeof(ImmutableSortedDictionary<,>))
            {
                var dictionary = ImmutableSortedDictionary<TKey, TValue>.Empty;

                foreach (var (key, value) in values)
                {
                    dictionary = dictionary.SetItem(key, value);
                }

                return new ParsingFinished<object>(dictionary);
            }

            return CreateUnsupportedContainerResult(genericDefinition);
        }
        catch (Exception exception)
        {
            return new GotError(UnwrapInvocationException(exception));
        }
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

    private static ParsingResult CreateUnsupportedContainerResult(Type containerType)
    {
        return new GotError(new NotSupportedException(
            $"Container '{containerType.FullName}' is not supported by {nameof(ContainerParser)}."));
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
