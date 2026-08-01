using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using CoffeeBeanery.GraphQL.Core.Mapping.Generators.Model;
using CoffeeBeanery.GraphQL.Core.Mapping.Generators.Passes;

namespace CoffeeBeanery.GraphQL.Core.Mapping.Generators.Parsing;

internal static class MappingClassParser
{
    public static MappingClassInfo Parse(
        INamedTypeSymbol classSymbol,
        SemanticModel semanticModel,
        CancellationToken ct)
    {
        var info = new MappingClassInfo
        {
            ClassSymbol = classSymbol,
            IsModel = true
        };

        ct.ThrowIfCancellationRequested();

        var definitionProperty =
            classSymbol
                .GetMembers("Definition")
                .OfType<IPropertySymbol>()
                .FirstOrDefault();

        if (definitionProperty == null)
        {
            info.Diagnostics.Add(
                Diagnostic.Create(
                    MappingDiagnostics.InvalidMappingDefinition,
                    classSymbol.Locations[0],
                    classSymbol.Name));

            return info;
        }

        var syntax =
            definitionProperty
                .DeclaringSyntaxReferences
                .FirstOrDefault()
                ?.GetSyntax(ct);

        if (syntax is not PropertyDeclarationSyntax property)
            return info;

        var expression =
            GetPropertyExpression(property);

        if (expression == null)
            return info;

        var initializer =
            GetObjectInitializer(expression);

        if (initializer == null)
            return info;

        // ---------------------------------------------------------------
        // NEW: single-entity shorthand. Lets a non-composite mapping write
        //   Entity = typeof(DataEntity.Contract),
        //   Key    = nameof(DataEntity.Contract.ContractKey)
        // instead of a full Entities = [ new() { Entity = ..., ModelKey =
        // ..., EntityKey = ..., IsPrimary = true } ] block. Captured here,
        // expanded into a real EntityDefinitionInfo entry below — AFTER
        // the assignment loop, and ONLY if the author didn't also write an
        // explicit Entities = [...] block. If both are present, the
        // explicit Entities block wins (this is purely additive; nothing
        // about the existing Entities/PrimaryKey/UpsertKeys behavior
        // changes for mappings that don't use the shorthand).
        // ---------------------------------------------------------------
        INamedTypeSymbol? entityShorthand = null;
        string? keyShorthand = null;

        foreach (var assignment in initializer.Expressions
                     .OfType<AssignmentExpressionSyntax>())
        {
            ct.ThrowIfCancellationRequested();

            var name =
                (assignment.Left as IdentifierNameSyntax)
                ?.Identifier.Text;


            switch (name)
            {
                case "Model":

                    info.ModelType =
                        GetTypeSymbol(
                            assignment.Right,
                            semanticModel);

                    break;


                case "Navigations":

                    ParseNavigations(
                        assignment.Right,
                        info,
                        semanticModel);

                    break;


                case "ForeignKeys":

                    ParseForeignKeys(
                        assignment.Right,
                        info,
                        semanticModel);

                    break;


                case "Schema":

                    info.Schema =
                        EvaluateStringLikeExpression(
                            assignment.Right,
                            semanticModel);

                    break;


                case "Entities":

                    ParseEntities(
                        assignment.Right,
                        info,
                        semanticModel);

                    break;


                case "Entity":

                    // Top-level shorthand only — distinct from the nested
                    // "Entity" case inside ParseEntities/ParseForeignKeys/
                    // ParseFields, which operate on their own local `name`
                    // variable in their own method scope and are unaffected.
                    entityShorthand =
                        GetTypeSymbol(
                            assignment.Right,
                            semanticModel);

                    break;


                case "Key":

                    keyShorthand =
                        EvaluateStringLikeExpression(
                            assignment.Right,
                            semanticModel);

                    break;


                case "PrimaryKey":
                    ParsePrimaryKeys(
                        assignment.Right,
                        info,
                        semanticModel);
                    break;


                case "UpsertKeys":

                    ParseUpsertKeys(
                        assignment.Right,
                        info,
                        semanticModel);

                    break;


                case "Fields":

                    ParseFields(
                        assignment.Right,
                        info,
                        semanticModel);

                    break;


                case "Graph":

                    ParseGraph(
                        assignment.Right,
                        info,
                        semanticModel);

                    break;
            }
        }

        // ---------------------------------------------------------------
        // NEW: expand the Entity/Key shorthand into a real single-entity
        // Entities[] entry, but only if the author didn't already supply
        // an explicit Entities = [...] block (ParseEntities would have
        // populated info.Definition.Entities above if they had).
        // ---------------------------------------------------------------
        if (info.Definition.Entities.Count == 0 &&
            entityShorthand != null)
        {
            info.Definition.Entities.Add(
                new EntityDefinitionInfo
                {
                    EntityType = entityShorthand,
                    ModelKey = keyShorthand,
                    EntityKey = keyShorthand,
                    FromColumn = keyShorthand,
                    ToColumn = keyShorthand,
                    IsPrimary = true
                });
        }

        // ---------------------------------------------------------------
        // NEW: synthesize PrimaryKey/UpsertKeys from the primary entity's
        // key when the author left those collections empty. Explicit
        // PrimaryKey/UpsertKeys declarations (parsed above, if present)
        // always win — this only fills a gap, never overrides.
        //
        // ModelKey is a required member on PrimaryKeyDefinitionInfo, so it
        // must be set here even though the mapping author didn't type it
        // out — sourced from the same Entities[] entry's ModelKey/
        // FromColumn (the model-side property name), falling back to the
        // column name itself if ModelKey was never separately specified.
        // ---------------------------------------------------------------
        if (info.Definition.PrimaryKey.Count == 0)
        {
            var primaryEntry =
                info.Definition.Entities
                    .FirstOrDefault(e => e.IsPrimary && e.EntityType != null);

            if (primaryEntry != null &&
                !string.IsNullOrWhiteSpace(primaryEntry.ToColumn))
            {
                info.Definition.PrimaryKey.Add(
                    new PrimaryKeyDefinitionInfo
                    {
                        Entity = primaryEntry.EntityType!,
                        ModelKey =
                            primaryEntry.FromColumn
                            ?? primaryEntry.ModelKey
                            ?? primaryEntry.ToColumn,
                        ColumnKey = primaryEntry.ToColumn
                    });
            }
        }

        if (info.UpsertKeys.Count == 0)
        {
            foreach (var entity in info.Definition.Entities
                         .Where(e =>
                             e.EntityType != null &&
                             !string.IsNullOrWhiteSpace(e.ToColumn)))
            {
                info.UpsertKeys.Add(
                    new UpsertKeyInfo
                    {
                        Entity = entity.EntityType!.Name,
                        Key = entity.ToColumn!
                    });
            }
        }


        info.IsComposite =
            info.Definition.Entities
                .Where(e => e.EntityType != null)
                .Select(e => e.EntityType)
                .Distinct(SymbolEqualityComparer.Default)
                .Count() > 1;


        info.EntityType =
            info.Definition.Entities
                .FirstOrDefault(e => e.IsPrimary)
                ?.EntityType
            ??
            info.Definition.Entities
                .FirstOrDefault()
                ?.EntityType;


        NormalizeFieldDestinations(info);

        ResolvePropertyTypes(info);


        return info;
    }

