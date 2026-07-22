using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using CoffeeBeanery.GraphQL.Core.Mapping.Generators.Model;

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

        var expression = GetPropertyExpression(property);

        if (expression == null)
            return info;

        var initializer = GetObjectInitializer(expression);

        if (initializer == null)
            return info;

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
                    ParseNavigations(assignment.Right, info, semanticModel);
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
                            assignment.Right, semanticModel);
                    break;

                case "Entities":
                    ParseEntities(
                        assignment.Right,
                        info,
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
                        info, semanticModel);
                    break;
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
                .FirstOrDefault(k => k.IsPrimary)
                ?.EntityType
            ?? info.Definition.Entities
                .FirstOrDefault()
                ?.EntityType;
        
        ResolvePropertyTypes(info);

        return info;
    }
    
    private static void ParseForeignKeys(
    ExpressionSyntax expression,
    MappingClassInfo info,
    SemanticModel semanticModel)
{
    foreach (var element in GetCollectionElements(expression))
    {
        var initializer = GetObjectInitializer(element);

        if (initializer == null)
            continue;

        INamedTypeSymbol? resolvedEntity = null;
        string? resolvedColumn = null;
        INamedTypeSymbol? resolvedDependsOn = null;
        string? resolvedPrincipalColumn = null;
        string? resolvedModelField = null;

        foreach (var assignment in initializer.Expressions
                     .OfType<AssignmentExpressionSyntax>())
        {
            var name =
                (assignment.Left as IdentifierNameSyntax)
                ?.Identifier.Text;

            switch (name)
            {
                case "Entity":
                    resolvedEntity =
                        GetTypeSymbol(
                            assignment.Right,
                            semanticModel);
                    break;

                case "Column":
                    resolvedColumn =
                        EvaluateStringLikeExpression(
                            assignment.Right, semanticModel);
                    break;

                case "DependsOn":
                    resolvedDependsOn =
                        GetTypeSymbol(
                            assignment.Right,
                            semanticModel);
                    break;

                case "Principal":
                    resolvedPrincipalColumn =
                        EvaluateStringLikeExpression(
                            assignment.Right, semanticModel);
                    break;

                case "ModelField":
                    resolvedModelField =
                        EvaluateStringLikeExpression(
                            assignment.Right, semanticModel);
                    break;
            }
        }

        if (resolvedEntity != null &&
            resolvedColumn != null &&
            resolvedDependsOn != null &&
            resolvedPrincipalColumn != null)
        {
            info.Definition.ForeignKeys.Add(
                new ForeignKeyDefinitionInfo
                {
                    Entity = resolvedEntity,
                    Column = resolvedColumn,
                    DependsOn = resolvedDependsOn,
                    Principal = resolvedPrincipalColumn,
                    ModelField = resolvedModelField
                });
        }
    }
}
    
    private static void ResolvePropertyTypes(MappingClassInfo info)
    {
        if (info.ModelType == null)
            return;

        var modelProperties =
            info.ModelType
                .GetMembers()
                .OfType<IPropertySymbol>()
                .ToDictionary(p => p.Name, StringComparer.Ordinal);

        foreach (var field in info.FieldMaps)
        {
            if (field.PropertyType != null)
                continue; // already resolved elsewhere (e.g. FieldMapGeneration)

            if (string.IsNullOrWhiteSpace(field.SourceName))
                continue;

            if (modelProperties.TryGetValue(field.SourceName, out var property))
            {
                field.PropertyType = property.Type;
            }
        }
    }
    
    private static void ParsePrimaryKeys(
        ExpressionSyntax expression,
        MappingClassInfo info,
        SemanticModel semanticModel)
    {
        foreach (var element in GetCollectionElements(expression))
        {
            var initializer = GetObjectInitializer(element);
            if (initializer == null)
                continue;

            INamedTypeSymbol? resolvedEntity = null;
            string? resolvedModelKey = null;
            string? resolvedColumnKey = null;

            foreach (var assignment in initializer.Expressions.OfType<AssignmentExpressionSyntax>())
            {
                var name = (assignment.Left as IdentifierNameSyntax)?.Identifier.Text;

                switch (name)
                {
                    case "Entity":
                        resolvedEntity = GetTypeSymbol(assignment.Right, semanticModel);
                        break;

                    case "ModelKey":
                        resolvedModelKey = EvaluateStringLikeExpression(assignment.Right, semanticModel);
                        break;

                    case "ColumnKey":
                        resolvedColumnKey = EvaluateStringLikeExpression(assignment.Right, semanticModel);
                        break;
                }
            }

            if (resolvedEntity != null && !string.IsNullOrWhiteSpace(resolvedModelKey) && !string.IsNullOrWhiteSpace(resolvedColumnKey))
            {
                info.Definition.PrimaryKey.Add(new PrimaryKeyDefinitionInfo
                {
                    Entity = resolvedEntity,
                    ModelKey = resolvedModelKey!,
                    ColumnKey = resolvedColumnKey!
                });
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
        var initializer = GetObjectInitializer(element);
        if (initializer == null) continue;

        var nav = new NavigationDefinitionInfo { NavigationName = "" };

        foreach (var assignment in initializer.Expressions.OfType<AssignmentExpressionSyntax>())
        {
            var name = (assignment.Left as IdentifierNameSyntax)?.Identifier.Text;

            switch (name)
            {
                case "NavigationName":
                    nav = nav with { NavigationName = EvaluateStringLikeExpression(assignment.Right, semanticModel) ?? "" };
                    break;

                case "TargetModel":
                    nav = nav with { TargetModel = GetTypeSymbol(assignment.Right, semanticModel) };
                    break;

                case "IsCollection":
                    nav = nav with
                    {
                        IsCollection = assignment.Right.ToString()
                            .Equals("true", StringComparison.OrdinalIgnoreCase)
                    };
                    break;

                case "Paths":
                    nav = nav with { Paths = ParseJoinPaths(assignment.Right, semanticModel) };
                    break;
            }
        }

        if (!string.IsNullOrWhiteSpace(nav.NavigationName))
            info.Definition.Navigations.Add(nav);
    }
}

private static List<JoinPathDefinitionInfo> ParseJoinPaths(
    ExpressionSyntax expression,
    SemanticModel semanticModel)
{
    var paths = new List<JoinPathDefinitionInfo>();

    foreach (var element in GetCollectionElements(expression))
    {
        var initializer = GetObjectInitializer(element);
        if (initializer == null) continue;

        var path = new JoinPathDefinitionInfo();

        foreach (var assignment in initializer.Expressions.OfType<AssignmentExpressionSyntax>())
        {
            var name = (assignment.Left as IdentifierNameSyntax)?.Identifier.Text;

            switch (name)
            {
                case "TargetEntity":
                    path = path with { TargetEntity = GetTypeSymbol(assignment.Right, semanticModel) };
                    break;

                case "Hops":
                    path = path with { Hops = ParseJoinHops(assignment.Right, semanticModel) };
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
    var hops = new List<JoinHopDefinitionInfo>();

    foreach (var element in GetCollectionElements(expression))
    {
        var initializer = GetObjectInitializer(element);
        if (initializer == null) continue;

        var hop = new JoinHopDefinitionInfo();

        foreach (var assignment in initializer.Expressions.OfType<AssignmentExpressionSyntax>())
        {
            var name = (assignment.Left as IdentifierNameSyntax)?.Identifier.Text;

            switch (name)
            {
                case "FromEntity":
                    hop = hop with { FromEntity = GetTypeSymbol(assignment.Right, semanticModel) };
                    break;
                case "FromColumn":
                    hop = hop with { FromColumn = EvaluateStringLikeExpression(assignment.Right, semanticModel) };
                    break;
                case "ToEntity":
                    hop = hop with { ToEntity = GetTypeSymbol(assignment.Right, semanticModel) };
                    break;
                case "ToColumn":
                    hop = hop with { ToColumn = EvaluateStringLikeExpression(assignment.Right, semanticModel) };
                    break;
            }
        }

        hops.Add(hop);
    }

    return hops;
}

    private static ExpressionSyntax? GetPropertyExpression(
        PropertyDeclarationSyntax property)
    {
        // => new()
        if (property.ExpressionBody != null)
            return property.ExpressionBody.Expression;


        if (property.AccessorList != null)
        {
            var getter =
                property.AccessorList.Accessors
                    .FirstOrDefault(x =>
                        x.Keyword.Text == "get");


            // get => new()
            if (getter?.ExpressionBody != null)
                return getter.ExpressionBody.Expression;


            // get
            // {
            //     return new MappingDefinition();
            // }
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


    private static void ParseEntities(
    ExpressionSyntax expression,
    MappingClassInfo info,
    SemanticModel semanticModel)
{
    foreach (var element in GetCollectionElements(expression))
    {
        var initializer = GetObjectInitializer(element);

        if (initializer == null)
            continue;

        var entity = new EntityDefinitionInfo();

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

                case "ModelKey":
                    entity.FromColumn =
                        EvaluateStringLikeExpression(
                            assignment.Right, semanticModel);
                    break;

                case "EntityKey":
                    entity.ToColumn =
                        EvaluateStringLikeExpression(
                            assignment.Right, semanticModel);
                    break;

                case "AliasProperty":
                    entity.AliasProperty =
                        EvaluateStringLikeExpression(
                            assignment.Right, semanticModel);
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


    // Composite mappings must always have a primary entity.
    // The primary entity drives StorageEntityId/schema/name generation.
    if (info.Definition.Entities.Count > 0 &&
        !info.Definition.Entities.Any(x => x.IsPrimary))
    {
        info.Definition.Entities[0].IsPrimary = true;
    }
}
    
        private static void ParseUpsertKeys(
        ExpressionSyntax expression,
        MappingClassInfo info,
        SemanticModel semanticModel)
    {
        foreach (var element in GetCollectionElements(expression))
        {
            var initializer = GetObjectInitializer(element);

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
                                assignment.Right, semanticModel);
                        break;
                }
            }


            if (entity != null && column != null)
            {
                info.UpsertKeys.Add(
                    new UpsertKeyInfo
                    {
                        Entity = entity,
                        Key = column
                    });
            }
        }
    }


    private static IEnumerable<ExpressionSyntax> GetCollectionElements(
        ExpressionSyntax expression)
    {
        switch (expression)
        {
            case CollectionExpressionSyntax collection:
                foreach (var element in collection.Elements)
                {
                    if (element is ExpressionElementSyntax expr)
                        yield return expr.Expression;
                }

                yield break;


            case ImplicitArrayCreationExpressionSyntax array:
                foreach (var item in array.Initializer.Expressions)
                    yield return item;

                yield break;


            case ArrayCreationExpressionSyntax array:
                if (array.Initializer != null)
                {
                    foreach (var item in array.Initializer.Expressions)
                        yield return item;
                }

                yield break;
        }
    }


    private static void ParseFields(
        ExpressionSyntax expression,
        MappingClassInfo info,
        SemanticModel semanticModel)
    {
        foreach (var element in GetCollectionElements(expression))
        {
            var initializer = GetObjectInitializer(element);
            if (initializer == null) continue;

            var field = new FieldInfo();

            foreach (var assignment in initializer.Expressions.OfType<AssignmentExpressionSyntax>())
            {
                var identifier = (assignment.Left as IdentifierNameSyntax)?.Identifier;
                var name = identifier.Value.Text;

                switch (name)
                {
                    case "Source":
                        field.SourceName = EvaluateStringLikeExpression(assignment.Right, semanticModel);
                        break;
                    case "Destination":
                        field.DestinationName = EvaluateStringLikeExpression(assignment.Right, semanticModel);
                        break;
                    case "Entity":
                        field.DestinationEntity = EvaluateTypeName(assignment.Right);
                        break;
                    case "EnumMapping":
                        ParseEnumMapping(assignment.Right, field, semanticModel);
                        break;
                    case "IsNavigationKey":
                        field.IsNavigationKey =
                            assignment.Right.ToString().Equals("true", StringComparison.OrdinalIgnoreCase);
                        break;
                }
            }

            if (!string.IsNullOrWhiteSpace(field.SourceName) &&
                !string.IsNullOrWhiteSpace(field.DestinationEntity) &&
                !string.IsNullOrWhiteSpace(field.DestinationName))
            {
                info.FieldMaps.Add(field);
            }
        }
    }

    private static void ParseEnumMapping(
    ExpressionSyntax expression,
    FieldInfo field,
    SemanticModel semanticModel)
{
    var typeInfo = semanticModel.GetTypeInfo(expression);

    if (typeInfo.Type is not INamedTypeSymbol genericType)
        return;

    if (!genericType.IsGenericType || genericType.TypeArguments.Length != 2)
        return;

    field.ModelEnumType = genericType.TypeArguments[0] as INamedTypeSymbol;
    field.EntityEnumType = genericType.TypeArguments[1] as INamedTypeSymbol;

    if (field.ModelEnumType == null || field.EntityEnumType == null)
        return;

    var initializer = expression switch
    {
        ObjectCreationExpressionSyntax obj => obj.Initializer,
        ImplicitObjectCreationExpressionSyntax obj => obj.Initializer,
        _ => null
    };

    if (initializer != null)
    {
        foreach (var assignment in initializer.Expressions.OfType<AssignmentExpressionSyntax>())
        {
            var name = (assignment.Left as IdentifierNameSyntax)?.Identifier.Text;

            switch (name)
            {
                case "Overrides":
                    ParseEnumOverrides(assignment.Right, field, semanticModel);
                    break;

                case "Ignore":
                    ParseEnumIgnore(assignment.Right, field, semanticModel);
                    break;
            }
        }
    }

    if (initializer != null)
    {
        foreach (var assignment in initializer.Expressions.OfType<AssignmentExpressionSyntax>())
        {
            var name = (assignment.Left as IdentifierNameSyntax)?.Identifier.Text;

            switch (name)
            {
                case "Overrides":
                    ParseEnumOverrides(assignment.Right, field, semanticModel);
                    break;

                case "Ignore":
                    ParseEnumIgnore(assignment.Right, field, semanticModel);
                    break;
            }
        }
    }
}

// private static void BuildEnumConversionMaps(FieldInfo field)
// {
//     var modelEnum = field.ModelEnumType!;
//     var entityEnum = field.EntityEnumType!;
//
//     var entityMembersByName =
//         entityEnum.GetMembers()
//             .OfType<IFieldSymbol>()
//             .Where(f => f.IsConst && f.HasConstantValue)
//             .ToDictionary(
//                 f => f.Name,
//                 f => Convert.ToInt32(f.ConstantValue),
//                 StringComparer.Ordinal);
//
//     field.FromEnum = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
//
//     foreach (var modelMember in modelEnum.GetMembers()
//                  .OfType<IFieldSymbol>()
//                  .Where(f => f.IsConst && f.HasConstantValue))
//     {
//         if (field.EnumIgnored.Contains(modelMember.Name))
//             continue;
//
//         // Overrides lets the author say "model member X corresponds to
//         // entity member Y" when the names differ; otherwise assume the
//         // same name exists on both enums.
//         var entityMemberName =
//             field.EnumOverrides.TryGetValue(modelMember.Name, out var overridden)
//                 ? overridden
//                 : modelMember.Name;
//
//         if (!entityMembersByName.TryGetValue(entityMemberName, out var entityValue))
//         {
//             throw new InvalidOperationException(
//                 $"Enum mapping failed. '{modelEnum.Name}.{modelMember.Name}' maps to '{entityEnum.Name}.{entityMemberName}', but that member does not exist.");
//         }
//
//         field.FromEnum[modelMember.Name] = entityValue;
//     }
// }

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

    private static void ParseEnumOverrides(
        ExpressionSyntax expression,
        FieldInfo field,
        SemanticModel semanticModel)
    {
        InitializerExpressionSyntax? initializer = expression switch
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


            if (source != null && destination != null)
            {
                field.EnumOverrides[source] = destination;
            }
        }
    }

private static string? ExtractNameOf(ExpressionSyntax expression)
{
    if (expression is ElementAccessExpressionSyntax element)
    {
        expression =
            element.ArgumentList.Arguments[0].Expression;
    }

    if (expression is InvocationExpressionSyntax invocation &&
        invocation.Expression is IdentifierNameSyntax id &&
        id.Identifier.Text == "nameof")
    {
        var arg =
            invocation.ArgumentList.Arguments[0].Expression;

        return arg switch
        {
            MemberAccessExpressionSyntax member =>
                member.Name.Identifier.Text,

            IdentifierNameSyntax name =>
                name.Identifier.Text,

            _ => null
        };
    }

    return null;
}
    
private static void ParseEnumIgnore(
    ExpressionSyntax expression,
    FieldInfo field,
    SemanticModel semanticModel)
{
    foreach (var value in GetCollectionElements(expression))
    {
        var name =
            EvaluateEnumName(value, semanticModel)
            ?? EvaluateStringLikeExpression(value, semanticModel);

        if (name != null)
            field.EnumIgnored.Add(name);
    }
}


    private static void ParseGraph(
        ExpressionSyntax expression,
        MappingClassInfo info,
        SemanticModel semanticModel)
    {
        var initializer = GetObjectInitializer(expression);

        if (initializer == null)
            return;


        var graph = new GraphInfo();


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
                            assignment.Right, semanticModel) ?? "";
                    break;

                case "EdgeLabel":
                    graph.EdgeLabel =
                        EvaluateStringLikeExpression(
                            assignment.Right, semanticModel) ?? "";
                    break;

                case "EdgeKey":
                    graph.EdgeKey =
                        EvaluateStringLikeExpression(
                            assignment.Right, semanticModel) ?? "";
                    break;

                case "From":
                    graph.From =
                        ParseVertex(
                            assignment.Right, semanticModel);
                    break;

                case "To":
                    graph.To =
                        ParseVertex(
                            assignment.Right, semanticModel);
                    break;

                case "FromJoinColumn":
                    graph.FromJoinColumn =
                        EvaluateStringLikeExpression(
                            assignment.Right, semanticModel) ?? "";
                    break;

                case "ToJoinColumn":
                    graph.ToJoinColumn =
                        EvaluateStringLikeExpression(
                            assignment.Right, semanticModel) ?? "";
                    break;
            }
        }


        info.Graph = graph;
    }


    private static VertexInfo? ParseVertex(ExpressionSyntax expression,
        SemanticModel semanticModel)
    {
        var initializer = GetObjectInitializer(expression);
        if (initializer == null)
            return null;

        var vertex = new VertexInfo();

        foreach (var assignment in initializer.Expressions.OfType<AssignmentExpressionSyntax>())
        {
            var name = (assignment.Left as IdentifierNameSyntax)?.Identifier.Text;

            switch (name)
            {
                case "Label":
                    vertex.Label = EvaluateStringLikeExpression(assignment.Right, semanticModel) ?? "";
                    break;

                case "GraphProperty":
                    vertex.GraphProperty = EvaluateStringLikeExpression(assignment.Right, semanticModel) ?? "";
                    break;

                case "ForeignKeyColumn":
                    vertex.ForeignKeyColumn = EvaluateStringLikeExpression(assignment.Right, semanticModel) ?? "";
                    break;

                case "KeyColumn":
                    vertex.KeyColumn = EvaluateStringLikeExpression(assignment.Right, semanticModel) ?? "";
                    break;

                case "Alias":
                    vertex.Alias = EvaluateStringLikeExpression(assignment.Right, semanticModel);
                    break;
            }
        }

        return vertex;
    }


    private static string? EvaluateStringLikeExpression(
        ExpressionSyntax expression,
        SemanticModel semanticModel)
    {
        //
        // nameof(...)
        //
        var constant =
            semanticModel
                .GetConstantValue(expression);

        if (constant.HasValue &&
            constant.Value is string s)
        {
            return s;
        }


        //
        // Member access
        //
        if (expression is MemberAccessExpressionSyntax member)
        {
            return member.Name.Identifier.Text;
        }


        //
        // Literal string
        //
        if (expression is LiteralExpressionSyntax literal)
        {
            return literal.Token.ValueText;
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