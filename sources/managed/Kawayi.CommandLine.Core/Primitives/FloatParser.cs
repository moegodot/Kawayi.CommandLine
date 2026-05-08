// Copyright (c) 2026 MoeGodot<me@kawayi.moe>.
// Licensed under the GNU Affero General Public License v3-or-later license.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Kawayi.CommandLine.Abstractions;

namespace Kawayi.CommandLine.Core.Primitives;

/// <summary>
/// Parses floating-point numeric types from command-line tokens.
/// </summary>
public sealed class FloatParser(Type targetType) : IBuiltInTypeProvider
{
    /// <summary>
    /// Gets the default number styles used for floating-point parsing.
    /// </summary>
    public static NumberStyles DefaultNumberStyles { get; }
        = NumberStyles.Float;

    private readonly Type _targetType = ValidateTargetType(targetType);
    private readonly string _expectation = $"{targetType.Name} at NumberStyles.Float";

    /// <summary>
    /// Parses a floating-point numeric value from the supplied tokens.
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
            case TypeCode.Single when float.TryParse(value, DefaultNumberStyles, CultureInfo.InvariantCulture, out var parsedSingle):
                result = parsedSingle;
                return true;
            case TypeCode.Double when double.TryParse(value, DefaultNumberStyles, CultureInfo.InvariantCulture, out var parsedDouble):
                result = parsedDouble;
                return true;
            case TypeCode.Decimal when decimal.TryParse(value, DefaultNumberStyles, CultureInfo.InvariantCulture, out var parsedDecimal):
                result = parsedDecimal;
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
            TypeCode.Single or TypeCode.Double or TypeCode.Decimal
                => targetType,
            _ => throw new ArgumentException($"Type '{targetType.FullName}' is not a supported floating-point type.", nameof(targetType))
        };
    }

    private static object CreateDefaultValue(Type targetType)
    {
        return Type.GetTypeCode(targetType) switch
        {
            TypeCode.Single => default(float),
            TypeCode.Double => default(double),
            TypeCode.Decimal => default(decimal),
            _ => throw new InvalidOperationException($"Unable to create a default value for '{targetType.FullName}'.")
        };
    }
}
