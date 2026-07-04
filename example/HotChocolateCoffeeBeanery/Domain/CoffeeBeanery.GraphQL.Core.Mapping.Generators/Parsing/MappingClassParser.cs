using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using CoffeeBeanery.GraphQL.Core.Mapping.Generators;
using CoffeeBeanery.GraphQL.Core.Mapping.Generators.Model;

namespace CoffeeBeanery.GraphQL.Core.Mapping.Generators.Parsing
{
    public static class MappingClassParser
    {
        public static MappingClassInfo Parse(
            INamedTypeSymbol classSymbol,
            INamedTypeSymbol modelType,
            MethodDeclarationSyntax buildMap,
            SemanticModel semanticModel,
            CancellationToken ct)
        {
            var info = new MappingClassInfo
            {
                ClassSymbol = classSymbol,
                ModelType = modelType,

                // FIX: every class the generator's predicate matches derives from
                // BaseModelMappingRegistration<T> (TryGetMappingClass only accepts that base
                // type by name) - it is unconditionally a model. Previously this was left at
                // its default (false) and nothing else in this parser ever set it, even when
                // BuildMap() explicitly wrote `map.IsModel = true;` - that's a plain
                // AssignmentExpressionSyntax statement, which the switch below silently
                // ignores (`case ExpressionStatementSyntax { Expression:
                // AssignmentExpressionSyntax }: break;`). The practical effect: NodeTreeEmitter
                // .EmitModelNodeTree's `if (!info.IsModel) return;` guard always fired, and a
                // mapping like CustomerCustomerEdge never got a ModelNodeTree written into
                // NodeRegistry.ModelTrees at all, despite BuildMap() saying it should.
                IsModel = true
            };

            if (buildMap.Body is null)
                return info;

            foreach (var statement in buildMap.Body.Statements)
            {
                ct.ThrowIfCancellationRequested();

                switch (statement)
                {
                    case ExpressionStatementSyntax { Expression: InvocationExpressionSyntax invocation }:
                        ParseInvocation(invocation, info, semanticModel);
                        break;

                    case ExpressionStatementSyntax { Expression: AssignmentExpressionSyntax assign }:
                        ParseAssignment(assign, info);
                        break;

                    case LocalDeclarationStatementSyntax local:
                        ParseLocalDeclaration(local, info);
                        break;

                    case ReturnStatementSyntax:
                        break;

                    default:
                        info.Diagnostics.Add(Diagnostic.Create(
                            MappingDiagnostics.InvalidBuildMapShape,
                            statement.GetLocation(),
                            classSymbol.Name,
                            statement.ToString().Trim()));
                        break;
                }
            }

            if (info.ModelToEntityTypes.Count == 1)
            {
                info.EntityType = info.ModelToEntityTypes[0];
                info.IsEntity = true;
            }

            return info;
        }
        
        private static void ParseLocalDeclaration(
            LocalDeclarationStatementSyntax local,
            MappingClassInfo info)
        {
            foreach (var variable in local.Declaration.Variables)
            {
                if (variable.Initializer?.Value is not ObjectCreationExpressionSyntax { Initializer: not null } creation)
                    continue;

                // Don't filter by type name — BuildMap uses `var map = new NodeMap { ... }`
                // so the declared type is `var`, not `NodeMap`.
                foreach (var expr in creation.Initializer.Expressions.OfType<AssignmentExpressionSyntax>())
                {
                    var propName = (expr.Left as IdentifierNameSyntax)?.Identifier.Text;
                    var value = EvaluateStringLikeExpression(expr.Right);
                    if (propName is null || value is null) continue;

                    switch (propName)
                    {
                        case "Schema":
                            info.Schema = value;
                            break;
                        case "Prefix":
                            info.Prefix = value;
                            break;
                    }
                }
            }
        }
        
        private static void ParseAssignment(
            AssignmentExpressionSyntax assign,
            MappingClassInfo info)
        {
            if (assign.Left is not MemberAccessExpressionSyntax { Name.Identifier.Text: var propName })
                return;

            var value = EvaluateStringLikeExpression(assign.Right);
            if (value is null) return;

            switch (propName)
            {
                case "Schema":
                    info.Schema = value;
                    break;
                case "Prefix":
                    info.Prefix = value;
                    break;
            }
        }