    private static void ParsePrimaryKeys(
        ExpressionSyntax expression,
        MappingClassInfo info,
        SemanticModel semanticModel)
    {
        foreach (var element in GetCollectionElements(expression))
        {
            var initializer =
                GetObjectInitializer(element);

            if (initializer == null)
                continue;


            var primaryKey =
                new PrimaryKeyDefinitionInfo
                {
                    Entity = null!,
                    ModelKey = string.Empty,
                    ColumnKey = string.Empty
                };


            foreach (var assignment in initializer.Expressions
                         .OfType<AssignmentExpressionSyntax>())
            {
                var name =
                    (assignment.Left as IdentifierNameSyntax)
                    ?.Identifier.Text;


                switch (name)
                {
                    case "Entity":
                        primaryKey.Entity =
                            GetTypeSymbol(
                                assignment.Right,
                                semanticModel);
                        break;


                    case "ModelKey":
                        primaryKey.ModelKey =
                            EvaluateStringLikeExpression(
                                assignment.Right,
                                semanticModel)
                            ?? string.Empty;
                        break;


                    case "Column":
                    case "ColumnKey":
                        primaryKey.ColumnKey =
                            EvaluateStringLikeExpression(
                                assignment.Right,
                                semanticModel)
                            ?? string.Empty;
                        break;
                }
            }


            if (primaryKey.Entity != null &&
                !string.IsNullOrWhiteSpace(primaryKey.ColumnKey))
            {
                info.Definition.PrimaryKey.Add(
                    primaryKey);
            }
        }
    }

