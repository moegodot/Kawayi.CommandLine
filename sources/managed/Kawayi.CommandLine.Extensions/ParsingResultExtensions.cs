// Copyright (c) 2026 MoeGodot<me@kawayi.moe>.
// Licensed under the GNU Affero General Public License v3-or-later license.

using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Kawayi.CommandLine.Abstractions;
using Kawayi.CommandLine.Core;

namespace Kawayi.CommandLine.Extensions;

/// <summary>
/// Provides helper APIs for working with parsing results and typed parsing outputs.
/// </summary>
public static class ParsingResultExtensions
{
    extension(ParsingResult result)
    {
        /// <summary>
        /// Extracts a successful parsing result as the requested type.
        /// </summary>
        public T Expect<T>()
        {
            return result is ParsingFinished { UntypedResult: T v }
                ? v
                : throw new ArgumentException($"expect {typeof(T).FullName}, get {result}");
        }
    }

    extension(IParsingResultCollection result)
    {
        /// <summary>
        /// Tries to resolve the effective value for a definition in the current scope by applying
        /// explicit values, default value factories, and CLR defaults in that order.
        /// </summary>
        public bool TryGetEffectiveValue(TypedDefinition definition, out object? value)
        {
            ArgumentNullException.ThrowIfNull(result);
            ArgumentNullException.ThrowIfNull(definition);

            if (!result.Scope.AvailableTypedDefinitions.Any(candidate =>
                    EqualityComparer<TypedDefinition>.Default.Equals(candidate, definition)))
            {
                value = null;
                return false;
            }

            if (result.TryGetValue(definition, out value))
            {
                return true;
            }

            if (definition.DefaultValueFactory is not null)
            {
                value = definition.DefaultValueFactory(result);
                return true;
            }

            if (definition.Requirement)
            {
                value = null;
                return false;
            }

            value = GetClrDefault(definition.Type);
            return true;
        }

        /// <summary>
        /// Gets the effective value for a definition in the current scope by applying explicit values,
        /// default value factories, and CLR defaults in that order.
        /// </summary>
        public object? GetEffectiveValueOrDefault(TypedDefinition definition)
        {
            ArgumentNullException.ThrowIfNull(result);
            ArgumentNullException.ThrowIfNull(definition);

            if (!result.Scope.AvailableTypedDefinitions.Any(candidate =>
                    EqualityComparer<TypedDefinition>.Default.Equals(candidate, definition)))
            {
                throw new InvalidOperationException(
                    $"Definition '{definition.Information.Name.Value}' is not available in the current scope.");
            }

            if (result.TryGetValue(definition, out var explicitValue))
            {
                return explicitValue;
            }

            if (definition.DefaultValueFactory is not null)
            {
                return definition.DefaultValueFactory(result);
            }

            if (definition.Requirement)
            {
                throw new InvalidOperationException(
                    $"Required definition '{definition.Information.Name.Value}' does not have an explicit value or default factory.");
            }

            return GetClrDefault(definition.Type);
        }
    }

    private static object? GetClrDefault(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] Type type)
    {
        return TypeDefaultValues.GetValue(type);
    }
}
