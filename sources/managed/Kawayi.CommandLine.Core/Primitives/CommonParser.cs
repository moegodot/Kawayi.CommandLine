// Copyright (c) 2026 MoeGodot<me@kawayi.moe>.
// Licensed under the GNU Affero General Public License v3-or-later license.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Kawayi.CommandLine.Abstractions;

namespace Kawayi.CommandLine.Core.Primitives;

/// <summary>
/// Parses common framework types from command-line tokens.
/// </summary>
public sealed class CommonParser(Type targetType) : IBuiltInTypeProvider
{
    private readonly Type _targetType = ValidateTargetType(targetType);

    /// <summary>
    /// Parses a common framework value from the supplied tokens.
    /// </summary>
    public bool TryParse(ImmutableArray<Token> input,
                         TypeProviders typeProviders,
                         string? format,
                         [NotNullWhen(true)] out object? result,
                         [NotNullWhen(false)] out string? error)
    {
        if (input.IsDefaultOrEmpty)
        {
            return TryGetDefaultValue(out result, out error);
        }

        var value = input[^1].Value;

        switch (Type.GetTypeCode(_targetType))
        {
            case TypeCode.String:
                result = value;
                error = null;
                return true;
            case TypeCode.Object when _targetType == typeof(Guid):
                if (Guid.TryParse(value, out var parsedGuid))
                {
                    result = parsedGuid;
                    error = null;
                    return true;
                }

                result = null;
                error = "Guid";
                return false;
            case TypeCode.Object when _targetType == typeof(Uri):
                if (Uri.TryCreate(value, UriKind.RelativeOrAbsolute, out var uri) && uri is not null)
                {
                    result = uri;
                    error = null;
                    return true;
                }

                result = null;
                error = "Uri at UriKind.RelativeOrAbsolute";
                return false;
            case TypeCode.DateTime:
                return TryParseTemporal(
                    value,
                    format,
                    "DateTime",
                    static (string raw, out DateTime parsed) =>
                        DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out parsed),
                    static (string raw, string exactFormat, out DateTime parsed) =>
                        DateTime.TryParseExact(raw, exactFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out parsed),
                    out result,
                    out error);
            case TypeCode.Object when _targetType == typeof(DateTimeOffset):
                return TryParseTemporal(
                    value,
                    format,
                    "DateTimeOffset",
                    static (string raw, out DateTimeOffset parsed) =>
                        DateTimeOffset.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out parsed),
                    static (string raw, string exactFormat, out DateTimeOffset parsed) =>
                        DateTimeOffset.TryParseExact(raw, exactFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out parsed),
                    out result,
                    out error);
            case TypeCode.Object when _targetType == typeof(DateOnly):
                return TryParseTemporal(
                    value,
                    format,
                    "DateOnly",
                    static (string raw, out DateOnly parsed) =>
                        DateOnly.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out parsed),
                    static (string raw, string exactFormat, out DateOnly parsed) =>
                        DateOnly.TryParseExact(raw, exactFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out parsed),
                    out result,
                    out error);
            case TypeCode.Object when _targetType == typeof(TimeOnly):
                return TryParseTemporal(
                    value,
                    format,
                    "TimeOnly",
                    static (string raw, out TimeOnly parsed) =>
                        TimeOnly.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out parsed),
                    static (string raw, string exactFormat, out TimeOnly parsed) =>
                        TimeOnly.TryParseExact(raw, exactFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out parsed),
                    out result,
                    out error);
            default:
                result = null;
                error = _targetType.FullName ?? _targetType.Name;
                return false;
        }
    }

    private static Type ValidateTargetType(Type targetType)
    {
        ArgumentNullException.ThrowIfNull(targetType);

        if (targetType == typeof(string)
            || targetType == typeof(Guid)
            || targetType == typeof(Uri)
            || targetType == typeof(DateTime)
            || targetType == typeof(DateTimeOffset)
            || targetType == typeof(DateOnly)
            || targetType == typeof(TimeOnly))
        {
            return targetType;
        }

        throw new ArgumentException($"Type '{targetType.FullName}' is not a supported common parser target.", nameof(targetType));
    }

    private bool TryGetDefaultValue([NotNullWhen(true)] out object? result,
                                    [NotNullWhen(false)] out string? error)
    {
        if (_targetType == typeof(string))
        {
            result = string.Empty;
            error = null;
            return true;
        }

        if (_targetType == typeof(Uri))
        {
            result = new Uri(string.Empty, UriKind.Relative);
            error = null;
            return true;
        }

        if (_targetType == typeof(Guid))
        {
            result = Guid.Empty;
            error = null;
            return true;
        }

        if (_targetType == typeof(DateTime))
        {
            result = default(DateTime);
            error = null;
            return true;
        }

        if (_targetType == typeof(DateTimeOffset))
        {
            result = default(DateTimeOffset);
            error = null;
            return true;
        }

        if (_targetType == typeof(DateOnly))
        {
            result = default(DateOnly);
            error = null;
            return true;
        }

        if (_targetType == typeof(TimeOnly))
        {
            result = default(TimeOnly);
            error = null;
            return true;
        }

        result = null;
        error = _targetType.FullName ?? _targetType.Name;
        return false;
    }

    private delegate bool TryParseDelegate<T>(string value, out T result);
    private delegate bool TryParseExactDelegate<T>(string value, string format, out T result);

    private static bool TryParseTemporal<T>(string value,
                                            string? format,
                                            string typeName,
                                            TryParseDelegate<T> tryParse,
                                            TryParseExactDelegate<T> tryParseExact,
                                            [NotNullWhen(true)] out object? result,
                                            [NotNullWhen(false)] out string? error)
    {
        if (string.IsNullOrWhiteSpace(format))
        {
            if (tryParse(value, out var parsedValue))
            {
                result = parsedValue!;
                error = null;
                return true;
            }

            result = null;
            error = $"{typeName} at DateTimeStyles.None";
            return false;
        }

        var exactFormat = format!;
        if (tryParseExact(value, exactFormat, out var exactValue))
        {
            result = exactValue!;
            error = null;
            return true;
        }

        result = null;
        error = $"{typeName} at exact format '{exactFormat}'";
        return false;
    }
}
