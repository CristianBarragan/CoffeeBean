// using System.Linq;
// using System.Threading;
// using Microsoft.CodeAnalysis;
// using Microsoft.CodeAnalysis.CSharp.Syntax;
//
// namespace CoffeeBeanery.GraphQL.Core.Mapping
// {
//     /// <summary>
//     /// Statically interprets a BuildMap() method body to learn three things:
//     /// which entity types it references via AddModelToEntity&lt;,&gt;(...) (the
//     /// candidate set for FieldMapGeneration), which (SourceName, DestinationEntity)
//     /// pairs already have a manual FieldMap (for dedup), and which
//     /// ExcludedFieldMappings/ModelChildren are already declared (also for dedup).
//     ///
//     /// This is intentionally narrow - it never needs to *execute* BuildMap() or
//     /// reconstruct the full NodeMap, since the hand-written BuildMap() keeps
//     /// running unchanged at runtime. The generator only needs to know what's
//     /// already there so it doesn't duplicate it.
//     ///
//     /// IMPORTANT: this only reads the ONE method body it's pointed at. If that
//     /// body starts with `var map = base.BuildMap();`, any manual FieldMaps
//     /// declared in the base class's own BuildMap() are invisible here - the
//     /// caller is responsible for detecting that call and merging in a separate
//     /// Parse() pass over the base class.
//     ///
//     /// KNOWN GAP: ParseAddModelToEntity below only captures EntityType plus the
//     /// two key-selector lambdas. It does NOT capture isPrimary/alias arguments
//     /// (e.g. `AddModelToEntity<Customer, DataEntity.Customer>(m => m.CustomerKey,
//     /// e => e.CustomerKey, isPrimary: true)`), and it writes into
//     /// ModelToEntityTypes/ModelToEntityBindings rather than info.ModelToEntity -
//     /// the property PlannerEmitter/FieldMapGeneration actually read (whose link
//     /// objects carry IsPrimary/EntityType/AliasProperty/FromColumn). Until
//     /// ModelToEntity is populated by this or another pass, entity-candidate
//     /// resolution in FieldMapGeneration/PlannerEmitter will still come up empty
//     /// even with this parser wired in. FieldMaps/ExcludedFieldMappings/
//     /// ModelChildren parsing below is unaffected by this gap and is safe to use.
//     /// </summary>
//     public static class MappingClassParser
//     {
//         public static MappingClassInfo Parse(
//             INamedTypeSymbol classSymbol,
//             INamedTypeSymbol modelType,
//             MethodDeclarationSyntax buildMap,
//             SemanticModel semanticModel,
//             CancellationToken ct)
//         {
//             var info = new MappingClassInfo
//             {
//                 ClassSymbol = classSymbol,
//                 ModelType = modelType
//             };
//
//             if (buildMap.Body is null)
//                 return info;
//
//             foreach (var statement in buildMap.Body.Statements)
//             {
//                 ct.ThrowIfCancellationRequested();
//
//                 switch (statement)
//                 {
//                     case ExpressionStatementSyntax { Expression: InvocationExpressionSyntax invocation }:
//                         ParseInvocation(invocation, info, semanticModel);
//                         break;
//
//                     case ExpressionStatementSyntax { Expression: AssignmentExpressionSyntax }:
//                         break; // e.g. `map.ModelName = ...`, `map.GraphMap = new GraphMap{...}`,
//                                // `map.PrimaryKey = ...` - structural property assignment, nothing to learn
//
//                     case LocalDeclarationStatementSyntax:
//                     case ReturnStatementSyntax:
//                         break; // `var map = new NodeMap{...}` / `return map;` - nothing to learn here
//
//                     default:
//                         info.Diagnostics.Add(Diagnostic.Create(
//                             MappingDiagnostics.InvalidBuildMapShape,
//                             statement.GetLocation(),
//                             classSymbol.Name,
//                             statement.ToString().Trim()));
//                         break;
//                 }
//             }
//
//             return info;
//         }
//
//         private static void ParseInvocation(
//             InvocationExpressionSyntax invocation,
//             MappingClassInfo info,
//             SemanticModel semanticModel)
//         {
//             if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
//                 return;
//
//             var memberName = memberAccess.Name is GenericNameSyntax generic
//                 ? generic.Identifier.Text
//                 : memberAccess.Name.Identifier.Text;
//
//             switch (memberName)
//             {
//                 case "AddModelToEntity":
//                     ParseAddModelToEntity(invocation, memberAccess, info, semanticModel);
//                     return;
//
//                 case "Add" when memberAccess.Expression is MemberAccessExpressionSyntax { Name.Identifier.Text: "FieldMaps" }:
//                     ParseFieldMapAdd(invocation, info);
//                     return;
//
//                 case "AddRange" when memberAccess.Expression is MemberAccessExpressionSyntax { Name.Identifier.Text: "FieldMaps" }:
//                     ParseFieldMapAddRange(invocation, info);
//                     return;
//
//                 case "Add" when memberAccess.Expression is MemberAccessExpressionSyntax { Name.Identifier.Text: "ExcludedFieldMappings" }:
//                     ParseExcludedFieldMapAdd(invocation, info);
//                     return;
//
//                 case "Add" when memberAccess.Expression is MemberAccessExpressionSyntax { Name.Identifier.Text: "ModelChildren" }:
//                     ParseModelChildAdd(invocation, info);
//                     return;
//
//                 case "Add" when memberAccess.Expression is MemberAccessExpressionSyntax { Name.Identifier.Text: "UpsertKeys" }:
//                 case "Add" when memberAccess.Expression is MemberAccessExpressionSyntax { Name.Identifier.Text: "EntityChildren" }:
//                 case "Add" when memberAccess.Expression is MemberAccessExpressionSyntax { Name.Identifier.Text: "EntityChildrenRelated" }:
//                     return; // entity-side wiring, not relevant to the Model-side dedup passes - leave to hand-written BuildMap()
//
//                 default:
//                     info.Diagnostics.Add(Diagnostic.Create(
//                         MappingDiagnostics.InvalidBuildMapShape,
//                         invocation.GetLocation(),
//                         info.ClassSymbol.Name,
//                         invocation.ToString()));
//                     return;
//             }
//         }
//
//         // map.AddModelToEntity<Product, DataEntity.Contract>(x => x.ContractKey, x => x.ContractKey)
//         //
//         // NOTE: does not currently capture isPrimary:/alias: named arguments, and writes into
//         // ModelToEntityTypes/ModelToEntityBindings rather than info.ModelToEntity. See the
//         // KNOWN GAP note on the class doc comment above before relying on this for anything
//         // that needs IsPrimary/AliasProperty/FromColumn (composite/self-join query planning).
//         private static void ParseAddModelToEntity(
//             InvocationExpressionSyntax invocation,
//             MemberAccessExpressionSyntax memberAccess,
//             MappingClassInfo info,
//             SemanticModel semanticModel)
//         {
//             if (memberAccess.Name is not GenericNameSyntax { TypeArgumentList.Arguments: { Count: 2 } typeArgs })
//                 return;
//
//             if (semanticModel.GetTypeInfo(typeArgs[1]).Type is not INamedTypeSymbol entityTypeSymbol)
//                 return;
//
//             info.ModelToEntityTypes.Add(entityTypeSymbol);
//
//             var binding = new ModelToEntityBinding { EntityType = entityTypeSymbol };
//
//             var args = invocation.ArgumentList.Arguments;
//             if (args.Count >= 1)
//                 binding.ModelKeyPropertyName = ExtractLambdaMemberName(args[0].Expression);
//             if (args.Count >= 2)
//                 binding.EntityKeyPropertyName = ExtractLambdaMemberName(args[1].Expression);
//
//             info.ModelToEntityBindings.Add(binding);
//         }
//
//         /// <summary>Pulls the property name out of a simple `x => x.Prop` lambda. Returns null
//         /// for any other shape (chained access, method calls, etc.) - callers fall back accordingly.</summary>
//         private static string? ExtractLambdaMemberName(ExpressionSyntax expr)
//         {
//             if (expr is not SimpleLambdaExpressionSyntax { Body: MemberAccessExpressionSyntax memberAccess })
//                 return null;
//
//             return memberAccess.Name.Identifier.Text;
//         }
//
//         // map.FieldMaps.Add(new FieldMap { SourceName = ..., DestinationEntity = ..., DestinationName = ..., [FromEnum/ToEnum] })
//         private static void ParseFieldMapAdd(InvocationExpressionSyntax invocation, MappingClassInfo info)
//         {
//             var arg = invocation.ArgumentList.Arguments.FirstOrDefault()?.Expression;
//             if (arg is ObjectCreationExpressionSyntax creation)
//                 TryParseFieldMapCreation(creation, info);
//         }
//
//         // map.FieldMaps.AddRange(new[] { new FieldMap {...}, new FieldMap {...} })
//         private static void ParseFieldMapAddRange(InvocationExpressionSyntax invocation, MappingClassInfo info)
//         {
//             var arg = invocation.ArgumentList.Arguments.FirstOrDefault()?.Expression;
//
//             SeparatedSyntaxList<ExpressionSyntax>? maybeElements = arg switch
//             {
//                 ArrayCreationExpressionSyntax { Initializer: { } init } => init.Expressions,
//                 ImplicitArrayCreationExpressionSyntax { Initializer: { } init } => init.Expressions,
//                 InitializerExpressionSyntax init => init.Expressions,
//                 _ => (SeparatedSyntaxList<ExpressionSyntax>?)null
//             };
//
//             if (maybeElements is not { } elements)
//                 return;
//
//             foreach (var element in elements)
//             {
//                 if (element is ObjectCreationExpressionSyntax creation)
//                     TryParseFieldMapCreation(creation, info);
//             }
//         }
//
//         private static void TryParseFieldMapCreation(ObjectCreationExpressionSyntax creation, MappingClassInfo info)
//         {
//             if (creation.Initializer is null)
//                 return;
//
//             string? sourceName = null, destEntity = null, destName = null;
//
//             foreach (var assign in creation.Initializer.Expressions.OfType<AssignmentExpressionSyntax>())
//             {
//                 var propName = (assign.Left as IdentifierNameSyntax)?.Identifier.Text;
//                 switch (propName)
//                 {
//                     case "SourceName":
//                         sourceName = EvaluateStringLikeExpression(assign.Right);
//                         break;
//                     case "DestinationEntity":
//                         destEntity = EvaluateStringLikeExpression(assign.Right);
//                         break;
//                     case "DestinationName":
//                         destName = EvaluateStringLikeExpression(assign.Right);
//                         break;
//                     // FromEnum/ToEnum intentionally not parsed - manual FieldMaps are never
//                     // re-emitted, only used for dedup keyed on (SourceName, DestinationEntity).
//                 }
//             }
//
//             if (sourceName is null || destEntity is null || destName is null)
//             {
//                 info.Diagnostics.Add(Diagnostic.Create(
//                     MappingDiagnostics.InvalidBuildMapShape,
//                     creation.GetLocation(),
//                     info.ClassSymbol.Name,
//                     "FieldMap initializer missing SourceName/DestinationEntity/DestinationName"));
//                 return;
//             }
//
//             // Written straight to info.FieldMaps (not a separate ManualFieldMaps staging
//             // list) since FieldMapGeneration.ApplyCore reads info.FieldMaps directly for
//             // both dedup (HasAnyFieldMap) and downstream consumption by the emitters.
//             info.FieldMaps.Add(new FieldMapInfo
//             {
//                 SourceName = sourceName,
//                 DestinationEntity = destEntity,
//                 DestinationName = destName,
//                 IsGenerated = false
//             });
//         }
//
//         private static void ParseExcludedFieldMapAdd(InvocationExpressionSyntax invocation, MappingClassInfo info)
//         {
//             var arg = invocation.ArgumentList.Arguments.FirstOrDefault()?.Expression;
//             if (arg is not ObjectCreationExpressionSyntax { Initializer: not null } creation)
//                 return;
//
//             string? sourceName = null, destEntity = null;
//             foreach (var assign in creation.Initializer.Expressions.OfType<AssignmentExpressionSyntax>())
//             {
//                 var propName = (assign.Left as IdentifierNameSyntax)?.Identifier.Text;
//                 if (propName == "SourceName")
//                     sourceName = EvaluateStringLikeExpression(assign.Right);
//                 else if (propName == "DestinationEntity")
//                     destEntity = EvaluateStringLikeExpression(assign.Right);
//             }
//
//             if (sourceName is not null && destEntity is not null)
//             {
//                 info.ExcludedFieldMappings.Add(new ExcludedFieldMappingInfo
//                 {
//                     SourceName = sourceName,
//                     DestinationEntity = destEntity
//                 });
//             }
//         }
//
//         // map.ModelChildren.Add(new ModelKey { To = nameof(SomeType) })
//         private static void ParseModelChildAdd(InvocationExpressionSyntax invocation, MappingClassInfo info)
//         {
//             var arg = invocation.ArgumentList.Arguments.FirstOrDefault()?.Expression;
//             if (arg is not ObjectCreationExpressionSyntax { Initializer: not null } creation)
//                 return;
//
//             foreach (var assign in creation.Initializer.Expressions.OfType<AssignmentExpressionSyntax>())
//             {
//                 if ((assign.Left as IdentifierNameSyntax)?.Identifier.Text != "To")
//                     continue;
//
//                 var value = EvaluateStringLikeExpression(assign.Right);
//                 if (value is not null)
//                     info.ModelChildren.Add(new ModelChildInfo { To = value });
//             }
//         }
//
//         /// <summary>Handles nameof(X) / nameof(X.Y) and plain string literals -
//         /// the only two shapes used for string-valued NodeMap/FieldMap properties today.</summary>
//         private static string? EvaluateStringLikeExpression(ExpressionSyntax expr)
//         {
//             switch (expr)
//             {
//                 case InvocationExpressionSyntax { Expression: IdentifierNameSyntax { Identifier.Text: "nameof" } } nameofInvocation:
//                     var nameofArg = nameofInvocation.ArgumentList.Arguments.FirstOrDefault()?.Expression;
//                     return nameofArg switch
//                     {
//                         MemberAccessExpressionSyntax ma => ma.Name.Identifier.Text,
//                         IdentifierNameSyntax id => id.Identifier.Text,
//                         _ => null
//                     };
//
//                 case LiteralExpressionSyntax literal:
//                     return literal.Token.ValueText;
//
//                 default:
//                     return null;
//             }
//         }
//     }
// }