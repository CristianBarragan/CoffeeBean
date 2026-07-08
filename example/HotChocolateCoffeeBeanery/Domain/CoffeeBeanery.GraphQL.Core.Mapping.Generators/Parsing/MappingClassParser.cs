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

                case "UpsertKeys":
                    ParseUpsertKeys(
                        assignment.Right,
                        info);
                    break;

                case "Fields":
                    ParseFields(
                        assignment.Right,
                        info);
                    break;

                case "Graph":
                    ParseGraph(
                        assignment.Right,
                        info);
                    break;
            }
        }

        // Nothing above sets the singular EntityType — only ModelToEntity gets
        // populated (via ParseEntities). Several downstream emitters (PlannerEmitter,
        // MetadataEmitter, MaterializerEmitter) still read info.EntityType directly
        // for simple/non-composite models, and were throwing/crashing because it was
        // always null. Derive it here from whichever link is marked primary so this
        // is fixed once, at the source, instead of needing the same patch applied
        // separately to every downstream consumer.
        info.EntityType ??=
            info.ModelToEntity
                .FirstOrDefault(k => k.IsPrimary)
                ?.EntityType;

        return info;
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
                info.ModelToEntity.Add(entity);
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
        MappingClassInfo info)
    {
        foreach (var element in GetCollectionElements(expression))
        {
            var initializer = GetObjectInitializer(element);

            if (initializer == null)
                continue;

            var field = new FieldInfo();

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
                                assignment.Right);
                        break;

                    case "Destination":
                        field.DestinationName =
                            EvaluateStringLikeExpression(
                                assignment.Right);
                        break;

                    case "Entity":
                        field.DestinationEntity =
                            EvaluateTypeName(
                                assignment.Right);
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


        // NOTE: interpolated strings (e.g. `$"{nameof(X)}{nameof(Y)}"`, used for
        // vertex aliases in graph mapping definitions) are not handled here and
        // will silently return null. If any Definition uses an interpolated
        // string for a string-valued property, that value will be dropped.
        // Add an InterpolatedStringExpressionSyntax case here if that's in use.

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