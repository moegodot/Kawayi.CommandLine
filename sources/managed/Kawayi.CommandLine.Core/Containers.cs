// Copyright (c) 2026 MoeGodot<me@kawayi.moe>.
// Licensed under the GNU Affero General Public License v3-or-later license.

using System.Collections.Immutable;
using System.Reflection;
using Kawayi.CommandLine.Abstractions;
using Kawayi.CommandLine.Core.Primitives;
using Kawayi.Escapes;

namespace Kawayi.CommandLine.Core;

public class Containers
    : Abstractions.IParsable<ContainerType>
{
    public static IEscapeRule DefaultDictionaryEscapeRule { get; } = new SimpleEscapeRule(
        [new(@"\", @"\\"), new(@"=", @"\=")]);

    public static ParsingResult CreateParsing(ParsingOptions options,
                                              ImmutableArray<Token> arguments,
                                              ContainerType initialState)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(initialState);
        ArgumentNullException.ThrowIfNull(initialState.Container);
        ArgumentNullException.ThrowIfNull(initialState.ValueType);

        if (!initialState.Container.IsConstructedGenericType)
        {
            return CreateUnsupportedContainerResult(initialState.Container);
        }

        var genericDefinition = initialState.Container.GetGenericTypeDefinition();

        return IsDictionaryContainer(genericDefinition)
            ? CreateDictionaryParsing(options, arguments, initialState, genericDefinition)
            : CreateSequenceParsing(options, arguments, initialState, genericDefinition);
    }

    private static ParsingResult CreateSequenceParsing(ParsingOptions options,
                                                       ImmutableArray<Token> arguments,
                                                       ContainerType containerType,
                                                       Type genericDefinition)
    {
        var parsedValues = new object?[arguments.Length];

        for (var index = 0; index < arguments.Length; index++)
        {
            var result = ParseValue(options, arguments[index].RawValue, containerType.ValueType);

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
                                                         Type genericDefinition)
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

            if (!TrySplitDictionaryEntry(token.RawValue, out var rawKey, out var rawValue))
            {
                return new InvalidArgumentDetected(token.RawValue, "key=value", null);
            }

            var keyResult = ParseValue(options,
                                       DefaultDictionaryEscapeRule.Unescape(rawKey),
                                       containerType.KeyType);

            if (keyResult is not ParsingFinished parsedKey)
            {
                return keyResult;
            }

            var valueResult = ParseValue(options,
                                         DefaultDictionaryEscapeRule.Unescape(rawValue),
                                         containerType.ValueType);

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
        try
        {
            var typedArray = CreateTypedArray(valueType, parsedValues);

            var result = genericDefinition == typeof(ImmutableArray<>)
                ? InvokeGenericStaticMethod(typeof(ImmutableArray), nameof(ImmutableArray.CreateRange), [valueType], typedArray)
                : genericDefinition == typeof(ImmutableList<>)
                    ? InvokeGenericStaticMethod(typeof(ImmutableList), nameof(ImmutableList.CreateRange), [valueType], typedArray)
                    : genericDefinition == typeof(ImmutableQueue<>)
                        ? InvokeGenericStaticMethod(typeof(ImmutableQueue), nameof(ImmutableQueue.CreateRange), [valueType], typedArray)
                        : genericDefinition == typeof(ImmutableStack<>)
                            ? InvokeGenericStaticMethod(typeof(ImmutableStack), nameof(ImmutableStack.CreateRange), [valueType], typedArray)
                            : genericDefinition == typeof(ImmutableSortedSet<>)
                                ? InvokeGenericStaticMethod(typeof(ImmutableSortedSet), nameof(ImmutableSortedSet.CreateRange), [valueType], typedArray)
                                : genericDefinition == typeof(ImmutableHashSet<>)
                                    ? InvokeGenericStaticMethod(typeof(ImmutableHashSet), nameof(ImmutableHashSet.CreateRange), [valueType], typedArray)
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

    private static ParsingResult BuildDictionaryContainer(Type genericDefinition,
                                                          Type keyType,
                                                          Type valueType,
                                                          ParsedDictionaryEntry[] entries)
    {
        try
        {
            var typedArray = CreateTypedKeyValuePairArray(keyType, valueType, entries);
            var closedContainerType = genericDefinition.MakeGenericType(keyType, valueType);
            var emptyInstance = closedContainerType.GetField("Empty", BindingFlags.Public | BindingFlags.Static)
                ?.GetValue(null);

            if (emptyInstance is null)
            {
                throw new MissingFieldException(closedContainerType.FullName, "Empty");
            }

            var result = genericDefinition == typeof(ImmutableDictionary<,>)
                         || genericDefinition == typeof(ImmutableSortedDictionary<,>)
                ? InvokeInstanceMethod(emptyInstance, "SetItems", typedArray)
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

    private static ParsingResult ParseValue(ParsingOptions options, string rawValue, Type targetType)
    {
        if (targetType == typeof(string))
        {
            return new ParsingFinished<string>(rawValue);
        }

        ImmutableArray<Token> arguments = [new ArgumentOrCommandToken(rawValue)];

        if (targetType == typeof(bool))
        {
            return BooleanParser.CreateParsing(options, arguments, false);
        }

        if (targetType == typeof(byte))
        {
            return NumberParser.CreateParsing(options, arguments, (byte)0);
        }

        if (targetType == typeof(sbyte))
        {
            return NumberParser.CreateParsing(options, arguments, (sbyte)0);
        }

        if (targetType == typeof(ushort))
        {
            return NumberParser.CreateParsing(options, arguments, (ushort)0);
        }

        if (targetType == typeof(short))
        {
            return NumberParser.CreateParsing(options, arguments, (short)0);
        }

        if (targetType == typeof(uint))
        {
            return NumberParser.CreateParsing(options, arguments, 0u);
        }

        if (targetType == typeof(int))
        {
            return NumberParser.CreateParsing(options, arguments, 0);
        }

        if (targetType == typeof(ulong))
        {
            return NumberParser.CreateParsing(options, arguments, 0UL);
        }

        if (targetType == typeof(long))
        {
            return NumberParser.CreateParsing(options, arguments, 0L);
        }

        if (targetType == typeof(float))
        {
            return FloatParser.CreateParsing(options, arguments, 0f);
        }

        if (targetType == typeof(double))
        {
            return FloatParser.CreateParsing(options, arguments, 0d);
        }

        if (targetType == typeof(decimal))
        {
            return FloatParser.CreateParsing(options, arguments, decimal.Zero);
        }

        if (targetType == typeof(Guid))
        {
            return CommonParser.CreateParsing(options, arguments, Guid.Empty);
        }

        if (targetType == typeof(Uri))
        {
            return CommonParser.CreateParsing(options, arguments, new Uri("https://placeholder.invalid"));
        }

        if (targetType == typeof(DateTime))
        {
            return CommonParser.CreateParsing(options, arguments, default(DateTime));
        }

        if (targetType == typeof(DateTimeOffset))
        {
            return CommonParser.CreateParsing(options, arguments, default(DateTimeOffset));
        }

        if (targetType == typeof(DateOnly))
        {
            return CommonParser.CreateParsing(options, arguments, default(DateOnly));
        }

        if (targetType == typeof(TimeOnly))
        {
            return CommonParser.CreateParsing(options, arguments, default(TimeOnly));
        }

        return new GotError(new NotSupportedException(
            $"Type '{targetType.FullName}' is not supported by {nameof(Containers)}."));
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

    private static Array CreateTypedArray(Type elementType, object?[] values)
    {
        var array = Array.CreateInstance(elementType, values.Length);

        for (var index = 0; index < values.Length; index++)
        {
            array.SetValue(values[index], index);
        }

        return array;
    }

    private static Array CreateTypedKeyValuePairArray(Type keyType,
                                                      Type valueType,
                                                      ParsedDictionaryEntry[] entries)
    {
        var pairType = typeof(KeyValuePair<,>).MakeGenericType(keyType, valueType);
        var array = Array.CreateInstance(pairType, entries.Length);

        for (var index = 0; index < entries.Length; index++)
        {
            array.SetValue(Activator.CreateInstance(pairType, entries[index].Key, entries[index].Value), index);
        }

        return array;
    }

    private static object InvokeGenericStaticMethod(Type declaringType,
                                                    string methodName,
                                                    Type[] genericArguments,
                                                    params object?[] arguments)
    {
        foreach (var candidate in declaringType
                     .GetMethods(BindingFlags.Public | BindingFlags.Static)
                     .Where(static method => method.IsGenericMethodDefinition))
        {
            if (candidate.Name != methodName ||
                candidate.GetGenericArguments().Length != genericArguments.Length ||
                candidate.GetParameters().Length != arguments.Length)
            {
                continue;
            }

            var closedMethod = candidate.MakeGenericMethod(genericArguments);
            var parameters = closedMethod.GetParameters();

            var matches = true;

            for (var index = 0; index < parameters.Length; index++)
            {
                var argument = arguments[index];

                if (argument is null)
                {
                    if (parameters[index].ParameterType.IsValueType &&
                        Nullable.GetUnderlyingType(parameters[index].ParameterType) is null)
                    {
                        matches = false;
                        break;
                    }

                    continue;
                }

                if (!parameters[index].ParameterType.IsInstanceOfType(argument))
                {
                    matches = false;
                    break;
                }
            }

            if (matches)
            {
                return closedMethod.Invoke(null, arguments)
                    ?? throw new InvalidOperationException(
                        $"Method '{declaringType.FullName}.{methodName}' returned null.");
            }
        }

        throw new MissingMethodException(
            $"Unable to resolve '{declaringType.FullName}.{methodName}' for {string.Join(", ", genericArguments.Select(static type => type.FullName))}.");
    }

    private static object InvokeInstanceMethod(object instance,
                                               string methodName,
                                               params object?[] arguments)
    {
        var declaringType = instance.GetType();

        foreach (var candidate in declaringType.GetMethods(BindingFlags.Public | BindingFlags.Instance))
        {
            if (candidate.Name != methodName || candidate.GetParameters().Length != arguments.Length)
            {
                continue;
            }

            var parameters = candidate.GetParameters();
            var matches = true;

            for (var index = 0; index < parameters.Length; index++)
            {
                var argument = arguments[index];

                if (argument is null)
                {
                    if (parameters[index].ParameterType.IsValueType &&
                        Nullable.GetUnderlyingType(parameters[index].ParameterType) is null)
                    {
                        matches = false;
                        break;
                    }

                    continue;
                }

                if (!parameters[index].ParameterType.IsInstanceOfType(argument))
                {
                    matches = false;
                    break;
                }
            }

            if (matches)
            {
                return candidate.Invoke(instance, arguments)
                    ?? throw new InvalidOperationException(
                        $"Method '{declaringType.FullName}.{methodName}' returned null.");
            }
        }

        throw new MissingMethodException(
            $"Unable to resolve instance method '{declaringType.FullName}.{methodName}'.");
    }

    private static ParsingResult CreateUnsupportedContainerResult(Type containerType)
    {
        return new GotError(new NotSupportedException(
            $"Container '{containerType.FullName}' is not supported by {nameof(Containers)}."));
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

    private readonly record struct ParsedDictionaryEntry(object Key, object Value);
}