    private static void NormalizeFieldDestinations(
        MappingClassInfo info)
    {
        foreach (var field in info.FieldMaps)
        {
            if (string.IsNullOrWhiteSpace(field.DestinationName) ||
                string.IsNullOrWhiteSpace(field.DestinationEntity))
            {
                continue;
            }


            var entity =
                info.Definition.Entities
                    .FirstOrDefault(x =>
                        x.EntityType?.Name ==
                        field.DestinationEntity);


            if (entity?.EntityType == null)
                continue;


            var property =
                entity.EntityType
                    .GetMembers()
                    .OfType<IPropertySymbol>()
                    .FirstOrDefault(x =>
                        string.Equals(
                            x.Name,
                            field.DestinationName,
                            StringComparison.OrdinalIgnoreCase));


            if (property != null)
            {
                field.DestinationName =
                    property.Name;
            }
        }
    }

    private static void ResolvePropertyTypes(
        MappingClassInfo info)
    {
        if (info.ModelType == null)
            return;


        var properties =
            info.ModelType
                .GetMembers()
                .OfType<IPropertySymbol>()
                .ToDictionary(
                    p => p.Name,
                    StringComparer.OrdinalIgnoreCase);


        foreach (var field in info.FieldMaps)
        {
            if (field.PropertyType != null)
                continue;


            if (string.IsNullOrWhiteSpace(
                    field.SourceName))
            {
                continue;
            }


            if (properties.TryGetValue(
                    field.SourceName,
                    out var property))
            {
                field.PropertyType =
                    property.Type;
            }
        }
    }
    
        private static void ParseNavigations(
        ExpressionSyntax expression,
        MappingClassInfo info,
        SemanticModel semanticModel)
    {
        foreach (var element in GetCollectionElements(expression))
        {
            var initializer =
                GetObjectInitializer(element);

            if (initializer == null)
                continue;


            var navigation =
                new NavigationDefinitionInfo
                {
                    NavigationName = string.Empty
                };


            foreach (var assignment in initializer.Expressions
                         .OfType<AssignmentExpressionSyntax>())
            {
                var name =
                    (assignment.Left as IdentifierNameSyntax)
                    ?.Identifier.Text;


                switch (name)
                {
                    case "NavigationName":

                        navigation =
                            navigation with
                            {
                                NavigationName =
                                    EvaluateStringLikeExpression(
                                        assignment.Right,
                                        semanticModel)
                                    ?? string.Empty
                            };

                        break;


                    case "TargetModel":

                        navigation =
                            navigation with
                            {
                                TargetModel =
                                    GetTypeSymbol(
                                        assignment.Right,
                                        semanticModel)
                            };

                        break;


                    case "IsCollection":

                        navigation =
                            navigation with
                            {
                                IsCollection =
                                    assignment.Right
                                        .ToString()
                                        .Equals(
                                            "true",
                                            StringComparison.OrdinalIgnoreCase)
                            };

                        break;


                    case "Paths":

                        navigation =
                            navigation with
                            {
                                Paths =
                                    ParseJoinPaths(
                                        assignment.Right,
                                        semanticModel)
                            };

                        break;
                }
            }


            if (!string.IsNullOrWhiteSpace(
                    navigation.NavigationName))
            {
                info.Definition.Navigations.Add(
                    navigation);
            }
        }
    }


    private static void ParseEnumMapping(
        ExpressionSyntax expression,
        FieldInfo field,
        SemanticModel semanticModel)
    {
        var typeInfo =
            semanticModel.GetTypeInfo(expression);


        if (typeInfo.Type is not INamedTypeSymbol genericType)
            return;


        if (!genericType.IsGenericType ||
            genericType.TypeArguments.Length != 2)
        {
            return;
        }


        field.ModelEnumType =
            genericType.TypeArguments[0]
                as INamedTypeSymbol;


        field.EntityEnumType =
            genericType.TypeArguments[1]
                as INamedTypeSymbol;


        if (field.ModelEnumType == null ||
            field.EntityEnumType == null)
        {
            return;
        }


        var initializer =
            expression switch
            {
                ObjectCreationExpressionSyntax obj =>
                    obj.Initializer,

                ImplicitObjectCreationExpressionSyntax obj =>
                    obj.Initializer,

                _ => null
            };


        if (initializer == null)
            return;


        foreach (var assignment in initializer.Expressions
                     .OfType<AssignmentExpressionSyntax>())
        {
            var name =
                (assignment.Left as IdentifierNameSyntax)
                ?.Identifier.Text;


            switch (name)
            {
                case "Overrides":

                    ParseEnumOverrides(
                        assignment.Right,
                        field,
                        semanticModel);

                    break;


                case "Ignore":

                    ParseEnumIgnore(
                        assignment.Right,
                        field,
                        semanticModel);

                    break;
            }
        }
    }


