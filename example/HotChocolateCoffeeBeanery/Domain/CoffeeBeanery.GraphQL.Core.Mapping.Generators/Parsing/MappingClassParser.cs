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

                    case ExpressionStatementSyntax { Expression: AssignmentExpressionSyntax }:
                        // Plain property assignments on `map` (e.g. `map.ModelName = ...;`,
                        // `map.IsModel = true;`, `map.Schema = ...;`) carry no structural
                        // information the generator needs beyond what's already inferred
                        // below from ModelToEntityTypes/AddModelToEntity calls - IsModel is
                        // unconditionally true for this base class (set above), and IsEntity/
                        // EntityType are derived from AddModelToEntity's link count, not from
                        // any assignment statement. Intentionally still a no-op here.
                        break;

                    case LocalDeclarationStatementSyntax:
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

            // FIX: info.EntityType/IsEntity were never set anywhere in this parser, even
            // though ParseAddModelToEntity already captures the entity type symbol into
            // info.ModelToEntityTypes for every `map.AddModelToEntity<TModel, TEntity>(...)`
            // call found above. NodeTreeEmitter.EmitCoffeeBeanery.GraphQL.Core.Sql.EntityNodeTree gates on
            // `info.IsEntity && info.EntityType is not null` - without this inference, no
            // mapping using BaseModelMappingRegistration<T> could ever produce an
            // CoffeeBeanery.GraphQL.Core.Sql.EntityNodeTree, regardless of how many AddModelToEntity links it had.
            //
            // Rule (matches the project's own convention, mirrored from
            // BaseMappingRegistration<TModel,TEntity> which hardcodes IsEntity = true for the
            // single-entity case): exactly ONE AddModelToEntity link means this model has a
            // real single backing entity (e.g. CustomerCustomerEdge -> CustomerCustomerRelationship)
            // and should get both a ModelNodeTree and an CoffeeBeanery.GraphQL.Core.Sql.EntityNodeTree. Zero or multiple links
            // means a genuine model-only or multi-entity aggregate (e.g. Product, GraphModel) -
            // IsEntity stays false, EntityType stays null, and only a ModelNodeTree is emitted.
            if (info.ModelToEntityTypes.Count == 1)
            {
                info.EntityType = info.ModelToEntityTypes[0];
                info.IsEntity = true;
            }

            return info;
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