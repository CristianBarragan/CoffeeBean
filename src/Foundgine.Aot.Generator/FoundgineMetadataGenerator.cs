using System.Text;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp;

namespace Foundgine.Aot.Generator;

[Generator(LanguageNames.CSharp)]
public sealed class FoundgineMetadataGenerator : IIncrementalGenerator
{
    private const string EntityAttribute = "Foundgine.Aot.FoundgineEntityAttribute";
    private const string ModelAttribute = "Foundgine.Aot.FoundgineModelAttribute";
    private const string FieldAttribute = "Foundgine.Aot.FoundgineFieldAttribute";
    private const string RelationshipAttribute = "Foundgine.Aot.FoundgineRelationshipAttribute";
    private const string ConnectionAttribute = "Foundgine.Aot.FoundgineConnectionAttribute";
    private const string ConnectionMapAttribute = "Foundgine.Aot.FoundgineConnectionMapAttribute";
    private const string ModelEntityMapAttribute = "Foundgine.Aot.FoundgineModelEntityMapAttribute";
    private const string ConversionAttribute = "Foundgine.Aot.FoundgineConversionAttribute";
    private const string AuthorizationAttribute = "Foundgine.Aot.FoundgineAuthorizationAttribute";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var entities = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                EntityAttribute,
                static (node, _) => node is ClassDeclarationSyntax,
                static (ctx, _) => (INamedTypeSymbol)ctx.TargetSymbol)
            .Collect();

        var models = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                ModelAttribute,
                static (node, _) => node is ClassDeclarationSyntax,
                static (ctx, _) => (INamedTypeSymbol)ctx.TargetSymbol)
            .Collect();

        var conversions = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                ConversionAttribute,
                static (node, _) => node is MethodDeclarationSyntax,
                static (ctx, _) => (IMethodSymbol)ctx.TargetSymbol)
            .Collect();

        var authorizations = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                AuthorizationAttribute,
                static (node, _) => node is PropertyDeclarationSyntax,
                static (ctx, _) => (IPropertySymbol)ctx.TargetSymbol)
            .Collect();

        var connectionMaps = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                ConnectionMapAttribute,
                static (node, _) => node is ClassDeclarationSyntax,
                static (ctx, _) => (INamedTypeSymbol)ctx.TargetSymbol)
            .Collect();

        var modelEntityMaps = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                ModelEntityMapAttribute,
                static (node, _) => node is ClassDeclarationSyntax,
                static (ctx, _) => (INamedTypeSymbol)ctx.TargetSymbol)
            .Collect();

        var input = entities.Combine(models).Combine(conversions).Combine(authorizations).Combine(connectionMaps).Combine(modelEntityMaps);
        context.RegisterSourceOutput(input, static (spc, pair) =>
        {
            var entities = pair.Left.Left.Left.Left.Left;
            var models = pair.Left.Left.Left.Left.Right;
            var conversions = pair.Left.Left.Left.Right;
            var authorizations = pair.Left.Left.Right;
            var connectionMaps = pair.Left.Right;
            var modelEntityMaps = pair.Right;

            // Do not emit a GeneratedMetadata type for projects that do not
            // contain any Foundgine AOT declarations. This keeps the runtime
            // AOT assembly free of an accidental empty generated type if the
            // analyzer is ever included transitively.
            if (entities.IsDefaultOrEmpty &&
                models.IsDefaultOrEmpty &&
                conversions.IsDefaultOrEmpty &&
                authorizations.IsDefaultOrEmpty &&
                connectionMaps.IsDefaultOrEmpty &&
                modelEntityMaps.IsDefaultOrEmpty)
            {
                return;
            }

            spc.AddSource("Foundgine.GeneratedMetadata.g.cs", Emit(
                entities,
                models,
                conversions,
                authorizations,
                connectionMaps,
                modelEntityMaps));
        });
    }

    private static string Emit(
        ImmutableArray<INamedTypeSymbol> symbols,
        ImmutableArray<INamedTypeSymbol> models,
        ImmutableArray<IMethodSymbol> conversions,
        ImmutableArray<IPropertySymbol> authorizations,
        ImmutableArray<INamedTypeSymbol> connectionMaps,
        ImmutableArray<INamedTypeSymbol> modelEntityMaps)
    {
        var ordered = symbols
            .OrderBy(x => x.ToDisplayString(), StringComparer.Ordinal)
            .ToArray();

        var entityIds = AllocateEntityIds(ordered);
        var modelIds = AllocateModelIds(models);
        var connectionIds = AllocateConnectionIds(models);
        var modelEntityMap = BuildModelEntityMap(modelEntityMaps);
        var connectionMap = BuildConnectionMap(connectionMaps);
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine("using System.Linq;");
        sb.AppendLine("#nullable enable");
        sb.AppendLine("using Foundgine.Metadata;");
        sb.AppendLine("using Foundgine.Abstractions;");
        sb.AppendLine("using Foundgine.Aot;");
        sb.AppendLine("namespace Foundgine.Generated;");
        sb.AppendLine();
        sb.AppendLine("public static class GeneratedMetadata");
        sb.AppendLine("{");
        sb.AppendLine("    public static readonly MetadataRegistry Registry = Build();");
        sb.AppendLine();
        sb.AppendLine("    private static MetadataRegistry Build()");
        sb.AppendLine("    {");
        sb.AppendLine("        var registry = new MetadataRegistry();");

        foreach (var model in models.OrderBy(x => x.ToDisplayString(), StringComparer.Ordinal))
        {
            var modelId = modelIds[model.ToDisplayString()];
            var modelName = GetNamedString(GetAttribute(model, ModelAttribute), "Name") ?? model.Name;
            if (modelEntityMap.TryGetValue(model.ToDisplayString(), out var modelEntity) &&
                entityIds.TryGetValue(modelEntity.ToDisplayString(), out var mappedEntityId))
            {
                sb.AppendLine($"        registry.Register(new ModelMetadata(new ModelId({modelId}), \"{Escape(modelName)}\", new EntityId({mappedEntityId}))); ");
            }
            else
            {
                sb.AppendLine($"        registry.Register(new ModelMetadata(new ModelId({modelId}), \"{Escape(modelName)}\"));");
            }
        }

        foreach (var entity in ordered)
        {
            var entityId = entityIds[entity.ToDisplayString()];
            var entityName = GetEntityName(entity);
            var storageName = GetEntityStorageName(entity) ?? entityName;
            var properties = entity.GetMembers().OfType<IPropertySymbol>()
                .Where(p => p.DeclaredAccessibility == Accessibility.Public && !p.IsStatic)
                .ToArray();

            var scalar = properties.Where(p => p.GetAttributes().All(a => a.AttributeClass?.ToDisplayString() != RelationshipAttribute)).ToArray();
            var fieldIds = AllocateIds(scalar.Select(p => entity.ToDisplayString() + "." + p.Name).ToArray());
            var columnIds = fieldIds;

            sb.AppendLine("        registry.Register(new EntityMetadata(");
            sb.AppendLine($"            new EntityId({entityId}),");
            sb.AppendLine($"            \"{Escape(entityName)}\",");
            sb.AppendLine("            new ColumnMetadata[]");
            sb.AppendLine("            {");
            foreach (var p in scalar)
            {
                var field = GetAttribute(p, FieldAttribute);
                var colName = GetNamedString(field, "StorageName") ?? GetNamedString(field, "Name") ?? p.Name;
                var id = GetNamedUShort(field, "Id") ?? columnIds[entity.ToDisplayString() + "." + p.Name];
                sb.AppendLine($"                new ColumnMetadata(new ColumnId({id}), \"{Escape(colName)}\"),");
            }
            sb.AppendLine("            },");
            sb.AppendLine($"            StorageName: \"{Escape(storageName)}\",");
            var primaryKey = scalar.FirstOrDefault(p => GetNamedBool(GetAttribute(p, FieldAttribute), "IsPrimaryKey"));
            if (primaryKey is not null)
            {
                var pkField = GetAttribute(primaryKey, FieldAttribute);
                var pkId = GetNamedUShort(pkField, "Id") ?? columnIds[entity.ToDisplayString() + "." + primaryKey.Name];
                sb.AppendLine($"            PrimaryKey: new ColumnReference(new EntityId({entityId}), new ColumnId({pkId})),");
            }
            sb.AppendLine("            Fields: new FieldMetadata[]");
            sb.AppendLine("            {");
            foreach (var p in scalar)
            {
                var field = GetAttribute(p, FieldAttribute);
                var fieldName = GetNamedString(field, "Name") ?? p.Name;
                var fieldId = GetNamedUShort(field, "Id") ?? fieldIds[entity.ToDisplayString() + "." + p.Name];
                var colName = GetNamedString(field, "StorageName") ?? GetNamedString(field, "Name") ?? p.Name;
                var colId = GetNamedUShort(field, "Id") ?? columnIds[entity.ToDisplayString() + "." + p.Name];
                sb.AppendLine($"                new FieldMetadata(new FieldId({fieldId}), \"{Escape(fieldName)}\", typeof({p.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}), new ColumnReference(new EntityId({entityId}), new ColumnId({colId}))),");
            }
            sb.AppendLine("            }));");
        }

        foreach (var entity in ordered)
        {
            var sourceId = entityIds[entity.ToDisplayString()];
            foreach (var p in entity.GetMembers().OfType<IPropertySymbol>())
            {
                var rel = GetAttribute(p, RelationshipAttribute);
                if (rel is null) continue;
                var target = GetTypeArgument(rel, 0);
                if (target is null) continue;
                var targetId = entityIds[target.ToDisplayString()];
                var relKey = entity.ToDisplayString() + "." + p.Name;
                var id = GetNamedUShort(rel, "Id") ?? AllocateIds(new[] { relKey })[relKey];
                var name = GetNamedString(rel, "Name") ?? p.Name;
                var fk = GetCtorString(rel, 1) ?? "Id";
                var pk = GetCtorString(rel, 2) ?? "Id";
                var sourceOwnsForeignKey = entity.GetMembers(fk).OfType<IPropertySymbol>().Any();
                var fkOwner = sourceOwnsForeignKey ? entity : target;
                var principalOwner = sourceOwnsForeignKey ? target : entity;
                var fkId = ResolveColumnId(fkOwner, fk, entityIds, fieldIds: null);
                var principalId = ResolveColumnId(principalOwner, pk, entityIds, fieldIds: null);

                // SourceKey/TargetKey always describe the key on each side of the
                // semantic relationship, regardless of which side physically owns
                // the foreign key.
                var sourceKeyEntity = sourceId;
                var sourceKeyColumn = sourceOwnsForeignKey ? fkId : principalId;
                var targetKeyEntity = targetId;
                var targetKeyColumn = sourceOwnsForeignKey ? principalId : fkId;
                sb.AppendLine($"        registry.Register(new RelationshipMetadata(new RelationshipId({id}), new EntityId({sourceId}), new EntityId({targetId}), \"{Escape(name)}\", new ColumnReference(new EntityId({sourceKeyEntity}), new ColumnId({sourceKeyColumn})), new ColumnReference(new EntityId({targetKeyEntity}), new ColumnId({targetKeyColumn})), {IsCollectionExpression(p.Type)}));");
            }
        }

        foreach (var conversion in conversions.OrderBy(x => x.ToDisplayString(), StringComparer.Ordinal))
        {
            var attribute = GetAttribute(conversion, ConversionAttribute);
            var sourceType = GetTypeArgument(attribute, 0);
            var targetType = GetTypeArgument(attribute, 1);
            if (sourceType is null || targetType is null)
                continue;

            var method = conversion.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            sb.AppendLine($"        registry.Register(new ConversionMetadata(typeof({sourceType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}), typeof({targetType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}), \"{Escape(method)}\"));");
        }

        foreach (var authorization in authorizations.OrderBy(x => x.ToDisplayString(), StringComparer.Ordinal))
        {
            var attribute = GetAttribute(authorization, AuthorizationAttribute);
            var connectionId = GetConstructorUShort(attribute, 0);
            if (connectionId is null)
                continue;

            var expression = GetAuthorizationExpression(authorization);
            if (expression is null)
                continue;

            var delegateType = GetExpressionDelegateType(authorization.Type);
            if (delegateType is null || delegateType.TypeArguments.Length != 3)
                continue;

            var contextType = delegateType.TypeArguments[0];
            var resourceType = delegateType.TypeArguments[1];
            var returnType = delegateType.TypeArguments[2];
            if (returnType.SpecialType != SpecialType.System_Boolean)
                continue;

            var id = GetNamedUShort(attribute, "Id") ?? connectionId.Value;
            var name = GetNamedString(attribute, "Name") ?? authorization.Name;
            var predicate = BuildAuthorizationPredicate(authorization);
            if (predicate is null)
                continue;
            sb.AppendLine($"        registry.Register(new AuthorizationMetadata(new AuthorizationId({id}), new ConnectionId({connectionId.Value}), \"{Escape(name)}\", \"{Escape(authorization.Name)}\", typeof({contextType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}), typeof({resourceType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}), \"{Escape(expression)}\", {predicate}));");
        }

        foreach (var model in models.OrderBy(x => x.ToDisplayString(), StringComparer.Ordinal))
        {
            var modelId = modelIds[model.ToDisplayString()];
            foreach (var property in model.GetMembers().OfType<IPropertySymbol>())
            {
                var connection = GetAttribute(property, ConnectionAttribute);
                if (connection is null) continue;
                var key = model.ToDisplayString() + "." + property.Name;
                var target = GetTypeArgument(connection, 0);
                if (target is null)
                    connectionMap.TryGetValue(key, out target);
                if (target is null || !entityIds.TryGetValue(target.ToDisplayString(), out var targetId))
                    continue;


                var connectionId = GetNamedUShort(connection, "Id") ?? connectionIds[key];
                var name = GetNamedString(connection, "Name") ?? property.Name;
                var fields = BuildConnectionFields(property, model, target, conversions);
                var fieldText = fields.Count == 0
                    ? "null"
                    : "new ConnectionFieldMetadata[] { " + string.Join(", ", fields.Select(f => $"new ConnectionFieldMetadata(\"{Escape(f.SourceMember)}\", \"{Escape(f.TargetMember)}\", typeof({f.SourceType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}), typeof({f.TargetType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}), {ToNullableString(f.Converter)} )")) + " }";
                sb.AppendLine($"        registry.Register(new ConnectionMetadata(new ConnectionId({connectionId}), new ModelId({modelId}), new EntityId({targetId}), \"{Escape(name)}\", \"{Escape(property.Name)}\", {fieldText}));");
            }
        }

        sb.AppendLine("        return registry;");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        sb.AppendLine();

        EmitSemanticModel(sb, models, modelEntityMap, entityIds);

        sb.AppendLine("public sealed class GeneratedMetadataProvider : IMetadataProvider, IMetadataSource");
        sb.AppendLine("{");
        sb.AppendLine("    public IReadOnlyCollection<EntityMetadata> Entities => GeneratedMetadata.Registry.Entities.ToArray();");
        sb.AppendLine("    public IReadOnlyCollection<RelationshipMetadata> Relationships => GeneratedMetadata.Registry.Relationships.ToArray();");
        sb.AppendLine("    public IReadOnlyCollection<ModelMetadata> Models => GeneratedMetadata.Registry.Models.ToArray();");
        sb.AppendLine("    public IReadOnlyCollection<ConnectionMetadata> Connections => GeneratedMetadata.Registry.Connections.ToArray();");
        sb.AppendLine("    public IReadOnlyCollection<ConversionMetadata> Conversions => GeneratedMetadata.Registry.Conversions.ToArray();");
        sb.AppendLine("    public IReadOnlyCollection<AuthorizationMetadata> Authorizations => GeneratedMetadata.Registry.Authorizations.ToArray();");
        sb.AppendLine("    public EntityMetadata GetEntity(EntityId entityId) => GeneratedMetadata.Registry.GetEntity(entityId);");
        sb.AppendLine("    public RelationshipMetadata GetRelationship(RelationshipId relationshipId) => GeneratedMetadata.Registry.GetRelationship(relationshipId);");
        sb.AppendLine("    public ModelMetadata GetModel(ModelId modelId) => GeneratedMetadata.Registry.GetModel(modelId);");
        sb.AppendLine("    public ConnectionMetadata GetConnection(ConnectionId connectionId) => GeneratedMetadata.Registry.GetConnection(connectionId);");
        sb.AppendLine("    public ConversionMetadata? FindConversion(Type sourceType, Type targetType) => GeneratedMetadata.Registry.FindConversion(sourceType, targetType);");
        sb.AppendLine("    public AuthorizationMetadata GetAuthorization(AuthorizationId authorizationId) => GeneratedMetadata.Registry.GetAuthorization(authorizationId);");
        sb.AppendLine("}");
        return sb.ToString();
    }


    private static void EmitSemanticModel(
        StringBuilder sb,
        ImmutableArray<INamedTypeSymbol> models,
        Dictionary<string, INamedTypeSymbol> modelEntityMap,
        Dictionary<string, ushort> entityIds)
    {
        sb.AppendLine("public static class GeneratedSemanticModel");
        sb.AppendLine("{");

        foreach (var model in models.OrderBy(x => x.ToDisplayString(), StringComparer.Ordinal))
        {
            if (!modelEntityMap.TryGetValue(model.ToDisplayString(), out var entity) ||
                !entityIds.TryGetValue(entity.ToDisplayString(), out var entityId))
                continue;

            var modelName =
                GetNamedString(GetAttribute(model, ModelAttribute), "Name")
                ?? model.Name;

            var modelProperties = model.GetMembers()
                .OfType<IPropertySymbol>()
                .Where(p => p.DeclaredAccessibility == Accessibility.Public && !p.IsStatic)
                .ToArray();

            var entityProperties = entity.GetMembers()
                .OfType<IPropertySymbol>()
                .Where(p => p.DeclaredAccessibility == Accessibility.Public && !p.IsStatic)
                .ToDictionary(p => p.Name, StringComparer.Ordinal);

            sb.AppendLine($"    public static class {model.Name}");
            sb.AppendLine("    {");

            // Do not call this member "Name": Name may itself be a semantic field.
            sb.AppendLine(
                $"        public const string ModelName = \"{Escape(modelName)}\";");

            sb.AppendLine(
                $"        public static readonly EntityId Entity = new({entityId});");

            var fieldMembers = new List<(string Identifier, ushort FieldId, string SemanticName)>();
            var usedIdentifiers = new HashSet<string>(StringComparer.Ordinal)
            {
                model.Name,
                "ModelName",
                "Entity",
                "All"
            };

            foreach (var property in modelProperties)
            {
                if (!entityProperties.TryGetValue(property.Name, out var entityProperty))
                    continue;

                var field = GetAttribute(entityProperty, FieldAttribute);
                var fieldId = GetNamedUShort(field, "Id");

                if (fieldId is null)
                    continue;

                var identifier = GetGeneratedSemanticFieldIdentifier(
                    property.Name,
                    usedIdentifiers);

                usedIdentifiers.Add(identifier);

                fieldMembers.Add((
                    identifier,
                    fieldId.Value,
                    property.Name));

                sb.AppendLine(
                    $"        public static readonly GeneratedSemanticField {identifier} = " +
                    $"new(Entity, new FieldId({fieldId.Value}), \"{Escape(property.Name)}\");");
            }

            sb.AppendLine("        public static IReadOnlyList<FieldId> All { get; } = new FieldId[]");
            sb.AppendLine("        {");

            foreach (var field in fieldMembers)
                sb.AppendLine($"            {field.Identifier}.Id,");

            sb.AppendLine("        };");
            sb.AppendLine("    }");
            sb.AppendLine();
        }

        sb.AppendLine("}");
        sb.AppendLine();
    }

    private static string GetGeneratedSemanticFieldIdentifier(
        string propertyName,
        HashSet<string> usedIdentifiers)
    {
        var candidate = propertyName;

        if (!usedIdentifiers.Contains(candidate))
            return candidate;

        candidate = propertyName + "Field";

        if (!usedIdentifiers.Contains(candidate))
            return candidate;

        var suffix = 2;

        while (usedIdentifiers.Contains(candidate + suffix))
            suffix++;

        return candidate + suffix;
    }

    private static Dictionary<string, INamedTypeSymbol> BuildModelEntityMap(ImmutableArray<INamedTypeSymbol> declarations)
    {
        var result = new Dictionary<string, INamedTypeSymbol>(StringComparer.Ordinal);
        foreach (var declaration in declarations.OrderBy(x => x.ToDisplayString(), StringComparer.Ordinal))
        {
            foreach (var attribute in declaration.GetAttributes().Where(a => a.AttributeClass?.ToDisplayString() == ModelEntityMapAttribute))
            {
                var model = GetTypeArgument(attribute, 0);
                var entity = GetTypeArgument(attribute, 1);
                if (model is not null && entity is not null)
                    result[model.ToDisplayString()] = entity;
            }
        }
        return result;
    }

    private static Dictionary<string, INamedTypeSymbol> BuildConnectionMap(ImmutableArray<INamedTypeSymbol> declarations)
    {
        var result = new Dictionary<string, INamedTypeSymbol>(StringComparer.Ordinal);
        foreach (var declaration in declarations.OrderBy(x => x.ToDisplayString(), StringComparer.Ordinal))
        {
            foreach (var attribute in declaration.GetAttributes().Where(a => a.AttributeClass?.ToDisplayString() == ConnectionMapAttribute))
            {
                var model = GetTypeArgument(attribute, 0);
                var member = GetCtorString(attribute, 1);
                var entity = GetTypeArgument(attribute, 2);
                if (model is not null && entity is not null && !string.IsNullOrWhiteSpace(member))
                    result[model.ToDisplayString() + "." + member] = entity;
            }
        }
        return result;
    }

    private sealed class ConnectionField
    {
        public ConnectionField(string sourceMember, string targetMember, ITypeSymbol sourceType, ITypeSymbol targetType, string? converter)
        {
            SourceMember = sourceMember;
            TargetMember = targetMember;
            SourceType = sourceType;
            TargetType = targetType;
            Converter = converter;
        }

        public string SourceMember { get; }
        public string TargetMember { get; }
        public ITypeSymbol SourceType { get; }
        public ITypeSymbol TargetType { get; }
        public string? Converter { get; }
    }

    private static List<ConnectionField> BuildConnectionFields(
        IPropertySymbol connectionProperty,
        INamedTypeSymbol model,
        INamedTypeSymbol target,
        ImmutableArray<IMethodSymbol> conversions)
    {
        var expressionFields = BuildExpressionConnectionFields(connectionProperty, model, target, conversions);
        if (expressionFields is not null)
            return expressionFields;

        return BuildConventionConnectionFields(model, target, conversions);
    }

    private static List<ConnectionField>? BuildExpressionConnectionFields(
        IPropertySymbol connectionProperty,
        INamedTypeSymbol model,
        INamedTypeSymbol target,
        ImmutableArray<IMethodSymbol> conversions)
    {
        var syntax = connectionProperty.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax() as PropertyDeclarationSyntax;
        if (syntax?.ExpressionBody?.Expression is not LambdaExpressionSyntax lambda)
            return null;

        if (lambda.Body is not AnonymousObjectCreationExpressionSyntax anonymous)
            return null;

        var sourceParameter = lambda switch
        {
            SimpleLambdaExpressionSyntax simple => simple.Parameter.Identifier.Text,
            ParenthesizedLambdaExpressionSyntax parenthesized => parenthesized.ParameterList.Parameters.FirstOrDefault()?.Identifier.Text,
            _ => null
        };
        if (string.IsNullOrWhiteSpace(sourceParameter))
            return null;

        var result = new List<ConnectionField>();
        foreach (var initializer in anonymous.Initializers)
        {
            var targetMember = initializer.NameEquals?.Name.Identifier.Text;
            var expression = initializer.Expression;

            if (string.IsNullOrWhiteSpace(targetMember))
            {
                targetMember = expression switch
                {
                    MemberAccessExpressionSyntax member => member.Name.Identifier.Text,
                    IdentifierNameSyntax identifier => identifier.Identifier.Text,
                    _ => null
                };
            }

            if (string.IsNullOrWhiteSpace(targetMember))
                continue;

            var targetMemberName = targetMember!;
            var targetProperty = target.GetMembers(targetMemberName).OfType<IPropertySymbol>().FirstOrDefault();
            if (targetProperty is null)
                continue;

            var sourceMember = sourceParameter is null ? null : GetDirectSourceMember(expression, sourceParameter);
            if (sourceMember is null)
                continue;

            var sourceProperty = model.GetMembers(sourceMember).OfType<IPropertySymbol>().FirstOrDefault();
            if (sourceProperty is null)
                continue;

            IMethodSymbol? converter = null;
            if (!SymbolEqualityComparer.Default.Equals(sourceProperty.Type, targetProperty.Type))
            {
                converter = FindConversionForExpression(expression, sourceProperty.Type, targetProperty.Type, conversions);
                if (converter is null)
                    continue;
            }

            result.Add(new ConnectionField(
                sourceMember,
                targetMemberName,
                sourceProperty.Type,
                targetProperty.Type,
                converter?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)));
        }

        return result;
    }

    private static string? GetDirectSourceMember(ExpressionSyntax expression, string sourceParameter)
    {
        if (expression is InvocationExpressionSyntax invocation && invocation.ArgumentList.Arguments.Count == 1)
            return GetDirectSourceMember(invocation.ArgumentList.Arguments[0].Expression, sourceParameter);

        if (expression is IdentifierNameSyntax identifier)
            return identifier.Identifier.Text;

        if (expression is not MemberAccessExpressionSyntax member)
            return null;

        if (member.Expression is IdentifierNameSyntax receiver && receiver.Identifier.Text == sourceParameter)
            return member.Name.Identifier.Text;

        return null;
    }

    private static IMethodSymbol? FindConversionForExpression(
        ExpressionSyntax expression,
        ITypeSymbol sourceType,
        ITypeSymbol targetType,
        ImmutableArray<IMethodSymbol> conversions)
    {
        var methodName = expression is InvocationExpressionSyntax invocation
            ? GetInvokedMethodName(invocation.Expression)
            : null;

        return conversions.FirstOrDefault(method =>
        {
            if (methodName is not null && method.Name != methodName)
                return false;

            var attribute = GetAttribute(method, ConversionAttribute);
            var from = GetTypeArgument(attribute, 0);
            var to = GetTypeArgument(attribute, 1);
            return from is not null && to is not null
                && SymbolEqualityComparer.Default.Equals(from, sourceType)
                && SymbolEqualityComparer.Default.Equals(to, targetType);
        });
    }

    private static string? GetInvokedMethodName(ExpressionSyntax expression) => expression switch
    {
        IdentifierNameSyntax identifier => identifier.Identifier.Text,
        MemberAccessExpressionSyntax member => member.Name.Identifier.Text,
        _ => null
    };

    private static List<ConnectionField> BuildConventionConnectionFields(INamedTypeSymbol model, INamedTypeSymbol target, ImmutableArray<IMethodSymbol> conversions)
    {
        var sourceProperties = model.GetMembers().OfType<IPropertySymbol>()
            .Where(p => p.DeclaredAccessibility == Accessibility.Public && !p.IsStatic)
            .ToArray();
        var targetProperties = target.GetMembers().OfType<IPropertySymbol>()
            .Where(p => p.DeclaredAccessibility == Accessibility.Public && !p.IsStatic)
            .Where(p => GetAttribute(p, RelationshipAttribute) is null)
            .ToArray();

        var result = new List<ConnectionField>();
        foreach (var destination in targetProperties)
        {
            var source = sourceProperties.FirstOrDefault(p => p.Name == destination.Name);
            if (source is not null && SymbolEqualityComparer.Default.Equals(source.Type, destination.Type))
            {
                result.Add(new ConnectionField(source.Name, destination.Name, source.Type, destination.Type, null));
                continue;
            }

            var candidates = sourceProperties
                .Select(sourceProperty => new
                {
                    Property = sourceProperty,
                    Conversion = conversions.FirstOrDefault(method =>
                    {
                        var attribute = GetAttribute(method, ConversionAttribute);
                        var from = GetTypeArgument(attribute, 0);
                        var to = GetTypeArgument(attribute, 1);
                        return from is not null && to is not null
                            && SymbolEqualityComparer.Default.Equals(from, sourceProperty.Type)
                            && SymbolEqualityComparer.Default.Equals(to, destination.Type);
                    })
                })
                .Where(x => x.Conversion is not null)
                .ToArray();

            if (candidates.Length == 1)
            {
                var candidate = candidates[0];
                result.Add(new ConnectionField(
                    candidate.Property.Name,
                    destination.Name,
                    candidate.Property.Type,
                    destination.Type,
                    candidate.Conversion!.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)));
            }
        }

        return result;
    }

    private static INamedTypeSymbol? GetExpressionDelegateType(ITypeSymbol type)
    {
        if (type is not INamedTypeSymbol named || named.Name != "Expression" || named.TypeArguments.Length != 1)
            return null;

        return named.TypeArguments[0] as INamedTypeSymbol;
    }

    private static string? BuildAuthorizationPredicate(IPropertySymbol property)
    {
        var syntax = property.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax() as PropertyDeclarationSyntax;
        if (syntax?.ExpressionBody?.Expression is not LambdaExpressionSyntax lambda)
            return null;

        var parameters = lambda switch
        {
            SimpleLambdaExpressionSyntax simple => new[] { simple.Parameter.Identifier.Text },
            ParenthesizedLambdaExpressionSyntax parenthesized => parenthesized.ParameterList.Parameters.Select(p => p.Identifier.Text).ToArray(),
            _ => Array.Empty<string>()
        };

        if (parameters.Length == 0)
            return null;

        var body = lambda switch
        {
            SimpleLambdaExpressionSyntax simple => simple.Body,
            ParenthesizedLambdaExpressionSyntax parenthesized => parenthesized.Body,
            _ => null
        };

        return body is ExpressionSyntax expression
            ? BuildPredicateNode(expression, parameters)
            : null;
    }

    private static string? BuildPredicateNode(ExpressionSyntax expression, IReadOnlyList<string> parameters)
    {
        switch (expression)
        {
            case ParenthesizedExpressionSyntax parenthesized:
                return BuildPredicateNode(parenthesized.Expression, parameters);

            case IdentifierNameSyntax identifier when parameters.Count > 0 && identifier.Identifier.Text == parameters[0]:
                return $"Foundgine.Abstractions.AuthorizationPredicate.ContextParameter(\"{Escape(identifier.Identifier.Text)}\")";

            case IdentifierNameSyntax identifier when parameters.Count > 1 && identifier.Identifier.Text == parameters[1]:
                return $"Foundgine.Abstractions.AuthorizationPredicate.ResourceParameter(\"{Escape(identifier.Identifier.Text)}\")";

            case MemberAccessExpressionSyntax member:
            {
                var target = BuildPredicateNode(member.Expression, parameters);
                return target is null
                    ? null
                    : $"Foundgine.Abstractions.AuthorizationPredicate.Member({target}, \"{Escape(member.Name.Identifier.Text)}\")";
            }

            case LiteralExpressionSyntax literal:
                return $"Foundgine.Abstractions.AuthorizationPredicate.Constant(\"{Escape(literal.ToString())}\")";

            case BinaryExpressionSyntax binary when binary.IsKind(SyntaxKind.EqualsExpression):
                return BuildBinaryPredicate(binary, "Equal", parameters);

            case BinaryExpressionSyntax binary when binary.IsKind(SyntaxKind.NotEqualsExpression):
                return BuildBinaryPredicate(binary, "NotEqual", parameters);

            case BinaryExpressionSyntax binary when binary.IsKind(SyntaxKind.LogicalAndExpression):
                return BuildBinaryPredicate(binary, "And", parameters);

            case BinaryExpressionSyntax binary when binary.IsKind(SyntaxKind.LogicalOrExpression):
                return BuildBinaryPredicate(binary, "Or", parameters);

            case PrefixUnaryExpressionSyntax unary when unary.IsKind(SyntaxKind.LogicalNotExpression):
            {
                var operand = BuildPredicateNode(unary.Operand, parameters);
                return operand is null ? null : $"Foundgine.Abstractions.AuthorizationPredicate.Not({operand})";
            }

            default:
                return null;
        }
    }

    private static string? BuildBinaryPredicate(
        BinaryExpressionSyntax binary,
        string operation,
        IReadOnlyList<string> parameters)
    {
        var left = BuildPredicateNode(binary.Left, parameters);
        var right = BuildPredicateNode(binary.Right, parameters);
        return left is null || right is null
            ? null
            : $"Foundgine.Abstractions.AuthorizationPredicate.{operation}({left}, {right})";
    }

    private static string? GetAuthorizationExpression(IPropertySymbol property)
    {
        var syntax = property.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax() as PropertyDeclarationSyntax;
        return syntax?.ExpressionBody?.Expression is LambdaExpressionSyntax lambda
            ? lambda.ToString()
            : null;
    }

    private static ushort? GetConstructorUShort(AttributeData? attribute, int index)
    {
        if (attribute is null || index < 0 || index >= attribute.ConstructorArguments.Length)
            return null;

        var value = attribute.ConstructorArguments[index].Value;
        return value switch
        {
            ushort ushortValue => ushortValue,
            short shortValue when shortValue >= 0 => (ushort)shortValue,
            int intValue when intValue >= 0 && intValue <= ushort.MaxValue => (ushort)intValue,
            _ => null
        };
    }

    private static string ToNullableString(string? value) =>
        value is null ? "null" : $"\"{Escape(value)}\"";

    private static Dictionary<string, ushort> AllocateModelIds(IReadOnlyList<INamedTypeSymbol> models)
    {
        var result = new Dictionary<string, ushort>(StringComparer.Ordinal);
        var used = new HashSet<ushort>();
        foreach (var model in models.OrderBy(x => x.ToDisplayString(), StringComparer.Ordinal))
        {
            var explicitId = GetNamedUShort(GetAttribute(model, ModelAttribute), "Id");
            if (explicitId.HasValue)
            {
                if (!used.Add(explicitId.Value))
                    throw new InvalidOperationException($"Duplicate Foundgine model ID {explicitId.Value}.");
                result[model.ToDisplayString()] = explicitId.Value;
            }
        }
        foreach (var model in models.OrderBy(x => x.ToDisplayString(), StringComparer.Ordinal))
        {
            if (result.ContainsKey(model.ToDisplayString())) continue;
            var candidate = StableHash("model:" + model.ToDisplayString());
            while (candidate == 0 || !used.Add(candidate))
                candidate = candidate == ushort.MaxValue ? (ushort)1 : (ushort)(candidate + 1);
            result[model.ToDisplayString()] = candidate;
        }
        return result;
    }

    private static Dictionary<string, ushort> AllocateConnectionIds(IReadOnlyList<INamedTypeSymbol> models)
    {
        var keys = models.SelectMany(model => model.GetMembers().OfType<IPropertySymbol>()
            .Where(p => GetAttribute(p, ConnectionAttribute) is not null)
            .Select(p => model.ToDisplayString() + "." + p.Name)).ToArray();
        return AllocateIds(keys.Select(x => "connection:" + x).ToArray())
            .ToDictionary(x => x.Key.Substring("connection:".Length), x => x.Value, StringComparer.Ordinal);
    }

    private static Dictionary<string, ushort> AllocateEntityIds(IReadOnlyList<INamedTypeSymbol> entities)
    {
        var result = new Dictionary<string, ushort>(StringComparer.Ordinal);
        var used = new HashSet<ushort>();
        foreach (var entity in entities.OrderBy(x => x.ToDisplayString(), StringComparer.Ordinal))
        {
            var explicitId = GetNamedUShort(GetAttribute(entity, EntityAttribute), "Id");
            if (explicitId.HasValue)
            {
                if (!used.Add(explicitId.Value))
                    throw new InvalidOperationException($"Duplicate Foundgine entity ID {explicitId.Value}.");
                result[entity.ToDisplayString()] = explicitId.Value;
            }
        }
        foreach (var entity in entities.OrderBy(x => x.ToDisplayString(), StringComparer.Ordinal))
        {
            if (result.ContainsKey(entity.ToDisplayString())) continue;
            var candidate = StableHash(entity.ToDisplayString());
            while (candidate == 0 || !used.Add(candidate))
                candidate = candidate == ushort.MaxValue ? (ushort)1 : (ushort)(candidate + 1);
            result[entity.ToDisplayString()] = candidate;
        }
        return result;
    }

    private static Dictionary<string, ushort> AllocateIds(IReadOnlyList<string> keys)
    {
        var used = new HashSet<ushort>();
        var result = new Dictionary<string, ushort>(StringComparer.Ordinal);
        foreach (var key in keys.OrderBy(x => x, StringComparer.Ordinal))
        {
            var candidate = StableHash(key);
            while (candidate == 0 || !used.Add(candidate))
                candidate = candidate == ushort.MaxValue ? (ushort)1 : (ushort)(candidate + 1);
            result[key] = candidate;
        }
        return result;
    }

    private static ushort StableHash(string value)
    {
        uint hash = 2166136261;
        foreach (var c in value)
        {
            hash ^= c;
            hash *= 16777619;
        }
        return (ushort)(hash ^ (hash >> 16));
    }

    private static AttributeData? GetAttribute(ISymbol symbol, string fullName) =>
        symbol.GetAttributes().FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == fullName);

    private static string GetEntityName(INamedTypeSymbol type) =>
        GetNamedString(GetAttribute(type, EntityAttribute), "Name") ?? type.Name;

    private static string? GetEntityStorageName(INamedTypeSymbol type) =>
        GetNamedString(GetAttribute(type, EntityAttribute), "StorageName");

    private static string? GetNamedString(AttributeData? attribute, string name) =>
        attribute?.NamedArguments.FirstOrDefault(x => x.Key == name).Value.Value as string;

    private static ushort? GetNamedUShort(AttributeData? attribute, string name)
    {
        var value = attribute?.NamedArguments.FirstOrDefault(x => x.Key == name).Value.Value;
        return value is ushort u && u != 0 ? u : null;
    }

    private static bool GetNamedBool(AttributeData? attribute, string name) =>
        attribute?.NamedArguments.FirstOrDefault(x => x.Key == name).Value.Value is true;


    private static string? GetCtorString(AttributeData attribute, int index) =>
        index < attribute.ConstructorArguments.Length ? attribute.ConstructorArguments[index].Value as string : null;

    private static INamedTypeSymbol? GetTypeArgument(AttributeData? attribute, int index) =>
        attribute is not null && index < attribute.ConstructorArguments.Length
            ? attribute.ConstructorArguments[index].Value as INamedTypeSymbol
            : null;

    private static ushort ResolveColumnId(INamedTypeSymbol entity, string propertyName, Dictionary<string, ushort> _, Dictionary<string, ushort>? fieldIds)
    {
        var property = entity.GetMembers(propertyName).OfType<IPropertySymbol>().FirstOrDefault();
        if (property is null) return 0;
        var field = GetAttribute(property, FieldAttribute);
        var explicitId = GetNamedUShort(field, "Id");
        if (explicitId.HasValue) return explicitId.Value;
        return AllocateIds(new[] { entity.ToDisplayString() + "." + property.Name })[entity.ToDisplayString() + "." + property.Name];
    }

    private static string Escape(string value) => value.Replace("\\", "\\\\").Replace("\"", "\\\"");
    private static string IsCollectionExpression(ITypeSymbol type)
    {
        if (type is IArrayTypeSymbol)
            return "true";

        if (type.AllInterfaces.Any(i =>
            i.OriginalDefinition.SpecialType == SpecialType.System_Collections_Generic_IEnumerable_T))
            return "true";

        return "false";
    }

}