    private static List<JoinPathDefinitionInfo> ParseJoinPaths(
        ExpressionSyntax expression,
        SemanticModel semanticModel)
    {
        var paths =
            new List<JoinPathDefinitionInfo>();


        foreach (var element in GetCollectionElements(expression))
        {
            var initializer =
                GetObjectInitializer(element);

            if (initializer == null)
                continue;


            var path =
                new JoinPathDefinitionInfo();


            foreach (var assignment in initializer.Expressions
                         .OfType<AssignmentExpressionSyntax>())
            {
                var name =
                    (assignment.Left as IdentifierNameSyntax)
                    ?.Identifier.Text;


                switch (name)
                {
                    case "TargetEntity":

                        path =
                            path with
                            {
                                TargetEntity =
                                    GetTypeSymbol(
                                        assignment.Right,
                                        semanticModel)
                            };

                        break;


                    case "Hops":

                        path =
                            path with
                            {
                                Hops =
                                    ParseJoinHops(
                                        assignment.Right,
                                        semanticModel)
                            };

                        break;
                }
            }


            paths.Add(path);
        }


        return paths;
    }


    private static List<JoinHopDefinitionInfo> ParseJoinHops(
        ExpressionSyntax expression,
        SemanticModel semanticModel)
    {
        var hops =
            new List<JoinHopDefinitionInfo>();


        foreach (var element in GetCollectionElements(expression))
        {
            var initializer =
                GetObjectInitializer(element);

            if (initializer == null)
                continue;


            var hop =
                new JoinHopDefinitionInfo();


            foreach (var assignment in initializer.Expressions
                         .OfType<AssignmentExpressionSyntax>())
            {
                var name =
                    (assignment.Left as IdentifierNameSyntax)
                    ?.Identifier.Text;


                switch (name)
                {
                    case "FromEntity":

                        hop =
                            hop with
                            {
                                FromEntity =
                                    GetTypeSymbol(
                                        assignment.Right,
                                        semanticModel)
                            };

                        break;


                    case "FromColumn":

                        hop =
                            hop with
                            {
                                FromColumn =
                                    EvaluateStringLikeExpression(
                                        assignment.Right,
                                        semanticModel)
                            };

                        break;


                    case "ToEntity":

                        hop =
                            hop with
                            {
                                ToEntity =
                                    GetTypeSymbol(
                                        assignment.Right,
                                        semanticModel)
                            };

                        break;


                    case "ToColumn":

                        hop =
                            hop with
                            {
                                ToColumn =
                                    EvaluateStringLikeExpression(
                                        assignment.Right,
                                        semanticModel)
                            };

                        break;
                }
            }


            hops.Add(hop);
        }


        return hops;
    }
    
        private static void ParseEnumOverrides(
        ExpressionSyntax expression,
        FieldInfo field,
        SemanticModel semanticModel)
    {
        InitializerExpressionSyntax? initializer =
            expression switch
            {
                InitializerExpressionSyntax init =>
                    init,

                ObjectCreationExpressionSyntax obj =>
                    obj.Initializer,

                ImplicitObjectCreationExpressionSyntax obj =>
                    obj.Initializer,

                _ => null
            };


        if (initializer == null)
            return;


        foreach (var item in initializer.Expressions)
        {
            if (item is not AssignmentExpressionSyntax assignment)
                continue;


            var source =
                EvaluateEnumName(
                    assignment.Left,
                    semanticModel);


            var destination =
                EvaluateEnumName(
                    assignment.Right,
                    semanticModel);


            if (source != null &&
                destination != null)
            {
                field.EnumOverrides[source] =
                    destination;
            }
        }
    }


    private static void ParseEnumIgnore(
        ExpressionSyntax expression,
        FieldInfo field,
        SemanticModel semanticModel)
    {
        foreach (var value in GetCollectionElements(expression))
        {
            var name =
                EvaluateEnumName(
                    value,
                    semanticModel)
                ??
                EvaluateStringLikeExpression(
                    value,
                    semanticModel);


            if (!string.IsNullOrWhiteSpace(name))
            {
                field.EnumIgnored.Add(name);
            }
        }
    }


