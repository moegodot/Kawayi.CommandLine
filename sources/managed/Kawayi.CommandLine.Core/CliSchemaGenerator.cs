// Copyright (c) 2026 GodotAsync<me@kawayi.moe>.
// Licensed under the GNU Affero General Public License v3-or-later license.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Kawayi.CommandLine.Abstractions;
using Kawayi.CommandLine.Core.Attributes;

namespace Kawayi.CommandLine.Core;

/// <summary>
/// Generates immutable CLI schemas at runtime by reflecting over command types.
/// </summary>
public static class CliSchemaGenerator
{
    private const DynamicallyAccessedMemberTypes RequiredSubcommandDynamicallyAccessedMembers =
        DynamicallyAccessedMemberTypes.Interfaces |
        DynamicallyAccessedMemberTypes.PublicProperties |
        DynamicallyAccessedMemberTypes.NonPublicProperties |
        DynamicallyAccessedMemberTypes.PublicMethods |
        DynamicallyAccessedMemberTypes.NonPublicMethods |
        DynamicallyAccessedMemberTypes.PublicConstructors;

    /// <summary>
    /// Generates a runtime schema for the specified command type.
    /// </summary>
    /// <typeparam name="T">The command type to inspect.</typeparam>
    /// <returns>The generated immutable schema.</returns>
    public static CliSchema GenerateFor<
        [DynamicallyAccessedMembers(
            DynamicallyAccessedMemberTypes.Interfaces |
            DynamicallyAccessedMemberTypes.PublicProperties |
            DynamicallyAccessedMemberTypes.NonPublicProperties |
            DynamicallyAccessedMemberTypes.PublicMethods |
            DynamicallyAccessedMemberTypes.NonPublicMethods |
            DynamicallyAccessedMemberTypes.PublicConstructors)]
        T>()
    {
        return GenerateFor(typeof(T));
    }

    /// <summary>
    /// Generates a runtime schema for the specified command type.
    /// </summary>
    /// <param name="type">The command type to inspect.</param>
    /// <returns>The generated immutable schema.</returns>
    public static CliSchema GenerateFor(
        [DynamicallyAccessedMembers(
            DynamicallyAccessedMemberTypes.Interfaces |
            DynamicallyAccessedMemberTypes.PublicProperties |
            DynamicallyAccessedMemberTypes.NonPublicProperties |
            DynamicallyAccessedMemberTypes.PublicMethods |
            DynamicallyAccessedMemberTypes.NonPublicMethods |
            DynamicallyAccessedMemberTypes.PublicConstructors)]
        Type type)
    {
        ArgumentNullException.ThrowIfNull(type);

        return GenerateCore(type, new HashSet<Type>());
    }

    [UnconditionalSuppressMessage("ReflectionAnalysis", "IL2067", Justification = "Runtime schema generation intentionally flows reflected command metadata through helper methods.")]
    private static CliSchema GenerateCore(
        [DynamicallyAccessedMembers(
            DynamicallyAccessedMemberTypes.PublicProperties |
            DynamicallyAccessedMemberTypes.NonPublicProperties |
            DynamicallyAccessedMemberTypes.PublicMethods |
            DynamicallyAccessedMemberTypes.NonPublicMethods)]
        Type type,
        HashSet<Type> visiting)
    {
        if (!visiting.Add(type))
        {
            throw new InvalidOperationException($"Command schema generation detected a cycle at '{type.FullName}'.");
        }

        try
        {
            if (type.IsDefined(typeof(CommandAttribute), inherit: true))
            {
                return GenerateFromReflection(type, visiting);
            }

            if (TryGetStaticSymbols(type, out var symbols))
            {
                return GenerateFromStaticSymbols(type, symbols, visiting);
            }

            throw new InvalidOperationException(
                $"Type '{type.FullName}' must be annotated with {nameof(CommandAttribute)} or expose static {nameof(ISymbolExporter.Symbols)} metadata.");
        }
        finally
        {
            visiting.Remove(type);
        }
    }