        private static void ParseInvocation(
            InvocationExpressionSyntax invocation,
            MappingClassInfo info,
            SemanticModel semanticModel)
        {
            if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
                return;

            var memberName = memberAccess.Name is GenericNameSyntax generic
                ? generic.Identifier.Text
                : memberAccess.Name.Identifier.Text;

            switch (memberName)
            {
                case "AddModelToEntity":
                    ParseAddModelToEntity(invocation, memberAccess, info, semanticModel);
                    return;

                case "Add" when memberAccess.Expression is MemberAccessExpressionSyntax { Name.Identifier.Text: "FieldMaps" }:
                    ParseFieldMapAdd(invocation, info);
                    return;

                case "AddRange" when memberAccess.Expression is MemberAccessExpressionSyntax { Name.Identifier.Text: "FieldMaps" }:
                    ParseFieldMapAddRange(invocation, info);
                    return;

                case "Add" when memberAccess.Expression is MemberAccessExpressionSyntax { Name.Identifier.Text: "ExcludedFieldMappings" }:
                    ParseExcludedFieldMapAdd(invocation, info);
                    return;

                case "Add" when memberAccess.Expression is MemberAccessExpressionSyntax { Name.Identifier.Text: "ModelChildren" }:
                    ParseModelChildAdd(invocation, info);
                    return;

                case "Add" when memberAccess.Expression is MemberAccessExpressionSyntax { Name.Identifier.Text: "UpsertKeys" }:
                case "Add" when memberAccess.Expression is MemberAccessExpressionSyntax { Name.Identifier.Text: "EntityChildren" }:
                case "Add" when memberAccess.Expression is MemberAccessExpressionSyntax { Name.Identifier.Text: "EntityChildrenRelated" }:
                    return;

                default:
                    info.Diagnostics.Add(Diagnostic.Create(
                        MappingDiagnostics.InvalidBuildMapShape,
                        invocation.GetLocation(),
                        info.ClassSymbol.Name,
                        invocation.ToString()));
                    return;
            }
        }

        private static void ParseAddModelToEntity(
            InvocationExpressionSyntax invocation,
            MemberAccessExpressionSyntax memberAccess,
            MappingClassInfo info,
            SemanticModel semanticModel)
        {
            if (memberAccess.Name is not GenericNameSyntax { TypeArgumentList.Arguments: { Count: 2 } typeArgs })
                return;

            if (semanticModel.GetTypeInfo(typeArgs[1]).Type is not INamedTypeSymbol entityTypeSymbol)
                return;

            info.ModelToEntityTypes.Add(entityTypeSymbol);

            var args = invocation.ArgumentList.Arguments;

            // fk (TModel -> key), pk (TEntity -> key), alias (TModel -> object?, optional, 3rd positional or named "alias")
            var modelKeyProp = args.Count >= 1 ? ExtractLambdaMemberName(args[0].Expression) : null;
            var entityKeyProp = args.Count >= 2 ? ExtractLambdaMemberName(args[1].Expression) : null;

            var aliasArg = args
                               .Where(a => a.NameColon?.Name.Identifier.Text == "alias")
                               .Select(a => (ArgumentSyntax?)a)
                               .FirstOrDefault()
                           ?? (args.Count >= 3 && args[2].NameColon is null ? args[2] : null);

            var aliasProperty = aliasArg is not null ? ExtractLambdaMemberName(aliasArg.Expression) : null;

            info.ModelToEntity.Add(new EntityKeyInfo
            {
                EntityType = entityTypeSymbol,
                To = entityTypeSymbol.Name,
                ToColumn = entityKeyProp,
                FromColumn = modelKeyProp,
                AliasProperty = aliasProperty   // null/empty for primary links, real alias name otherwise
            });
        }

        private static string? ExtractLambdaMemberName(ExpressionSyntax expr)
        {
            if (expr is not SimpleLambdaExpressionSyntax { Body: MemberAccessExpressionSyntax memberAccess })
                return null;

            return memberAccess.Name.Identifier.Text;
        }

        private static void ParseFieldMapAdd(InvocationExpressionSyntax invocation, MappingClassInfo info)
        {
            var arg = invocation.ArgumentList.Arguments.FirstOrDefault()?.Expression;
            if (arg is ObjectCreationExpressionSyntax creation)
                TryParseFieldMapCreation(creation, info);
        }

