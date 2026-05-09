// Copyright (c) 2026 GodotAsync<me@kawayi.moe>.
// Licensed under the GNU Affero General Public License v3-or-later license.

using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using Kawayi.CommandLine.Abstractions;
using Kawayi.CommandLine.Core.Attributes;

namespace Kawayi.CommandLine.Core;

/// <summary>
/// Binds parsed CLI values into existing command objects at runtime.
/// </summary>
public static class Binder
{
    private const DynamicallyAccessedMemberTypes RequiredSubcommandDynamicallyAccessedMembers =
        DynamicallyAccessedMemberTypes.Interfaces |
        DynamicallyAccessedMemberTypes.PublicProperties |
        DynamicallyAccessedMemberTypes.NonPublicProperties |
        DynamicallyAccessedMemberTypes.PublicMethods |
        DynamicallyAccessedMemberTypes.NonPublicMethods |
        DynamicallyAccessedMemberTypes.PublicConstructors;

    /// <summary>
    /// Binds the provided CLI result into the existing target object.
    /// </summary>
    /// <typeparam name="T">The target command type.</typeparam>
    /// <param name="value">The existing object to populate.</param>
    /// <param name="cli">The parsed CLI result.</param>
    /// <param name="bindingOptions">The binding behavior options.</param>
    /// <returns>The same target object after binding.</returns>
    public static T Bind<
        [DynamicallyAccessedMembers(
            DynamicallyAccessedMemberTypes.Interfaces |
            DynamicallyAccessedMemberTypes.PublicProperties |
            DynamicallyAccessedMemberTypes.NonPublicProperties |
            DynamicallyAccessedMemberTypes.PublicConstructors)]
        T>(
        T value,
        Cli cli,
        BindingOptions bindingOptions)
    {
        return (T)Bind(value!, typeof(T), cli, bindingOptions);
    }

    /// <summary>
    /// Binds the provided CLI result into the existing target object.
    /// </summary>
    /// <param name="value">The existing object to populate.</param>
    /// <param name="typeOfValue">The target binding type to validate against.</param>
    /// <param name="cli">The parsed CLI result.</param>
    /// <param name="bindingOptions">The binding behavior options.</param>
    /// <returns>The same target object after binding.</returns>
    public static object Bind(
        object value,
        [DynamicallyAccessedMembers(
            DynamicallyAccessedMemberTypes.Interfaces |
            DynamicallyAccessedMemberTypes.PublicProperties |
            DynamicallyAccessedMemberTypes.NonPublicProperties |
            DynamicallyAccessedMemberTypes.PublicConstructors)]
        Type typeOfValue,
        Cli cli,
        BindingOptions bindingOptions)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(typeOfValue);
        ArgumentNullException.ThrowIfNull(cli);
        ArgumentNullException.ThrowIfNull(bindingOptions);

        if (!typeOfValue.IsInstanceOfType(value))
        {
            throw new ArgumentException($"Value '{value.GetType().FullName}' is not assignable to '{typeOfValue.FullName}'.", nameof(value));
        }

        if (bindingOptions.CheckGeneratedType
            && cli.Schema.GeneratedFrom is not null
            && !cli.Schema.GeneratedFrom.Equals(typeOfValue))
        {
            throw new ArgumentException(
                $"the Cli.Schema.GeneratedFrom {cli.Schema.GeneratedFrom} do not equals to current binding type {typeOfValue}.");
        }

