// using System.Collections.Generic;
// using System.Linq;
// using System.Threading;
// using Microsoft.CodeAnalysis;
// using Microsoft.CodeAnalysis.CSharp.Syntax;
// using CoffeeBeanery.GraphQL.Core.Mapping.Generators;
// using CoffeeBeanery.GraphQL.Core.Mapping.Generators.Model;
// using CoffeeBeanery.GraphQL.Core.Mapping.Generators.Model.CoffeeBeanery.GraphQL.Core.Mapping.Generators.Model;
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
//     /// caller (MappingFieldMapGenerator) is responsible for detecting that
//     /// call and merging in a separate Parse() pass over the base class.
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
//         /// <summary>
//         /// Emits a generated static class containing:
//         ///   1. GetStaticMap() - the Func&lt;Entity1,...,EntityN,Model&gt; for Dapper's multi-mapping QueryAsync overload.
//         ///   2. SplitOnDapper - a Dictionary&lt;string,Type&gt; keyed "EntityName~KeyColumn" -> EntityType,
//         ///      in the exact shape ProcessQuery&lt;M&gt; expects on _SqlCompilationContext.SplitOnDapper.
//         ///   3. MapRows(...) - takes the raw row matrix + ordered types ProcessQuery already produces
//         ///      and returns List&lt;Model&gt;, so ProcessQuery&lt;M&gt;.MappingConfiguration can just delegate to it.
//         /// </summary>
//         public static string EmitDapperRouter(MappingClassInfo info)
//         {
//             var modelName = info.ModelType.ToDisplayString();
//             var modelSimpleName = info.ModelType.Name;
//
//             var bindings = info.ModelToEntityBindings.Count > 0
//                 ? info.ModelToEntityBindings
//                 : info.ModelToEntityTypes.Select(t => new ModelToEntityBinding { EntityType = t }).ToList();
//
//             var entityTypes = bindings.Select(b => b.EntityType.ToDisplayString()).ToList();
//             var typeArgumentsStr = string.Join(", ", entityTypes) + $", {modelName}";
//             var lambdaArgs = string.Join(", ", entityTypes.Select((_, i) => $"e{i}"));
//
//             var splitOnEntries = bindings.Select(b =>
//             {
//                 var entityName = b.EntityType.Name;
//                 var keyColumn = b.EntityKeyPropertyName ?? b.ModelKeyPropertyName ?? "Id";
//                 return $$"""        ["{{entityName}}~{{keyColumn}}"] = typeof({{b.EntityType.ToDisplayString()}}),""";
//             });
//             var splitOnDictBody = string.Join("\n", splitOnEntries);
//
//             return $$"""
//                      // <auto-generated />
//                      using System;
//                      using System.Collections.Generic;
//                      using Domain.Model;
//                      using Database.Entity;
//
//                      namespace CoffeeBeanery.Generated
//                      {
//                          public static class {{modelSimpleName}}DapperRouter
//                          {
//                              // A statically bound, zero-allocation reference method matching Dapper's map constraint
//                              public static Func<{{typeArgumentsStr}}> GetStaticMap()
//                              {
//                                  return ({{lambdaArgs}}) =>
//                                  {
//                                      var model = new {{modelName}}();
//
//                                      // The generator explicitly outputs property assignment bytecodes here
//                                      // based on your parsed NodeMap rules:
//                                      {{GeneratePropertyAssignments(info)}}
//
//                                      return model;
//                                  };
//                              }
//
//                              // Matches the shape _SqlCompilationContext.SplitOnDapper / ProcessQuery<M> expects:
//                              // key is "EntityName~KeyColumn", value is the CLR type to split that block into.
//                              public static readonly Dictionary<string, Type> SplitOnDapper = new()
//                              {
//                      {{splitOnDictBody}}
//                              };
//
//                              // Drop-in body for ProcessQuery<{{modelSimpleName}}>.MappingConfiguration.
//                              // ProcessQuery already produces rowMatrix (List<object[]>) and types (List<Type>)
//                              // in the same order as SplitOnDapper above; this just re-hydrates each row
//                              // through GetStaticMap() instead of leaving MappingConfiguration unimplemented.
//                              public static List<{{modelName}}> MapRows(List<object[]> rowMatrix)
//                              {
//                                  var map = GetStaticMap();
//                                  var results = new List<{{modelName}}>(rowMatrix.Count);
//
//                                  foreach (var row in rowMatrix)
//                                  {
//                                      // row[i] holds the already-split entity instance for bindings[i],
//                                      // in the same order GetStaticMap()'s positional args expect.
//                                      var typedArgs = new object[row.Length];
//                                      Array.Copy(row, typedArgs, row.Length);
//
//                                      var model = ({{modelName}})map.DynamicInvoke(typedArgs)!;
//                                      results.Add(model);
//                                  }
//
//                                  return results;
//                              }
//                          }
//                      }
//                      """;
//         }
//
//         // map.AddModelToEntity<Product, DataEntity.Contract>(x => x.ContractKey, x => x.ContractKey)
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
//             info.ManualFieldMaps.Add(new FieldMapInfo
//             {
//                 SourceName = sourceName,
//                 DestinationEntity = destEntity,
//                 DestinationName = destName
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
//                     info.ManualModelChildren.Add(new ModelChildInfo { To = value });
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
//
//         /// <summary>
//         /// TODO: not implemented in the source you've shown me. Plug in real property-assignment
//         /// codegen here based on info.ManualFieldMaps + name-matched properties between
//         /// info.ModelType and each binding's EntityType. Until then this emits a comment only,
//         /// so GetStaticMap() compiles but returns a model with default values.
//         /// </summary>
//         private static string GeneratePropertyAssignments(MappingClassInfo info)
//         {
//             return "// TODO: GeneratePropertyAssignments not implemented - model properties are currently unassigned.";
//         }
//     }
// }