    private static string? EvaluateEnumName(
        ExpressionSyntax expression,
        SemanticModel semanticModel)
    {
        if (expression is ImplicitElementAccessSyntax elementAccess)
        {
            expression =
                elementAccess.ArgumentList.Arguments
                    .FirstOrDefault()
                    ?.Expression;


            if (expression == null)
                return null;
        }


        if (expression is InvocationExpressionSyntax invocation &&
            invocation.Expression is IdentifierNameSyntax identifier &&
            identifier.Identifier.Text == "nameof")
        {
            var argument =
                invocation.ArgumentList.Arguments
                    .FirstOrDefault()
                    ?.Expression;


            return argument switch
            {
                MemberAccessExpressionSyntax member =>
                    member.Name.Identifier.Text,


                IdentifierNameSyntax id =>
                    id.Identifier.Text,


                _ => null
            };
        }


        if (expression is MemberAccessExpressionSyntax memberAccess)
        {
            return memberAccess.Name.Identifier.Text;
        }


        if (expression is LiteralExpressionSyntax literal)
        {
            return literal.Token.ValueText;
        }


        return null;
    }


    private static void ResolveDestinationColumns(
        MappingClassInfo info,
        IReadOnlyList<FluentEntityNavigationConvention.DerivedForeignKey> foreignKeys)
    {
        if (info.EntityType == null)
            return;


        foreach (var field in info.FieldMaps)
        {
            if (string.IsNullOrWhiteSpace(
                    field.DestinationName))
            {
                continue;
            }


            var foreignKey =
                foreignKeys.FirstOrDefault(x =>
                    SymbolEqualityComparer.Default.Equals(
                        x.DeclaringEntityType,
                        info.EntityType)
                    &&
                    (
                        string.Equals(
                            x.RawForeignKeyColumn,
                            field.DestinationName,
                            StringComparison.OrdinalIgnoreCase)
                        ||
                        string.Equals(
                            x.ModelForeignKeyProperty,
                            field.DestinationName,
                            StringComparison.OrdinalIgnoreCase)
                    ));


            if (foreignKey == null)
                continue;


            field.DestinationName =
                foreignKey.ModelForeignKeyProperty;
        }
    }
    
        private static void ParseForeignKeys(
        ExpressionSyntax expression,
        MappingClassInfo info,
        SemanticModel semanticModel)
    {
        foreach (var element in GetCollectionElements(expression))
        {
            var initializer =
                GetObjectInitializer(element);

            if (initializer == null)
                continue;


            INamedTypeSymbol? entity = null;
            string? column = null;
            string? modelField = null;


            foreach (var assignment in initializer.Expressions
                         .OfType<AssignmentExpressionSyntax>())
            {
                var name =
                    (assignment.Left as IdentifierNameSyntax)
                    ?.Identifier.Text;


                switch (name)
                {
                    case "Entity":

                        entity =
                            GetTypeSymbol(
                                assignment.Right,
                                semanticModel);

                        break;


                    case "Column":

                        column =
                            EvaluateStringLikeExpression(
                                assignment.Right,
                                semanticModel);

                        break;


                    case "ModelField":

                        modelField =
                            EvaluateStringLikeExpression(
                                assignment.Right,
                                semanticModel);

                        break;
                }
            }


            if (entity == null ||
                string.IsNullOrWhiteSpace(column) ||
                string.IsNullOrWhiteSpace(modelField))
            {
                continue;
            }


            info.FieldMaps.Add(
                new FieldInfo
                {
                    SourceName = modelField,
                    DestinationEntity =
                        entity.Name,
                    DestinationName =
                        column,
                    IsNavigationKey = true
                });
        }
    }



    private static void ParseUpsertKeys(
        ExpressionSyntax expression,
        MappingClassInfo info,
        SemanticModel semanticModel)
    {
        foreach (var element in GetCollectionElements(expression))
        {
            var initializer =
                GetObjectInitializer(element);

            if (initializer == null)
                continue;


            string? entity = null;
            string? column = null;


            foreach (var assignment in initializer.Expressions
                         .OfType<AssignmentExpressionSyntax>())
            {
                var name =
                    (assignment.Left as IdentifierNameSyntax)
                    ?.Identifier.Text;


                switch (name)
                {
                    case "Entity":

                        entity =
                            EvaluateTypeName(
                                assignment.Right);

                        break;


                    case "Column":

                        column =
                            EvaluateStringLikeExpression(
                                assignment.Right,
                                semanticModel);

                        break;
                }
            }


            if (string.IsNullOrWhiteSpace(entity) ||
                string.IsNullOrWhiteSpace(column))
            {
                continue;
            }


            info.UpsertKeys.Add(
                new UpsertKeyInfo
                {
                    Entity = entity,
                    Key = column
                });
        }
    }
    
