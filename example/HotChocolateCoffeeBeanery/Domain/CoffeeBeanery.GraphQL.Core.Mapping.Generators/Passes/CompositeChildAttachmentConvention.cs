using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using CoffeeBeanery.GraphQL.Core.Mapping.Generators.Model;

namespace CoffeeBeanery.GraphQL.Core.Mapping.Generators.Passes
{
    /// <summary>
    /// For every object-typed or List&lt;object&gt;-typed property on a Model
    /// (mirroring ModelChildrenInference's own unwrap rules), find the other
    /// mapping class in the compilation whose ModelType matches the property's
    /// element type, and register a ChildAttachment so it can be nested as a
    /// GraphQL field at runtime - even when the related model is composite
    /// (spans multiple unrelated EF entities, e.g. Product) and therefore has
    /// no single EF navigation property for EntityNavigationConvention to find.
    ///
    /// Skips:
    ///   - scalar/enum properties (same rules as ModelChildrenInference)
    ///   - self-references
    ///   - the Wrapper model (root payload container - not a real nesting target)
    ///   - fields already claimed by a hand-written ChildAttachment in BuildMap()
    ///
    /// Join column resolution: convention is "{ParentModel.Name}Key" must
    /// exist on the parent's own primary entity, and some entity in the
    /// related model's ModelToEntity composition must carry that same column
    /// name (e.g. Product's CustomerBankingRelationship link, which has its
    /// own "CustomerKey" column). If neither side matches, the attachment is
    /// still registered as unresolved (ParentJoinColumn left blank);
    /// NodeBuilder.BuildEdges skips unresolved attachments, so they're a
    /// silent no-op rather than a build error - the convention simply
    /// doesn't apply and a hand-written ChildAttachment is required instead.
    /// </summary>
    internal static class CompositeChildAttachmentConvention
    {
        private static readonly HashSet<string> ScalarTypeNames = new()
        {
            "String", "Guid", "DateTime", "DateTimeOffset", "Decimal",
            "Boolean", "Byte", "SByte", "Int16", "UInt16", "Int32", "UInt32",
            "Int64", "UInt64", "Single", "Double", "Char"
        };

        public static void Apply(
            MappingClassInfo info,
            System.Collections.Immutable.ImmutableArray<MappingClassInfo> allMappings)
        {
            if (info.EntityType is null)
                return; // composite/non-entity models don't themselves act as a parent here

            var existingFieldNames = new HashSet<string>(
                info.AutoChildAttachments.Select(a => a.FieldName)
                    .Concat(info.ModelChildren.Select(c => ToGraphQlFieldNameLiteral(c.To))),
                System.StringComparer.OrdinalIgnoreCase);

            var properties = info.ModelType.GetMembers().OfType<IPropertySymbol>()
                .Where(p => p.GetMethod is not null && !p.IsStatic);

            foreach (var prop in properties)
            {
                var elementType = UnwrapCollection(prop.Type);
                var unwrapped = UnwrapNullable(elementType);
                
                System.Diagnostics.Debug.WriteLine($"[ATTACH] {info.ModelType.Name}.{prop.Name}: elementType={elementType?.Name}, unwrapped={unwrapped?.Name}");

                if (unwrapped is not INamedTypeSymbol related)
                    continue;

                if (IsScalar(related))
                    continue;

                if (SymbolEqualityComparer.Default.Equals(related, info.ModelType))
                    continue; // self-reference

                if (related.Name == "Wrapper")
                    continue; // root payload placeholder - never a nesting target

                var fieldName = ToGraphQlFieldNameLiteral(related.Name);

                if (existingFieldNames.Contains(fieldName))
                    continue; // already declared (hand-written or otherwise)

                var childMapping = allMappings.FirstOrDefault(m =>
                    SymbolEqualityComparer.Default.Equals(m.ModelType, related));

                if (childMapping is null || childMapping.EntityType is null)
                    continue; // no registered mapping for this property's type, or it has no entity composition

                var parentJoinColumn = $"{info.ModelType.Name}Key";

                var parentHasColumn = info.EntityType.GetMembers().OfType<IPropertySymbol>()
                    .Any(p => p.Name == parentJoinColumn);

                INamedTypeSymbol? childEntityType = null;

                if (parentHasColumn)
                {
                    foreach (var link in childMapping.ModelToEntity)
                    {
                        var hasFk = link.EntityType.GetMembers().OfType<IPropertySymbol>()
                            .Any(p => p.Name == parentJoinColumn);

                        if (hasFk)
                        {
                            childEntityType = link.EntityType;
                            break; // first match wins - declaration order
                        }
                    }
                }

                info.ModelChildren.Add(new ModelChildInfo { To = related.Name });
                existingFieldNames.Add(fieldName);

                if (childEntityType is null)
                {
                    // Convention didn't resolve a join - register unresolved.
                    // NodeBuilder skips these; a hand-written ChildAttachment
                    // (or a fix to the property/column naming) is required.
                    continue;
                }

                info.AutoChildAttachments.Add(new AutoChildAttachmentInfo
                {
                    FieldName = fieldName,
                    ToModelName = related.Name,
                    ParentEntityType = info.EntityType,
                    ParentJoinColumn = parentJoinColumn,
                    ChildEntityType = childEntityType,
                    ChildJoinColumn = parentJoinColumn
                });
            }
        }

        private static ITypeSymbol UnwrapCollection(ITypeSymbol type)
        {
            if (type.SpecialType == SpecialType.System_String)
                return type;

            if (type is INamedTypeSymbol { IsGenericType: true } named &&
                named.TypeArguments.Length == 1 &&
                named.Name is "List" or "IEnumerable" or "ICollection" or "IList")
            {
                return named.TypeArguments[0];
            }

            return type;
        }

        private static ITypeSymbol UnwrapNullable(ITypeSymbol type) =>
            type is INamedTypeSymbol { Name: "Nullable", TypeArguments.Length: 1 } nullable
                ? nullable.TypeArguments[0]
                : type;

        private static bool IsScalar(ITypeSymbol type)
        {
            if (type.TypeKind == TypeKind.Enum) return true;
            if (type.IsValueType && type.SpecialType != SpecialType.None) return true;
            return ScalarTypeNames.Contains(type.Name);
        }

        private static string ToGraphQlFieldNameLiteral(string name)
        {
            if (string.IsNullOrEmpty(name))
                return name;

            return char.ToLowerInvariant(name[0]) + name.Substring(1);
        }
    }
}