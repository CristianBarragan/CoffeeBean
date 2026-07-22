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

                case "Schema":
                    info.Schema =
                        EvaluateStringLikeExpression(
                            assignment.Right);
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
                        info);
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
                        info);
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
                        resolvedModelKey = EvaluateStringLikeExpression(assignment.Right);
                        break;

                    case "ColumnKey":
                        resolvedColumnKey = EvaluateStringLikeExpression(assignment.Right);
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
                    nav = nav with { NavigationName = EvaluateStringLikeExpression(assignment.Right) ?? "" };
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
                    hop = hop with { FromColumn = EvaluateStringLikeExpression(assignment.Right) };
                    break;
                case "ToEntity":
                    hop = hop with { ToEntity = GetTypeSymbol(assignment.Right, semanticModel) };
                    break;
                case "ToColumn":
                    hop = hop with { ToColumn = EvaluateStringLikeExpression(assignment.Right) };
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
                            assignment.Right);
                    break;

                case "EntityKey":
                    entity.ToColumn =
                        EvaluateStringLikeExpression(
                            assignment.Right);
                    break;

                case "AliasProperty":
                    entity.AliasProperty =
                        EvaluateStringLikeExpression(
                            assignment.Right);
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
        MappingClassInfo info)
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
                                assignment.Right);
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
                        field.SourceName = EvaluateStringLikeExpression(assignment.Right);
                        break;
                    case "Destination":
                        field.DestinationName = EvaluateStringLikeExpression(assignment.Right);
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

        if (!genericType.IsGenericType ||
            genericType.TypeArguments.Length != 2)
        {
            return;
        }

        field.ModelEnumType =
            genericType.TypeArguments[0] as INamedTypeSymbol;

        field.EntityEnumType =
            genericType.TypeArguments[1] as INamedTypeSymbol;

        if (field.ModelEnumType == null ||
            field.EntityEnumType == null)
        {
            return;
        }

        var initializer = expression switch
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
                        field);
                    break;
            }
        }
    }

    private static string? EvaluateEnumName(
        ExpressionSyntax expression,
        SemanticModel semanticModel)
    {
        // nameof(ProductType.CreditCardProduct)
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

        // EnumType.Member syntax
        if (expression is MemberAccessExpressionSyntax memberAccess)
        {
            return memberAccess.Name.Identifier.Text;
        }

        // "CreditCardProduct"
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
        FieldInfo field)
    {
        foreach (var value in GetCollectionElements(expression))
        {
            var name =
                EvaluateStringLikeExpression(value);

            if (name != null)
                field.EnumIgnored.Add(name);
        }
    }


    private static void ParseGraph(
        ExpressionSyntax expression,
        MappingClassInfo info)
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
                            assignment.Right) ?? "";
                    break;

                case "EdgeLabel":
                    graph.EdgeLabel =
                        EvaluateStringLikeExpression(
                            assignment.Right) ?? "";
                    break;

                case "EdgeKey":
                    graph.EdgeKey =
                        EvaluateStringLikeExpression(
                            assignment.Right) ?? "";
                    break;

                case "From":
                    graph.From =
                        ParseVertex(
                            assignment.Right);
                    break;

                case "To":
                    graph.To =
                        ParseVertex(
                            assignment.Right);
                    break;

                case "FromJoinColumn":
                    graph.FromJoinColumn =
                        EvaluateStringLikeExpression(
                            assignment.Right) ?? "";
                    break;

                case "ToJoinColumn":
                    graph.ToJoinColumn =
                        EvaluateStringLikeExpression(
                            assignment.Right) ?? "";
                    break;
            }
        }


        info.Graph = graph;
    }


    private static VertexInfo? ParseVertex(ExpressionSyntax expression)
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
                    vertex.Label = EvaluateStringLikeExpression(assignment.Right) ?? "";
                    break;

                case "GraphProperty":
                    vertex.GraphProperty = EvaluateStringLikeExpression(assignment.Right) ?? "";
                    break;

                case "ForeignKeyColumn":
                    vertex.ForeignKeyColumn = EvaluateStringLikeExpression(assignment.Right) ?? "";
                    break;

                case "KeyColumn":
                    vertex.KeyColumn = EvaluateStringLikeExpression(assignment.Right) ?? "";
                    break;

                case "Alias":
                    vertex.Alias = EvaluateStringLikeExpression(assignment.Right);
                    break;
            }
        }

        return vertex;
    }


    private static string? EvaluateStringLikeExpression(
        ExpressionSyntax expression)
    {
        if (expression is InvocationExpressionSyntax invocation &&
            invocation.Expression is IdentifierNameSyntax identifier &&
            identifier.Identifier.Text == "nameof")
        {
            var arg =
                invocation.ArgumentList.Arguments
                    .FirstOrDefault()
                    ?.Expression;


            return arg switch
            {
                IdentifierNameSyntax id =>
                    id.Identifier.Text,

                MemberAccessExpressionSyntax member =>
                    member.Name.Identifier.Text,

                _ => null
            };
        }


        if (expression is LiteralExpressionSyntax literal)
            return literal.Token.ValueText;


        if (expression is InterpolatedStringExpressionSyntax interpolated)
        {
            var parts = new List<string>();

            foreach (var content in interpolated.Contents)
            {
                switch (content)
                {
                    case InterpolatedStringTextSyntax text:
                        parts.Add(text.TextToken.ValueText);
                        break;

                    case InterpolationSyntax interpolation:
                        var value = EvaluateStringLikeExpression(interpolation.Expression);
                        if (value != null)
                            parts.Add(value);
                        break;
                }
            }

            return string.Concat(parts);
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