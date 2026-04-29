// Copyright (c) 2026 MoeGodot<me@kawayi.moe>.
// Licensed under the GNU Affero General Public License v3-or-later license.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;

namespace Kawayi.CommandLine.Core;

/// <summary>
/// Provides CLR default values for the built-in parsing types without relying on runtime reflection.
/// </summary>
public static class TypeDefaultValues
{
    /// <summary>
    /// Gets the CLR default value for a supported parsing type.
    /// </summary>
    public static object? GetValue(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] Type type)
    {
        ArgumentNullException.ThrowIfNull(type);

        if (!type.IsValueType)
        {
            return null;
        }

        if (type == typeof(bool))
        {
            return false;
        }

        if (type == typeof(byte))
        {
            return (byte)0;
        }

        if (type == typeof(sbyte))
        {
            return (sbyte)0;
        }

        if (type == typeof(ushort))
        {
            return (ushort)0;
        }

        if (type == typeof(short))
        {
            return (short)0;
        }

        if (type == typeof(uint))
        {
            return 0u;
        }

        if (type == typeof(int))
        {
            return 0;
        }

        if (type == typeof(ulong))
        {
            return 0UL;
        }

        if (type == typeof(long))
        {
            return 0L;
        }

        if (type == typeof(float))
        {
            return 0f;
        }

        if (type == typeof(double))
        {
            return 0d;
        }

        if (type == typeof(decimal))
        {
            return decimal.Zero;
        }

        if (type == typeof(Guid))
        {
            return Guid.Empty;
        }

        if (type == typeof(DateTime))
        {
            return default(DateTime);
        }

        if (type == typeof(DateTimeOffset))
        {
            return default(DateTimeOffset);
        }

        if (type == typeof(DateOnly))
        {
            return default(DateOnly);
        }

        if (type == typeof(TimeOnly))
        {
            return default(TimeOnly);
        }

        if (type.IsConstructedGenericType
            && type.GetGenericTypeDefinition() == typeof(ImmutableArray<>)
            && TryGetImmutableArrayDefault(type.GetGenericArguments()[0], out var immutableArrayDefault))
        {
            return immutableArrayDefault;
        }

        return Activator.CreateInstance(type);
    }

    private static bool TryGetImmutableArrayDefault(Type elementType, out object? value)
    {
        if (elementType == typeof(bool))
        {
            value = default(ImmutableArray<bool>);
            return true;
        }

        if (elementType == typeof(byte))
        {
            value = default(ImmutableArray<byte>);
            return true;
        }

        if (elementType == typeof(sbyte))
        {
            value = default(ImmutableArray<sbyte>);
            return true;
        }

        if (elementType == typeof(ushort))
        {
            value = default(ImmutableArray<ushort>);
            return true;
        }

        if (elementType == typeof(short))
        {
            value = default(ImmutableArray<short>);
            return true;
        }

        if (elementType == typeof(uint))
        {
            value = default(ImmutableArray<uint>);
            return true;
        }

        if (elementType == typeof(int))
        {
            value = default(ImmutableArray<int>);
            return true;
        }

        if (elementType == typeof(ulong))
        {
            value = default(ImmutableArray<ulong>);
            return true;
        }

        if (elementType == typeof(long))
        {
            value = default(ImmutableArray<long>);
            return true;
        }

        if (elementType == typeof(float))
        {
            value = default(ImmutableArray<float>);
            return true;
        }

        if (elementType == typeof(double))
        {
            value = default(ImmutableArray<double>);
            return true;
        }

        if (elementType == typeof(decimal))
        {
            value = default(ImmutableArray<decimal>);
            return true;
        }

        if (elementType == typeof(Guid))
        {
            value = default(ImmutableArray<Guid>);
            return true;
        }

        if (elementType == typeof(string))
        {
            value = default(ImmutableArray<string>);
            return true;
        }

        if (elementType == typeof(Uri))
        {
            value = default(ImmutableArray<Uri>);
            return true;
        }

        if (elementType == typeof(DateTime))
        {
            value = default(ImmutableArray<DateTime>);
            return true;
        }

        if (elementType == typeof(DateTimeOffset))
        {
            value = default(ImmutableArray<DateTimeOffset>);
            return true;
        }

        if (elementType == typeof(DateOnly))
        {
            value = default(ImmutableArray<DateOnly>);
            return true;
        }

        if (elementType == typeof(TimeOnly))
        {
            value = default(ImmutableArray<TimeOnly>);
            return true;
        }

        value = null;
        return false;
    }
}
