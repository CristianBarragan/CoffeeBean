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
        
        info.IsComposite = info.ModelToEntityList.Count > 1
                           || info.Definition.Entities.Count > 1;
        
        info.EntityType ??=
            info.Definition.Entities
                .FirstOrDefault(k => k.IsPrimary)
                ?.EntityType;

        return info;
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


            var entity = new EntityKeyInfo
            {
                EntityType = null!
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
                            assignment.Right.ToString()
                                .Equals(
                                    "true",
                                    StringComparison.OrdinalIgnoreCase);
                        break;
                }
            }


            if (entity.EntityType != null)
            {
                info.Definition.Entities.Add(new EntityDefinitionInfo
                {
                    EntityType = entity.EntityType,
                    FromColumn = entity.FromColumn,
                    ToColumn = entity.ToColumn,
                    AliasProperty = entity.AliasProperty,
                    IsPrimary = entity.IsPrimary
                });
            }
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

            foreach (var assignment in initializer.Expressions
                         .OfType<AssignmentExpressionSyntax>())
            {
                var name = (assignment.Left as IdentifierNameSyntax)?.Identifier.Text;
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
                }
            }

            if (!string.IsNullOrWhiteSpace(field.SourceName) &&
                !string.IsNullOrWhiteSpace(field.DestinationEntity) &&
                !string.IsNullOrWhiteSpace(field.DestinationName))
                info.FieldMaps.Add(field);
        }
    }

    private static void ParseEnumMapping(
        ExpressionSyntax expression,
        FieldInfo field,
        SemanticModel semanticModel)
    {
        var typeInfo = semanticModel.GetTypeInfo(expression);
        if (typeInfo.Type is not INamedTypeSymbol { IsGenericType: true } genericType) return;
        if (genericType.TypeArguments.Length < 2) return;

        var modelEnum  = genericType.TypeArguments[0] as INamedTypeSymbol;
        var entityEnum = genericType.TypeArguments[1] as INamedTypeSymbol;

        if (modelEnum?.TypeKind  != TypeKind.Enum) return;
        if (entityEnum?.TypeKind != TypeKind.Enum) return;

        field.FromEnum = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var m in modelEnum.GetMembers().OfType<IFieldSymbol>()
                     .Where(f => f.IsConst && f.HasConstantValue))
            field.FromEnum[m.Name] = Convert.ToInt32(m.ConstantValue);

        field.ToEnum = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var m in entityEnum.GetMembers().OfType<IFieldSymbol>()
                     .Where(f => f.IsConst && f.HasConstantValue))
            field.ToEnum[m.Name] = Convert.ToInt32(m.ConstantValue);
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


    private static VertexInfo? ParseVertex(
        ExpressionSyntax expression)
    {
        var initializer = GetObjectInitializer(expression);

        if (initializer == null)
            return null;


        var vertex = new VertexInfo();


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
                            assignment.Right) ?? "";
                    break;

                case "KeyColumn":
                    vertex.KeyColumn =
                        EvaluateStringLikeExpression(
                            assignment.Right) ?? "";
                    break;

                case "Alias":
                    vertex.Alias =
                        EvaluateStringLikeExpression(
                            assignment.Right);
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