    [UnconditionalSuppressMessage("ReflectionAnalysis", "IL2072", Justification = "Runtime schema generation recursively walks reflected command types intentionally.")]
    private static CliSchema GenerateFromReflection(
        [DynamicallyAccessedMembers(
            DynamicallyAccessedMemberTypes.PublicProperties |
            DynamicallyAccessedMemberTypes.NonPublicProperties |
            DynamicallyAccessedMemberTypes.PublicMethods |
            DynamicallyAccessedMemberTypes.NonPublicMethods)]
        Type type,
        HashSet<Type> visiting)
    {
        var documents = GetStaticDocuments(type);
        var declaredSchema = BuildDeclaredSchema(type, documents, visiting);
        var baseType = type.BaseType;

        if (baseType is null || baseType == typeof(object) || !CanGenerateSchemaFor(baseType))
        {
            return declaredSchema;
        }

        return MergeInheritedSchema(baseType, declaredSchema, GenerateCore(baseType, visiting));
    }

    [UnconditionalSuppressMessage("ReflectionAnalysis", "IL2072", Justification = "Runtime schema generation recursively walks reflected subcommand types intentionally.")]
    private static CliSchema GenerateFromStaticSymbols(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.NonPublicProperties)]
        Type type,
        ImmutableArray<Symbol> symbols,
        HashSet<Type> visiting)
    {
        var subcommandMetadata = GetDeclaredSubcommandMetadata(type);
        var arguments = ImmutableList.CreateBuilder<ParameterDefinition>();
        var properties = ImmutableDictionary.CreateBuilder<OptionToken, PropertyDefinition>();
        var subcommandDefinitions = ImmutableDictionary.CreateBuilder<ArgumentOrCommandToken, CommandDefinition>();
        var subcommands = ImmutableDictionary.CreateBuilder<ArgumentOrCommandToken, CliSchema>();
        var promotedChildren = new List<(string MemberName, Type ChildType, CliSchema Schema)>();

        foreach (var symbol in symbols)
        {
            switch (symbol)
            {
                case ParameterDefinition argument:
                    arguments.Add(argument);
                    break;
                case PropertyDefinition property:
                    AddPropertyDefinition(properties, property);
                    break;
                case CommandDefinition definition:
                    var name = definition.Information.Name.Value;
                    if (!subcommandMetadata.TryGetValue(name, out var metadata))
                    {
                        throw new InvalidOperationException(
                            $"Subcommand symbol '{name}' on '{type.FullName}' does not have matching reflected subcommand metadata.");
                    }

                    ValidateSubcommandReflectionContract(metadata.Property);

                    if (metadata.IsGlobal)
                    {
                        promotedChildren.Add((metadata.Property.Name, metadata.Property.PropertyType, GenerateCore(metadata.Property.PropertyType, visiting)));
                        break;
                    }

                    AddSubcommandDefinition(subcommandDefinitions, definition);
                    subcommands[new ArgumentOrCommandToken(name)] = GenerateCore(metadata.Property.PropertyType, visiting);
                    break;
            }
        }

        var schema = new CliSchema(
            type,
            subcommandDefinitions.ToImmutable(),
            subcommands.ToImmutable(),
            properties.ToImmutable(),
            arguments.ToImmutable());

        foreach (var promotedChild in promotedChildren)
        {
            schema = PromoteGlobalSubcommand(schema, promotedChild.Schema, promotedChild.ChildType, promotedChild.MemberName);
        }

        var baseType = type.BaseType;
        if (baseType is null || baseType == typeof(object) || !CanGenerateSchemaFor(baseType))
        {
            return schema;
        }

        return MergeInheritedSchema(baseType, schema, GenerateCore(baseType, visiting));
    }

    [UnconditionalSuppressMessage("ReflectionAnalysis", "IL2072", Justification = "Runtime schema generation consumes reflected command property types intentionally.")]
    private static CliSchema BuildDeclaredSchema(
        [DynamicallyAccessedMembers(
            DynamicallyAccessedMemberTypes.PublicProperties |
            DynamicallyAccessedMemberTypes.NonPublicProperties |
            DynamicallyAccessedMemberTypes.PublicMethods |
            DynamicallyAccessedMemberTypes.NonPublicMethods)]
        Type type,
        ImmutableDictionary<string, Document> documents,
        HashSet<Type> visiting)
    {
        var properties = GetDeclaredInstanceProperties(type);
        var argumentsByPosition = new SortedDictionary<int, ParameterDefinition>();
        var optionDefinitions = ImmutableDictionary.CreateBuilder<OptionToken, PropertyDefinition>();
        var subcommandDefinitions = ImmutableDictionary.CreateBuilder<ArgumentOrCommandToken, CommandDefinition>();
        var subcommands = ImmutableDictionary.CreateBuilder<ArgumentOrCommandToken, CliSchema>();
        var promotedChildren = new List<(string MemberName, Type ChildType, CliSchema Schema)>();

        foreach (var property in properties)
        {
            var argumentAttribute = GetAttributeData(property, typeof(ArgumentAttribute));
            var propertyAttribute = GetAttributeData(property, typeof(PropertyAttribute));
            var subcommandAttribute = GetAttributeData(property, typeof(SubcommandAttribute));
            var roleCount = CountDefined(argumentAttribute, propertyAttribute, subcommandAttribute);
            var longAliases = GetAttributeDataList(property, typeof(LongAliasAttribute));
            var shortAliases = GetAttributeDataList(property, typeof(ShortAliasAttribute));
            var aliases = GetAttributeDataList(property, typeof(AliasAttribute));
            var validators = GetAttributeDataList(property, typeof(ValidatorAttribute));

            if (roleCount == 0)
            {
                ThrowIfUnexpectedAliases(property, longAliases, shortAliases, aliases);
                ThrowIfUnexpectedValidators(property, validators);
                continue;
            }

            if (roleCount > 1)
            {
                throw new InvalidOperationException($"Member '{property.Name}' declares more than one CLI role attribute.");
            }

            if (argumentAttribute is not null)
            {
                ThrowIfNotEmpty(longAliases, property, "LongAliasAttribute");
                ThrowIfNotEmpty(shortAliases, property, "ShortAliasAttribute");
                ThrowIfNotEmpty(aliases, property, "AliasAttribute");

                var valueRangeAttribute = GetAttributeData(property, typeof(ValueRangeAttribute))
                                          ?? throw new InvalidOperationException(
                                              $"Argument member '{property.Name}' must declare {nameof(ValueRangeAttribute)}.");
                var requirementIfNull = GetBoolValue(argumentAttribute, 3, false);
                ValidateRequirementIfNull(property, requirementIfNull);

                var position = GetIntValue(argumentAttribute, 0, 0);
                var parameterDefinition = new ParameterDefinition(
                    CreateDefinitionInformation(property, documents, GetCommandLineName(property), GetBoolValue(argumentAttribute, 2, true)),
                    null,
                    new ValueRange(GetIntValue(valueRangeAttribute, 0, 0), GetIntValue(valueRangeAttribute, 1, 0)),
                    property.PropertyType,
                    GetBoolValue(argumentAttribute, 1, false),
                    requirementIfNull)
                {
                    Format = GetStringValue(argumentAttribute, 4),
                    Validation = CreateValidationDelegate(property, validators)
                };

                if (!argumentsByPosition.TryAdd(position, parameterDefinition))
                {
                    throw new InvalidOperationException($"Argument position '{position}' is declared more than once on '{type.FullName}'.");
                }

                continue;
            }

            if (propertyAttribute is not null)
            {
                ThrowIfNotEmpty(aliases, property, "AliasAttribute");

                var requirementIfNull = GetBoolValue(propertyAttribute, 3, false);
                ValidateRequirementIfNull(property, requirementIfNull);

                var propertyDefinition = new PropertyDefinition(
                    CreateDefinitionInformation(property, documents, GetCommandLineName(property), GetBoolValue(propertyAttribute, 1, true)),
                    CreateAliasDictionary(longAliases),
                    CreateAliasDictionary(shortAliases),
                    null,
                    property.PropertyType,
                    GetBoolValue(propertyAttribute, 0, false),
                    requirementIfNull)
                {
                    ValueName = GetStringValue(propertyAttribute, 2),
                    Format = GetStringValue(propertyAttribute, 4),
                    NumArgs = GetNumArgs(property),
                    PossibleValues = property.PropertyType.IsEnum
                        ? new CountablePossibleValues<string>(ImmutableArray.CreateRange(Enum.GetNames(property.PropertyType)))
                        : null,
                    Validation = CreateValidationDelegate(property, validators)
                };

                AddPropertyDefinition(optionDefinitions, propertyDefinition);
                continue;
            }

            ThrowIfUnexpectedValidators(property, validators);
            ThrowIfNotEmpty(longAliases, property, "LongAliasAttribute");
            ThrowIfNotEmpty(shortAliases, property, "ShortAliasAttribute");
            ValidateSubcommandReflectionContract(property);

            var isGlobalSubcommand = GetBoolValue(subcommandAttribute!, 2, false);
            if (GetBoolValue(subcommandAttribute!, 0, false))
            {
                throw new InvalidOperationException($"Subcommand member '{property.Name}' cannot be required.");
            }

            ValidateSubcommandNullability(property, isGlobalSubcommand);

            var definition = new CommandDefinition(
                CreateDefinitionInformation(property, documents, GetCommandLineName(property), GetBoolValue(subcommandAttribute!, 1, true)),
                CreateAliasDictionary(aliases),
                null);
            var childSchema = GenerateCore(property.PropertyType, visiting);

            if (isGlobalSubcommand)
            {
                promotedChildren.Add((property.Name, property.PropertyType, childSchema));
                continue;
            }

            AddSubcommandDefinition(subcommandDefinitions, definition);
            subcommands[new ArgumentOrCommandToken(definition.Information.Name.Value)] = childSchema;
        }

        var schema = new CliSchema(
            type,
            subcommandDefinitions.ToImmutable(),
            subcommands.ToImmutable(),
            optionDefinitions.ToImmutable(),
            argumentsByPosition.Values.ToImmutableList());

        foreach (var promotedChild in promotedChildren)
        {
            schema = PromoteGlobalSubcommand(schema, promotedChild.Schema, promotedChild.ChildType, promotedChild.MemberName);
        }

        return schema;
    }

    private static DefinitionInformation CreateDefinitionInformation(
        PropertyInfo property,
        ImmutableDictionary<string, Document> documents,
        string commandLineName,
        bool visible)
    {
        return new DefinitionInformation(
            new NameWithVisibility(commandLineName, visible),
            documents.TryGetValue(property.Name, out var document) ? document : new Document(string.Empty, string.Empty));
    }

    private static ImmutableArray<PropertyInfo> GetDeclaredInstanceProperties(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.NonPublicProperties)]
        Type type)
    {
        return [.. type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
            .Where(static property => !property.IsIndexer())
            .OrderBy(static property => property.MetadataToken)];
    }

    private static ImmutableDictionary<string, SubcommandPropertyMetadata> GetDeclaredSubcommandMetadata(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.NonPublicProperties)]
        Type type)
    {
        var builder = ImmutableDictionary.CreateBuilder<string, SubcommandPropertyMetadata>(StringComparer.Ordinal);

        foreach (var property in GetDeclaredInstanceProperties(type))
        {
            var attribute = GetAttributeData(property, typeof(SubcommandAttribute));
            if (attribute is null)
            {
                continue;
            }

            builder[GetCommandLineName(property)] = new SubcommandPropertyMetadata(property, GetBoolValue(attribute, 2, false));
        }

        return builder.ToImmutable();
    }

    private static bool CanGenerateSchemaFor(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)]
        Type type)
    {
        return type.IsDefined(typeof(CommandAttribute), inherit: true) || TryGetStaticSymbols(type, out _);
    }

    private static bool TryGetStaticSymbols(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)]
        Type type,
        out ImmutableArray<Symbol> symbols)
    {
        if (TryGetStaticPropertyValue(type, nameof(ISymbolExporter.Symbols), out ImmutableArray<Symbol>? value) && value is not null)
        {
            symbols = value.Value;
            return true;
        }

        symbols = default;
        return false;
    }

    private static ImmutableDictionary<string, Document> GetStaticDocuments(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)]
        Type type)
    {
        return TryGetStaticPropertyValue(type, nameof(IDocumentExporter.Documents), out ImmutableDictionary<string, Document>? documents) && documents is not null
            ? documents
            : ImmutableDictionary<string, Document>.Empty;
    }

    private static bool TryGetStaticPropertyValue<T>(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)]
        Type type,
        string name,
        out T? value)
    {
        var property = type.GetProperty(name, BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);
        if (property is not null && typeof(T).IsAssignableFrom(property.PropertyType))
        {
            value = (T?)property.GetValue(null);
            return true;
        }

        value = default;
        return false;
    }

    private static ValueRange GetNumArgs(PropertyInfo property)
    {
        var valueRangeAttribute = GetAttributeData(property, typeof(ValueRangeAttribute));
        if (valueRangeAttribute is null)
        {
            return InferDefaultNumArgs(property.PropertyType);
        }

        return new ValueRange(GetIntValue(valueRangeAttribute, 0, 0), GetIntValue(valueRangeAttribute, 1, 0));
    }

    private static ValueRange InferDefaultNumArgs(Type type)
    {
        var effectiveType = Nullable.GetUnderlyingType(type) ?? type;

        if (effectiveType == typeof(bool))
        {
            return ValueRange.ZeroOrOne;
        }

        if (effectiveType.IsConstructedGenericType)
        {
            var genericDefinition = effectiveType.GetGenericTypeDefinition();
            if (genericDefinition == typeof(ImmutableDictionary<,>)
                || genericDefinition == typeof(ImmutableSortedDictionary<,>)
                || genericDefinition == typeof(ImmutableArray<>)
                || genericDefinition == typeof(ImmutableList<>)
                || genericDefinition == typeof(ImmutableQueue<>)
                || genericDefinition == typeof(ImmutableStack<>)
                || genericDefinition == typeof(ImmutableSortedSet<>)
                || genericDefinition == typeof(ImmutableHashSet<>))
            {
                return ValueRange.ZeroOrMore;
            }
        }

        return ValueRange.One;
    }

    [UnconditionalSuppressMessage("ReflectionAnalysis", "IL2075", Justification = "Validator discovery intentionally reflects over declared methods on the command type.")]
    private static Func<object, string?>? CreateValidationDelegate(PropertyInfo property, ImmutableArray<CustomAttributeData> validatorAttributes)
    {
        if (validatorAttributes.IsDefaultOrEmpty)
        {
            return null;
        }

        var methods = ImmutableArray.CreateBuilder<MethodInfo>(validatorAttributes.Length);
        foreach (var attribute in validatorAttributes)
        {
            var validatorName = GetStringValue(attribute, 0)
                                ?? throw new InvalidOperationException($"Validator attribute on '{property.Name}' does not specify a method name.");
            var matches = property.DeclaringType!
                .GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                .Where(method => IsMatchingValidator(method, validatorName, property.PropertyType))
                .ToArray();

            if (matches.Length != 1)
            {
                throw new InvalidOperationException(
                    $"Validator '{validatorName}' for member '{property.Name}' must resolve to exactly one static method that accepts '{property.PropertyType}'.");
            }

            methods.Add(matches[0]);
        }

        var validators = methods.ToImmutable();
        return value =>
        {
            foreach (var method in validators)
            {
                var result = method.Invoke(null, [value]);
                if (result is string message)
                {
                    return message;
                }
            }

            return null;
        };
    }

    private static bool IsMatchingValidator(MethodInfo method, string name, Type valueType)
    {
        if (!StringComparer.Ordinal.Equals(method.Name, name)
            || !method.IsStatic
            || method.IsGenericMethod
            || method.ReturnType != typeof(string))
        {
            return false;
        }

        var parameters = method.GetParameters();
        return parameters.Length == 1 && parameters[0].ParameterType == valueType;
    }

    private static void ValidateRequirementIfNull(PropertyInfo property, bool requirementIfNull)
    {
        if (!requirementIfNull || IsNullableType(property))
        {
            return;
        }

        throw new InvalidOperationException(
            $"Member '{property.Name}' cannot set requirement-if-null because its type is not nullable.");
    }

    private static void ValidateSubcommandNullability(PropertyInfo property, bool isGlobalSubcommand)
    {
        if (isGlobalSubcommand || IsNullableType(property))
        {
            return;
        }

        throw new InvalidOperationException(
            $"Regular subcommand member '{property.Name}' must be nullable or global.");
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

    private static bool IsNullableType(PropertyInfo property)
    {
        var propertyType = property.PropertyType;
        if (Nullable.GetUnderlyingType(propertyType) is not null)
        {
            return true;
        }

        if (propertyType.IsValueType)
        {
            return false;
        }

        var nullability = new NullabilityInfoContext().Create(property);
        return nullability.WriteState != NullabilityState.NotNull;
    }

    private static void AddPropertyDefinition(
        ImmutableDictionary<OptionToken, PropertyDefinition>.Builder builder,
        PropertyDefinition definition)
    {
        AddUniqueDefinition(builder, new LongOptionToken(definition.Information.Name.Value), definition);

        foreach (var alias in definition.LongName.Values)
        {
            AddUniqueDefinition(builder, new LongOptionToken(alias.Value), definition);
        }

        foreach (var alias in definition.ShortName.Values)
        {
            AddUniqueDefinition(builder, new ShortOptionToken(alias.Value), definition);
        }
    }

    private static void AddSubcommandDefinition(
        ImmutableDictionary<ArgumentOrCommandToken, CommandDefinition>.Builder builder,
        CommandDefinition definition)
    {
        AddUniqueDefinition(builder, new ArgumentOrCommandToken(definition.Information.Name.Value), definition);

        foreach (var alias in definition.Alias.Values)
        {
            AddUniqueDefinition(builder, new ArgumentOrCommandToken(alias.Value), definition);
        }
    }

    private static void AddUniqueDefinition<TKey, TValue>(
        ImmutableDictionary<TKey, TValue>.Builder builder,
        TKey key,
        TValue value)
        where TKey : notnull
        where TValue : class
    {
        if (builder.TryGetValue(key, out var existing))
        {
            if (!ReferenceEquals(existing, value))
            {
                throw new InvalidOperationException($"Token '{key}' is mapped to more than one definition.");
            }

            return;
        }

        builder[key] = value;
    }

    private static CliSchema PromoteGlobalSubcommand(CliSchema schema, CliSchema childSchema, Type childType, string memberName)
    {
        try
        {
            return MergeSchemas(schema, childSchema, allowOverride: false) with
            {
                GeneratedFrom = schema.GeneratedFrom
            };
        }
        catch (InvalidOperationException exception)
        {
            throw new InvalidOperationException(
                $"Global subcommand member '{memberName}' from '{childType.FullName}' cannot be promoted: {exception.Message}",
                exception);
        }
    }

    private static CliSchema MergeInheritedSchema(Type baseType, CliSchema ownSchema, CliSchema baseSchema)
    {
        try
        {
            return MergeSchemas(baseSchema, ownSchema, allowOverride: false) with
            {
                GeneratedFrom = ownSchema.GeneratedFrom ?? baseSchema.GeneratedFrom
            };
        }
        catch (InvalidOperationException exception)
        {
            throw new InvalidOperationException(
                $"Inherited command schema conflict while exporting '{ownSchema.GeneratedFrom?.FullName}' against '{baseType.FullName}': {exception.Message}",
                exception);
        }
    }

    private static CliSchema MergeSchemas(CliSchema current, CliSchema another, bool allowOverride)
    {
        if (!TryMergeArguments(current.Argument, another.Argument, allowOverride, out var arguments, out var reason)
            || !TryMergeDictionary(current.Properties, another.Properties, allowOverride, "property token", out var properties, out reason)
            || !TryMergeDictionary(current.SubcommandDefinitions, another.SubcommandDefinitions, allowOverride, "subcommand token", out var subcommandDefinitions, out reason)
            || !TryMergeDictionary(current.Subcommands, another.Subcommands, allowOverride, "subcommand schema", out var subcommands, out reason))
        {
            throw new InvalidOperationException(reason ?? "The CLI schemas could not be merged.");
        }

        return new CliSchema(
            another.GeneratedFrom ?? current.GeneratedFrom,
            subcommandDefinitions,
            subcommands,
            properties,
            arguments);
    }

    private static bool TryMergeArguments(
        ImmutableList<ParameterDefinition> current,
        ImmutableList<ParameterDefinition> another,
        bool allowOverride,
        out ImmutableList<ParameterDefinition> result,
        [NotNullWhen(false)] out string? reason)
    {
        var builder = current.ToBuilder();
        var indexes = new Dictionary<string, int>(StringComparer.Ordinal);

        for (var index = 0; index < builder.Count; index++)
        {
            indexes[builder[index].Information.Name.Value] = index;
        }

        foreach (var argument in another)
        {
            var name = argument.Information.Name.Value;
            if (indexes.TryGetValue(name, out var existingIndex))
            {
                if (!allowOverride)
                {
                    result = default!;
                    reason = $"Argument '{name}' is defined by more than one schema.";
                    return false;
                }

                builder[existingIndex] = argument;
                continue;
            }

            indexes[name] = builder.Count;
            builder.Add(argument);
        }

        result = builder.ToImmutable();
        reason = null;
        return true;
    }

    private static bool TryMergeDictionary<TKey, TValue>(
        ImmutableDictionary<TKey, TValue> current,
        ImmutableDictionary<TKey, TValue> another,
        bool allowOverride,
        string itemKind,
        out ImmutableDictionary<TKey, TValue> result,
        [NotNullWhen(false)] out string? reason)
        where TKey : notnull
    {
        var builder = current.ToBuilder();

        foreach (var (key, value) in another)
        {
            if (builder.ContainsKey(key) && !allowOverride)
            {
                result = default!;
                reason = $"The {itemKind} '{key}' is defined by more than one schema.";
                return false;
            }

            builder[key] = value;
        }

        result = builder.ToImmutable();
        reason = null;
        return true;
    }

    private static ImmutableDictionary<string, NameWithVisibility> CreateAliasDictionary(ImmutableArray<CustomAttributeData> attributes)
    {
        var builder = ImmutableDictionary.CreateBuilder<string, NameWithVisibility>(StringComparer.Ordinal);

        foreach (var attribute in attributes.OrderBy(static attribute => GetStringValue(attribute, 0) ?? string.Empty, StringComparer.Ordinal))
        {
            var alias = GetStringValue(attribute, 0) ?? string.Empty;
            builder[alias] = new NameWithVisibility(alias, GetBoolValue(attribute, 1, true));
        }

        return builder.ToImmutable();
    }

    private static CustomAttributeData? GetAttributeData(MemberInfo member, Type attributeType)
    {
        return member.CustomAttributes.FirstOrDefault(attribute => attribute.AttributeType == attributeType);
    }

    private static ImmutableArray<CustomAttributeData> GetAttributeDataList(MemberInfo member, Type attributeType)
    {
        return [.. member.CustomAttributes.Where(attribute => attribute.AttributeType == attributeType)];
    }

    private static int CountDefined(params CustomAttributeData?[] attributes)
    {
        var count = 0;
        foreach (var attribute in attributes)
        {
            if (attribute is not null)
            {
                count++;
            }
        }

        return count;
    }

    private static string GetCommandLineName(PropertyInfo property)
    {
        return CaseConverter.Pascal2Kebab(property.Name);
    }

    private static int GetIntValue(CustomAttributeData attribute, int index, int defaultValue)
    {
        return attribute.ConstructorArguments.Count > index && attribute.ConstructorArguments[index].Value is int value
            ? value
            : defaultValue;
    }

    private static bool GetBoolValue(CustomAttributeData attribute, int index, bool defaultValue)
    {
        return attribute.ConstructorArguments.Count > index && attribute.ConstructorArguments[index].Value is bool value
            ? value
            : defaultValue;
    }

    private static string? GetStringValue(CustomAttributeData attribute, int index)
    {
        return attribute.ConstructorArguments.Count > index
            ? attribute.ConstructorArguments[index].Value as string
            : null;
    }

    private static void ThrowIfUnexpectedAliases(
        PropertyInfo property,
        ImmutableArray<CustomAttributeData> longAliases,
        ImmutableArray<CustomAttributeData> shortAliases,
        ImmutableArray<CustomAttributeData> aliases)
    {
        ThrowIfNotEmpty(longAliases, property, "LongAliasAttribute");
        ThrowIfNotEmpty(shortAliases, property, "ShortAliasAttribute");
        ThrowIfNotEmpty(aliases, property, "AliasAttribute");
    }

    private static void ThrowIfUnexpectedValidators(PropertyInfo property, ImmutableArray<CustomAttributeData> validators)
    {
        ThrowIfNotEmpty(validators, property, "ValidatorAttribute");
    }

    private static void ThrowIfNotEmpty(ImmutableArray<CustomAttributeData> attributes, PropertyInfo property, string attributeName)
    {
        if (attributes.IsDefaultOrEmpty)
        {
            return;
        }

        throw new InvalidOperationException($"Member '{property.Name}' cannot use {attributeName} in its current CLI role.");
    }

    private sealed record SubcommandPropertyMetadata(PropertyInfo Property, bool IsGlobal);
}

file static class ReflectionPropertyInfoExtensions
{
    public static bool IsIndexer(this PropertyInfo property)
    {
        return property.GetIndexParameters().Length > 0;
    }
}
