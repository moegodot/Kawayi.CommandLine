// Copyright (c) 2026 MoeGodot<me@kawayi.moe>.
// Licensed under the GNU Affero General Public License v3-or-later license.

using System.Diagnostics.CodeAnalysis;
using Kawayi.CommandLine.Abstractions;
using Kawayi.CommandLine.Core;

namespace Kawayi.CommandLine.Extensions;

/// <summary>
/// Adds convenience helpers for working with parsed command result trees.
/// </summary>
public static class IParsingResultCollectionExtensions
{
    extension(IParsingResultCollection result)
    {
        /// <summary>
        /// Creates a new bindable command object and populates it from the parsing result collection.
        /// </summary>
        /// <returns>The populated command object.</returns>
        public T Bind<T>()
            where T : IBindable, new()
        {
            ArgumentNullException.ThrowIfNull(result);

            var obj = new T();
            obj.Bind(result);
            return obj;
        }

        /// <summary>
        /// get the root command of a subcommand's <see cref="IParsingResultCollection"/>
        /// </summary>
        /// <returns>the <see cref="IParsingResultCollection"/> that <see cref="IParsingResultCollection.Parent"/> is <see langword="null"/></returns>
        public IParsingResultCollection GetRootCommand()
        {
            while (result.Parent != null)
            {
                result = result.Parent;
            }

            return result;
        }


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
                return DoesEffectiveValueSatisfyRequirement(definition, value);
            }

            if (definition.DefaultValueFactory is not null)
            {
                value = definition.DefaultValueFactory(result);
                return DoesEffectiveValueSatisfyRequirement(definition, value);
            }

            if (definition.Requirement)
            {
                value = null;
                return false;
            }

            value = GetClrDefault(definition.Type);
            return DoesEffectiveValueSatisfyRequirement(definition, value);
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
                EnsureEffectiveValueSatisfiesRequirement(definition, explicitValue);
                return explicitValue;
            }

            if (definition.DefaultValueFactory is not null)
            {
                var defaultValue = definition.DefaultValueFactory(result);
                EnsureEffectiveValueSatisfiesRequirement(definition, defaultValue);
                return defaultValue;
            }

            if (definition.Requirement)
            {
                throw new InvalidOperationException(
                    $"Required definition '{definition.Information.Name.Value}' does not have an explicit value or default factory.");
            }

            var clrDefault = GetClrDefault(definition.Type);
            EnsureEffectiveValueSatisfiesRequirement(definition, clrDefault);
            return clrDefault;
        }
    }

    private static bool DoesEffectiveValueSatisfyRequirement(TypedDefinition definition, object? value)
    {
        return definition.Requirement || !definition.RequirementIfNull || value is not null;
    }

    private static void EnsureEffectiveValueSatisfiesRequirement(TypedDefinition definition, object? value)
    {
        if (!DoesEffectiveValueSatisfyRequirement(definition, value))
        {
            throw new InvalidOperationException(
                $"Required definition '{definition.Information.Name.Value}' does not have an explicit value or default factory.");
        }
    }

    private static object? GetClrDefault(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] Type type)
    {
        return TypeDefaultValues.GetValue(type);
    }
}