        BindCore(value, typeOfValue, cli, bindingOptions);
        return value;
    }

    [UnconditionalSuppressMessage("ReflectionAnalysis", "IL2070", Justification = "Runtime binding intentionally walks reflected properties.")]
    [UnconditionalSuppressMessage("ReflectionAnalysis", "IL2072", Justification = "Runtime binding intentionally instantiates reflected subcommand types.")]
    private static void BindCore(
        object target,
        [DynamicallyAccessedMembers(
            DynamicallyAccessedMemberTypes.PublicProperties |
            DynamicallyAccessedMemberTypes.NonPublicProperties |
            DynamicallyAccessedMemberTypes.PublicConstructors)]
        Type targetType,
        Cli cli,
        BindingOptions bindingOptions)
    {
        if (targetType.BaseType is not null && targetType.BaseType != typeof(object))
        {
            BindCore(target, targetType.BaseType, cli, bindingOptions);
        }

        var nestedBindingOptions = bindingOptions with { CheckGeneratedType = false };
        foreach (var property in targetType
                     .GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                     .Where(static property => property.GetIndexParameters().Length == 0)
                     .OrderBy(static property => property.MetadataToken))
        {
            var argumentAttribute = property.CustomAttributes.FirstOrDefault(attribute => attribute.AttributeType == typeof(ArgumentAttribute));
            var propertyAttribute = property.CustomAttributes.FirstOrDefault(attribute => attribute.AttributeType == typeof(PropertyAttribute));
            var subcommandAttribute = property.CustomAttributes.FirstOrDefault(attribute => attribute.AttributeType == typeof(SubcommandAttribute));
            var roleCount = (argumentAttribute is null ? 0 : 1)
                            + (propertyAttribute is null ? 0 : 1)
                            + (subcommandAttribute is null ? 0 : 1);

            if (roleCount == 0)
            {
                continue;
            }

            if (roleCount > 1)
            {
                throw new InvalidOperationException($"Member '{property.Name}' declares more than one CLI role attribute.");
            }

            var commandLineName = CaseConverter.Pascal2Kebab(property.Name);

            if (argumentAttribute is not null)
            {
                var definition = cli.Schema.Argument.FirstOrDefault(definition =>
                    StringComparer.Ordinal.Equals(definition.Information.Name.Value, commandLineName));
                if (definition is not null && cli.Arguments.TryGetValue(definition, out var value))
                {
                    AssignPropertyValue(target, property, value);
                }

                continue;
            }

            if (propertyAttribute is not null)
            {
                var definition = cli.Schema.Properties.Values.Distinct().FirstOrDefault(definition =>
                    StringComparer.Ordinal.Equals(definition.Information.Name.Value, commandLineName));
                if (definition is not null && cli.Properties.TryGetValue(definition, out var value))
                {
                    AssignPropertyValue(target, property, value);
                }

                continue;
            }

            ValidateSubcommandReflectionContract(property);

            var isGlobal = subcommandAttribute is not null && subcommandAttribute.ConstructorArguments.Count > 2 && subcommandAttribute.ConstructorArguments[2].Value is true;
            if (isGlobal)
            {
                var childTarget = property.GetValue(target) ?? CreateInstance(property.PropertyType);
                BindCore(childTarget, property.PropertyType, cli, nestedBindingOptions);

                if (!ReferenceEquals(childTarget, property.GetValue(target)))
                {
                    AssignPropertyValue(target, property, childTarget);
                }

                continue;
            }

            var definitionForSubcommand = cli.Schema.SubcommandDefinitions.Values.Distinct().FirstOrDefault(definition =>
                StringComparer.Ordinal.Equals(definition.Information.Name.Value, commandLineName));

            if (definitionForSubcommand is null || !cli.Subcommands.TryGetValue(definitionForSubcommand, out var childCli))
            {
                ClearPropertyValue(target, property);
                continue;
            }

            var childValue = property.GetValue(target) ?? CreateInstance(property.PropertyType);
            BindCore(childValue, property.PropertyType, childCli, nestedBindingOptions);

            if (!ReferenceEquals(childValue, property.GetValue(target)))
            {
                AssignPropertyValue(target, property, childValue);
            }
        }
    }

    private static void AssignPropertyValue(object target, PropertyInfo property, object? value)
    {
        var setter = property.SetMethod;
        if (setter is null)
        {
            setter = property.GetSetMethod(nonPublic: true);
        }

        if (setter is null)
        {
            throw new InvalidOperationException($"Property '{property.Name}' does not expose a setter for binding.");
        }

        setter.Invoke(target, [value]);
    }

    [UnconditionalSuppressMessage("ReflectionAnalysis", "IL2072", Justification = "Runtime binding intentionally clears reflected value-type members.")]
    private static void ClearPropertyValue(object target, PropertyInfo property)
    {
        if (!property.PropertyType.IsValueType || Nullable.GetUnderlyingType(property.PropertyType) is not null)
        {
            AssignPropertyValue(target, property, null);
            return;
        }

        AssignPropertyValue(target, property, RuntimeHelpers.GetUninitializedObject(property.PropertyType));
    }

    private static object CreateInstance(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
        Type type)
    {
        try
        {
            return Activator.CreateInstance(type)
                   ?? throw new InvalidOperationException($"Type '{type.FullName}' could not be instantiated for binding.");
        }
        catch (MissingMethodException exception)
        {
            throw new InvalidOperationException(
                $"Type '{type.FullName}' must declare a public parameterless constructor for reflection binding.",
                exception);
        }
    }

    private static void ValidateSubcommandReflectionContract(PropertyInfo property)
    {
        var contract = property.PropertyType.GetCustomAttribute<DynamicallyAccessedMembersAttribute>(inherit: false);
        if (contract is not null
            && (contract.MemberTypes & RequiredSubcommandDynamicallyAccessedMembers) == RequiredSubcommandDynamicallyAccessedMembers)
        {
            return;
        }

        throw new InvalidOperationException(
            $"Subcommand type '{property.PropertyType.FullName}' for member '{property.DeclaringType?.FullName}.{property.Name}' must be annotated with [{nameof(DynamicallyAccessedMembersAttribute)}({nameof(DynamicallyAccessedMemberTypes)}.{nameof(DynamicallyAccessedMemberTypes.Interfaces)} | {nameof(DynamicallyAccessedMemberTypes)}.{nameof(DynamicallyAccessedMemberTypes.PublicProperties)} | {nameof(DynamicallyAccessedMemberTypes)}.{nameof(DynamicallyAccessedMemberTypes.NonPublicProperties)} | {nameof(DynamicallyAccessedMemberTypes)}.{nameof(DynamicallyAccessedMemberTypes.PublicMethods)} | {nameof(DynamicallyAccessedMemberTypes)}.{nameof(DynamicallyAccessedMemberTypes.NonPublicMethods)} | {nameof(DynamicallyAccessedMemberTypes)}.{nameof(DynamicallyAccessedMemberTypes.PublicConstructors)})].");
    }
}