        private static void ParseEntities(
        ExpressionSyntax expression,
        MappingClassInfo info,
        SemanticModel semanticModel)
    {
        foreach (var element in GetCollectionElements(expression))
        {
            var initializer =
                GetObjectInitializer(element);

            if (initializer == null)
                continue;


            var entity =
                new EntityDefinitionInfo();


            foreach (var assignment in initializer.Expressions
                         .OfType<AssignmentExpressionSyntax>())
            {
                var name =
                    (assignment.Left as IdentifierNameSyntax)
                    ?.Identifier.Text;


                switch (name)
                {
                    case "Entity":

                        entity.EntityType =
                            GetTypeSymbol(
                                assignment.Right,
                                semanticModel);

                        break;


                    case "Schema":

                        entity.Schema =
                            EvaluateStringLikeExpression(
                                assignment.Right,
                                semanticModel);

                        break;


                    case "ModelKey":
                    {
                        var value =
                            EvaluateStringLikeExpression(
                                assignment.Right,
                                semanticModel);

                        entity.ModelKey = value;
                        entity.FromColumn = value;

                        break;
                    }


                    case "EntityKey":
                    {
                        var value =
                            EvaluateStringLikeExpression(
                                assignment.Right,
                                semanticModel);

                        entity.EntityKey = value;
                        entity.ToColumn = value;

                        break;
                    }


                    case "AliasProperty":

                        entity.AliasProperty =
                            EvaluateStringLikeExpression(
                                assignment.Right,
                                semanticModel);

                        break;


                    case "IsPrimary":

                        entity.IsPrimary =
                            assignment.Right
                                .ToString()
                                .Equals(
                                    "true",
                                    StringComparison.OrdinalIgnoreCase);

                        break;
                }
            }


            if (entity.EntityType == null)
                continue;


            info.Definition.Entities.Add(entity);
        }


        if (info.Definition.Entities.Count > 0 &&
            !info.Definition.Entities.Any(x => x.IsPrimary))
        {
            info.Definition.Entities[0].IsPrimary = true;
        }
    }



    private static void ParseFields(
        ExpressionSyntax expression,
        MappingClassInfo info,
        SemanticModel semanticModel)
    {
        foreach (var element in GetCollectionElements(expression))
        {
            var initializer =
                GetObjectInitializer(element);

            if (initializer == null)
                continue;


            var field =
                new FieldInfo();


            foreach (var assignment in initializer.Expressions
                         .OfType<AssignmentExpressionSyntax>())
            {
                var name =
                    (assignment.Left as IdentifierNameSyntax)
                    ?.Identifier.Text;


                switch (name)
                {
                    case "Source":

                        field.SourceName =
                            EvaluateStringLikeExpression(
                                assignment.Right,
                                semanticModel);

                        break;


                    case "Destination":

                        field.DestinationName =
                            EvaluateStringLikeExpression(
                                assignment.Right,
                                semanticModel);

                        break;


                    case "Entity":

                        field.DestinationEntity =
                            EvaluateTypeName(
                                assignment.Right);

                        break;


                    case "EnumMapping":

                        ParseEnumMapping(
                            assignment.Right,
                            field,
                            semanticModel);

                        break;


                    case "IsNavigationKey":

                        field.IsNavigationKey =
                            assignment.Right
                                .ToString()
                                .Equals(
                                    "true",
                                    StringComparison.OrdinalIgnoreCase);

                        break;
                }
            }


            if (string.IsNullOrWhiteSpace(field.SourceName) ||
                string.IsNullOrWhiteSpace(field.DestinationEntity) ||
                string.IsNullOrWhiteSpace(field.DestinationName))
            {
                continue;
            }


            info.FieldMaps.Add(field);
        }


        GenerateNavigationKeyFields(info);
    }
    
