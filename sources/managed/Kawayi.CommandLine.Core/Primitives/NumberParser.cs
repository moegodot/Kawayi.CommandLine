// Copyright (c) 2026 MoeGodot<me@kawayi.moe>.
// Licensed under the GNU Affero General Public License v3-or-later license.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Kawayi.CommandLine.Abstractions;

namespace Kawayi.CommandLine.Core.Primitives;

/// <summary>
/// Parses integral numeric types from command-line tokens.
/// </summary>
public sealed class NumberParser(Type targetType) : IBuiltInTypeProvider
{
    /// <summary>
    /// Gets the default number styles used for integral parsing.
    /// </summary>
    public static NumberStyles DefaultNumberStyles { get; }
        = NumberStyles.Integer;

    private readonly Type _targetType = ValidateTargetType(targetType);
    private readonly string _expectation = $"{targetType.Name} at NumberStyles.Integer";

    /// <summary>
    /// Parses an integral numeric value from the supplied tokens.
    /// </summary>
    public bool TryParse(ImmutableArray<Token> input,
                         TypeProviders typeProviders,
                         string? format,
                         [NotNullWhen(true)] out object? result,
                         [NotNullWhen(false)] out string? error)
    {
        if (input.IsDefaultOrEmpty)
        {
            result = CreateDefaultValue(_targetType);
            error = null;
            return true;
        }

        var token = input[^1].Value;

        if (TryParseCore(token, out result))
        {
            error = null;
            return true;
        }

        result = null;
        error = _expectation;
        return false;
    }

    private bool TryParseCore(string value, [NotNullWhen(true)] out object? result)
    {
        switch (Type.GetTypeCode(_targetType))
        {
            case TypeCode.Byte when byte.TryParse(value, DefaultNumberStyles, CultureInfo.InvariantCulture, out var parsedByte):
                result = parsedByte;
                return true;
            case TypeCode.SByte when sbyte.TryParse(value, DefaultNumberStyles, CultureInfo.InvariantCulture, out var parsedSByte):
                result = parsedSByte;
                return true;
            case TypeCode.UInt16 when ushort.TryParse(value, DefaultNumberStyles, CultureInfo.InvariantCulture, out var parsedUInt16):
                result = parsedUInt16;
                return true;
            case TypeCode.Int16 when short.TryParse(value, DefaultNumberStyles, CultureInfo.InvariantCulture, out var parsedInt16):
                result = parsedInt16;
                return true;
            case TypeCode.UInt32 when uint.TryParse(value, DefaultNumberStyles, CultureInfo.InvariantCulture, out var parsedUInt32):
                result = parsedUInt32;
                return true;
            case TypeCode.Int32 when int.TryParse(value, DefaultNumberStyles, CultureInfo.InvariantCulture, out var parsedInt32):
                result = parsedInt32;
                return true;
            case TypeCode.UInt64 when ulong.TryParse(value, DefaultNumberStyles, CultureInfo.InvariantCulture, out var parsedUInt64):
                result = parsedUInt64;
                return true;
            case TypeCode.Int64 when long.TryParse(value, DefaultNumberStyles, CultureInfo.InvariantCulture, out var parsedInt64):
                result = parsedInt64;
                return true;
            default:
                result = null;
                return false;
        }
    }

    private static Type ValidateTargetType(Type targetType)
    {
        ArgumentNullException.ThrowIfNull(targetType);

        return Type.GetTypeCode(targetType) switch
        {
            TypeCode.Byte or TypeCode.SByte or TypeCode.UInt16 or TypeCode.Int16 or TypeCode.UInt32 or TypeCode.Int32 or TypeCode.UInt64 or TypeCode.Int64
                => targetType,
            _ => throw new ArgumentException($"Type '{targetType.FullName}' is not a supported integral type.", nameof(targetType))
        };
    }

    private static object CreateDefaultValue(Type targetType)
    {
        return Type.GetTypeCode(targetType) switch
        {
            TypeCode.Byte => default(byte),
            TypeCode.SByte => default(sbyte),
            TypeCode.UInt16 => default(ushort),
            TypeCode.Int16 => default(short),
            TypeCode.UInt32 => default(uint),
            TypeCode.Int32 => default(int),
            TypeCode.UInt64 => default(ulong),
            TypeCode.Int64 => default(long),
            _ => throw new InvalidOperationException($"Unable to create a default value for '{targetType.FullName}'.")
        };
    }
}