        private static void ParseFieldMapAddRange(InvocationExpressionSyntax invocation, MappingClassInfo info)
        {
            var arg = invocation.ArgumentList.Arguments.FirstOrDefault()?.Expression;

            SeparatedSyntaxList<ExpressionSyntax>? maybeElements = arg switch
            {
                ArrayCreationExpressionSyntax { Initializer: { } init } => init.Expressions,
                ImplicitArrayCreationExpressionSyntax { Initializer: { } init } => init.Expressions,
                InitializerExpressionSyntax init => init.Expressions,
                _ => (SeparatedSyntaxList<ExpressionSyntax>?)null
            };

            if (maybeElements is not { } elements)
                return;

            foreach (var element in elements)
            {
                if (element is ObjectCreationExpressionSyntax creation)
                    TryParseFieldMapCreation(creation, info);
            }
        }

        private static void TryParseFieldMapCreation(ObjectCreationExpressionSyntax creation, MappingClassInfo info)
        {
            if (creation.Initializer is null)
                return;

            string? sourceName = null, destEntity = null, destName = null;

            foreach (var assign in creation.Initializer.Expressions.OfType<AssignmentExpressionSyntax>())
            {
                var propName = (assign.Left as IdentifierNameSyntax)?.Identifier.Text;
                switch (propName)
                {
                    case "SourceName":
                        sourceName = EvaluateStringLikeExpression(assign.Right);
                        break;
                    case "DestinationEntity":
                        destEntity = EvaluateStringLikeExpression(assign.Right);
                        break;
                    case "DestinationName":
                        destName = EvaluateStringLikeExpression(assign.Right);
                        break;
                }
            }

            if (sourceName is null || destEntity is null || destName is null)
            {
                info.Diagnostics.Add(Diagnostic.Create(
                    MappingDiagnostics.InvalidBuildMapShape,
                    creation.GetLocation(),
                    info.ClassSymbol.Name,
                    "FieldMap initializer missing SourceName/DestinationEntity/DestinationName"));
                return;
            }

            info.ManualFieldMaps.Add(new FieldMapInfo
            {
                SourceName = sourceName,
                DestinationEntity = destEntity,
                DestinationName = destName
            });
        }

        private static void ParseExcludedFieldMapAdd(InvocationExpressionSyntax invocation, MappingClassInfo info)
        {
            var arg = invocation.ArgumentList.Arguments.FirstOrDefault()?.Expression;
            if (arg is not ObjectCreationExpressionSyntax { Initializer: not null } creation)
                return;

            string? sourceName = null, destEntity = null;
            foreach (var assign in creation.Initializer.Expressions.OfType<AssignmentExpressionSyntax>())
            {
                var propName = (assign.Left as IdentifierNameSyntax)?.Identifier.Text;
                if (propName == "SourceName")
                    sourceName = EvaluateStringLikeExpression(assign.Right);
                else if (propName == "DestinationEntity")
                    destEntity = EvaluateStringLikeExpression(assign.Right);
            }

            if (sourceName is not null && destEntity is not null)
            {
                info.ExcludedFieldMappings.Add(new ExcludedFieldMappingInfo
                {
                    SourceName = sourceName,
                    DestinationEntity = destEntity
                });
            }
        }

        // map.ModelChildren.Add(new ModelKey { To = nameof(SomeType) })
        private static void ParseModelChildAdd(InvocationExpressionSyntax invocation, MappingClassInfo info)
        {
            var arg = invocation.ArgumentList.Arguments.FirstOrDefault()?.Expression;
            if (arg is not ObjectCreationExpressionSyntax { Initializer: not null } creation)
                return;

            foreach (var assign in creation.Initializer.Expressions.OfType<AssignmentExpressionSyntax>())
            {
                if ((assign.Left as IdentifierNameSyntax)?.Identifier.Text != "To")
                    continue;

                var value = EvaluateStringLikeExpression(assign.Right);
                if (value is not null)
                    info.ModelChildren.Add(new ModelChildInfo { To = value });
            }
        }

        private static string? EvaluateStringLikeExpression(ExpressionSyntax expr)
        {
            switch (expr)
            {
                case InvocationExpressionSyntax { Expression: IdentifierNameSyntax { Identifier.Text: "nameof" } } nameofInvocation:
                    var nameofArg = nameofInvocation.ArgumentList.Arguments.FirstOrDefault()?.Expression;
                    return nameofArg switch
                    {
                        MemberAccessExpressionSyntax ma => ma.Name.Identifier.Text,
                        IdentifierNameSyntax id => id.Identifier.Text,
                        _ => null
                    };

                case LiteralExpressionSyntax literal:
                    return literal.Token.ValueText;

                default:
                    return null;
            }
        }
    }
}