        private static void GenerateNavigationKeyFields(
        MappingClassInfo info)
    {
        foreach (var navigation in info.Definition.Navigations)
        {
            if (navigation.Paths == null)
                continue;


            foreach (var path in navigation.Paths)
            {
                foreach (var hop in path.Hops)
                {
                    if (hop.FromEntity == null ||
                        hop.ToEntity == null)
                    {
                        continue;
                    }


                    var relationshipEntity =
                        hop.FromEntity.Name;


                    var fromField =
                        ToCamelCase(
                            hop.FromColumn);


                    var toField =
                        ToCamelCase(
                            hop.ToColumn);


                    if (!string.IsNullOrWhiteSpace(
                            fromField)
                        &&
                        !info.FieldMaps.Any(f =>
                            string.Equals(
                                f.SourceName,
                                fromField,
                                StringComparison.Ordinal)
                            &&
                            string.Equals(
                                f.DestinationEntity,
                                relationshipEntity,
                                StringComparison.Ordinal)
                            &&
                            string.Equals(
                                f.DestinationName,
                                hop.FromColumn,
                                StringComparison.Ordinal)))
                    {
                        info.FieldMaps.Add(
                            new FieldInfo
                            {
                                SourceName =
                                    fromField,

                                DestinationEntity =
                                    relationshipEntity,

                                DestinationName =
                                    hop.FromColumn,

                                IsNavigationKey = true
                            });
                    }


                    if (!string.IsNullOrWhiteSpace(
                            toField)
                        &&
                        !info.FieldMaps.Any(f =>
                            string.Equals(
                                f.SourceName,
                                toField,
                                StringComparison.Ordinal)
                            &&
                            string.Equals(
                                f.DestinationEntity,
                                relationshipEntity,
                                StringComparison.Ordinal)
                            &&
                            string.Equals(
                                f.DestinationName,
                                hop.ToColumn,
                                StringComparison.Ordinal)))
                    {
                        info.FieldMaps.Add(
                            new FieldInfo
                            {
                                SourceName =
                                    toField,

                                DestinationEntity =
                                    relationshipEntity,

                                DestinationName =
                                    hop.ToColumn,

                                IsNavigationKey = true
                            });
                    }
                }
            }
        }
    }



    private static string ToCamelCase(
        string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;


        if (value.Length == 1)
            return value.ToLowerInvariant();


        return char.ToLowerInvariant(value[0]) +
               value.Substring(1);
    }
    
        private static void ParseGraph(
        ExpressionSyntax expression,
        MappingClassInfo info,
        SemanticModel semanticModel)
    {
        var initializer =
            GetObjectInitializer(expression);

        if (initializer == null)
            return;


        var graph =
            new GraphInfo();


        foreach (var assignment in initializer.Expressions
                     .OfType<AssignmentExpressionSyntax>())
        {
            var name =
                (assignment.Left as IdentifierNameSyntax)
                ?.Identifier.Text;


            switch (name)
            {
                case "GraphName":

                    graph.GraphName =
                        EvaluateStringLikeExpression(
                            assignment.Right,
                            semanticModel)
                        ?? string.Empty;

                    break;


                case "EdgeLabel":

                    graph.EdgeLabel =
                        EvaluateStringLikeExpression(
                            assignment.Right,
                            semanticModel)
                        ?? string.Empty;

                    break;


                case "EdgeKey":

                    graph.EdgeKey =
                        EvaluateStringLikeExpression(
                            assignment.Right,
                            semanticModel)
                        ?? string.Empty;

                    break;


                case "From":

                    graph.From =
                        ParseVertex(
                            assignment.Right,
                            semanticModel);

                    break;


                case "To":

                    graph.To =
                        ParseVertex(
                            assignment.Right,
                            semanticModel);

                    break;


                case "FromJoinColumn":

                    graph.FromJoinColumn =
                        EvaluateStringLikeExpression(
                            assignment.Right,
                            semanticModel)
                        ?? string.Empty;

                    break;


                case "ToJoinColumn":

                    graph.ToJoinColumn =
                        EvaluateStringLikeExpression(
                            assignment.Right,
                            semanticModel)
                        ?? string.Empty;

                    break;
            }
        }


        if (string.IsNullOrWhiteSpace(
                graph.EdgeKey))
        {
            var primaryKey =
                info.Definition.PrimaryKey
                    .FirstOrDefault();


            if (primaryKey != null)
            {
                graph.EdgeKey =
                    primaryKey.ColumnKey;
            }
        }


        info.Graph = graph;
    }



    private static VertexInfo? ParseVertex(
        ExpressionSyntax expression,
        SemanticModel semanticModel)
    {
        var initializer =
            GetObjectInitializer(expression);

        if (initializer == null)
            return null;


        var vertex =
            new VertexInfo();


        foreach (var assignment in initializer.Expressions
                     .OfType<AssignmentExpressionSyntax>())
        {
            var name =
                (assignment.Left as IdentifierNameSyntax)
                ?.Identifier.Text;


            switch (name)
            {
                case "Label":

                    vertex.Label =
                        EvaluateStringLikeExpression(
                            assignment.Right,
                            semanticModel)
                        ?? string.Empty;

                    break;


                case "GraphProperty":

                    vertex.GraphProperty =
                        EvaluateStringLikeExpression(
                            assignment.Right,
                            semanticModel)
                        ?? string.Empty;

                    break;


                case "ForeignKeyColumn":

                    vertex.ForeignKeyColumn =
                        EvaluateStringLikeExpression(
                            assignment.Right,
                            semanticModel)
                        ?? string.Empty;

                    break;


                case "KeyColumn":

                    vertex.KeyColumn =
                        EvaluateStringLikeExpression(
                            assignment.Right,
                            semanticModel)
                        ?? string.Empty;

                    break;


                case "Alias":

                    vertex.Alias =
                        EvaluateStringLikeExpression(
                            assignment.Right,
                            semanticModel);

                    break;
            }
        }


        if (string.IsNullOrWhiteSpace(
                vertex.KeyColumn))
        {
            vertex.KeyColumn =
                vertex.GraphProperty;
        }


        if (string.IsNullOrWhiteSpace(
                vertex.ForeignKeyColumn))
        {
            vertex.ForeignKeyColumn =
                vertex.KeyColumn;
        }


        return vertex;
    }
    
        private static ExpressionSyntax? GetPropertyExpression(
        PropertyDeclarationSyntax property)
    {
        if (property.ExpressionBody != null)
        {
            return property.ExpressionBody.Expression;
        }


        if (property.AccessorList != null)
        {
            var getter =
                property.AccessorList.Accessors
                    .FirstOrDefault(x =>
                        x.Keyword.Text == "get");


            if (getter?.ExpressionBody != null)
            {
                return getter.ExpressionBody.Expression;
            }


            if (getter?.Body != null)
            {
                return getter.Body.Statements
                    .OfType<ReturnStatementSyntax>()
                    .FirstOrDefault()
                    ?.Expression;
            }
        }


        return null;
    }



    private static InitializerExpressionSyntax? GetObjectInitializer(
        ExpressionSyntax expression)
    {
        return expression switch
        {
            ObjectCreationExpressionSyntax obj =>
                obj.Initializer,

            ImplicitObjectCreationExpressionSyntax obj =>
                obj.Initializer,

            _ => null
        };
    }



    private static IEnumerable<ExpressionSyntax> GetCollectionElements(
        ExpressionSyntax expression)
    {
        switch (expression)
        {
            case CollectionExpressionSyntax collection:

                foreach (var element in collection.Elements)
                {
                    if (element is ExpressionElementSyntax item)
                    {
                        yield return item.Expression;
                    }
                }

                yield break;


            case ImplicitArrayCreationExpressionSyntax array:

                foreach (var item in array.Initializer.Expressions)
                {
                    yield return item;
                }

                yield break;


            case ArrayCreationExpressionSyntax array:

                if (array.Initializer != null)
                {
                    foreach (var item in array.Initializer.Expressions)
                    {
                        yield return item;
                    }
                }

                yield break;
        }
    }



    private static INamedTypeSymbol? GetTypeSymbol(
        ExpressionSyntax expression,
        SemanticModel semanticModel)
    {
        if (expression is TypeOfExpressionSyntax typeOf)
        {
            return semanticModel
                .GetTypeInfo(typeOf.Type)
                .Type as INamedTypeSymbol;
        }


        return null;
    }



    private static string? EvaluateStringLikeExpression(
        ExpressionSyntax expression,
        SemanticModel semanticModel)
    {
        var constant =
            semanticModel.GetConstantValue(expression);


        if (constant.HasValue &&
            constant.Value is string value)
        {
            return value;
        }


        if (expression is LiteralExpressionSyntax literal)
        {
            return literal.Token.ValueText;
        }


        if (expression is MemberAccessExpressionSyntax member)
        {
            return member.Name.Identifier.Text;
        }


        return null;
    }



    private static string? EvaluateTypeName(
        ExpressionSyntax expression)
    {
        if (expression is TypeOfExpressionSyntax typeOf)
        {
            return typeOf.Type
                .ToString()
                .Split('.')
                .Last();
        }


        return null;
    }
}