using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace CoffeeBeanery.GraphQL.Core.Mapping.Generators.Model
{
    public sealed class MappingClassInfo
    {
        public INamedTypeSymbol ClassSymbol { get; set; } = null!;
        public INamedTypeSymbol ModelType { get; set; } = null!;
        public INamedTypeSymbol? EntityType { get; set; }

        public string ClassName => ClassSymbol.Name;
        public string Namespace => ClassSymbol.ContainingNamespace?.IsGlobalNamespace == false
            ? ClassSymbol.ContainingNamespace.ToDisplayString()
            : "";

        public string Alias { get; set; } = "";
        public string Prefix { get; set; } = "";
        public string Schema { get; set; } = "";

        public bool IsModel { get; set; }
        public bool IsEntity { get; set; }
        public bool IsGraph { get; set; }

        public GraphMapInfo? GraphMap { get; set; }

        public List<EntityKeyInfo> ModelToEntity { get; } = new();
        public List<INamedTypeSymbol> ModelToEntityTypes { get; } = new();

        public List<FieldMapInfo> FieldMaps { get; } = new();
        public List<FieldMapInfo> ManualFieldMaps { get; } = new();

        public List<ExcludedFieldMappingInfo> ExcludedFieldMappings { get; } = new();
        public List<ModelChildInfo> ModelChildren { get; } = new();
        public List<UpsertKeyInfo> UpsertKeys { get; } = new();
        public List<Diagnostic> Diagnostics { get; } = new();
        
        public List<AutoChildAttachmentInfo> AutoChildAttachments { get; } = new();

        public string Id { get; set; } = "";
        
        public List<CteUpdateMetaInfo> CteUpdateMeta { get; } = new();
    }
    
    public sealed class CteUpdateMetaInfo
    {
        /// <summary>
        /// Navigation alias — "InnerCustomer" / "OuterCustomer".
        /// Matched against MutationCteNode.Alias at runtime.
        /// </summary>
        public required string NavigationAlias { get; init; }

        /// <summary>
        /// FK column on the owning entity — "InnerCustomerId" / "OuterCustomerId".
        /// From HasForeignKey(c => c.InnerCustomerId).
        /// </summary>
        public required string ForeignKeyColumn { get; init; }

        /// <summary>
        /// PK column on the owning entity — "CustomerCustomerRelationshipKey".
        /// From the ModelToEntity entry where IsPrimary = true.
        /// </summary>
        public required string OwningPrimaryKeyColumn { get; init; }

        /// <summary>
        /// Simple name of the related entity type — "Customer".
        /// Used to build the table alias: NavigationAlias + RelatedEntityTypeName.
        /// </summary>
        public required string RelatedEntityTypeName { get; init; }

        /// <summary>
        /// Surrogate Id column on the related entity — "Id".
        /// SELECTed in the subquery and written into ForeignKeyColumn.
        /// </summary>
        public required string RelatedSurrogateIdColumn { get; init; }

        /// <summary>
        /// Natural key column on the related entity — "CustomerKey".
        /// Used in the WHERE clause to find the right row.
        /// From the ModelToEntity entry whose AliasProperty matches NavigationAlias.
        /// </summary>
        public required string RelatedNaturalKeyColumn { get; init; }
    }
    
    public sealed class AutoChildAttachmentInfo
    {
        public required string FieldName { get; init; }
        public required string ToModelName { get; init; }
        public required INamedTypeSymbol ParentEntityType { get; init; }
        public required string ParentJoinColumn { get; init; }
        public required INamedTypeSymbol ChildEntityType { get; init; }
        public required string ChildJoinColumn { get; init; }
    }

    public sealed class EntityKeyInfo
    {
        public string From { get; set; } = "";
        public string AliasFrom { get; set; } = "";
        public string? FromColumn { get; set; }
        public string To { get; set; } = "";
        public string AliasTo { get; set; } = "";
        public string? ToColumn { get; set; }
        public required INamedTypeSymbol EntityType { get; set; }
        public string? AliasProperty { get; set; }
        
        public bool IsPrimary { get; set; } 
    }

    public sealed class FieldMapInfo
    {
        public required string SourceName { get; init; }
        public required string DestinationEntity { get; init; }
        public required string DestinationName { get; init; }
        public string? SourceAlias { get; set; }
        public string? DestinationAlias { get; set; }
        public Dictionary<string, int>? FromEnum { get; init; }
        public Dictionary<string, int>? ToEnum { get; init; }
        public bool IsGenerated { get; init; }
    }

    public sealed class ExcludedFieldMappingInfo
    {
        public required string SourceName { get; init; }
        public required string DestinationEntity { get; init; }
    }

    public sealed class ModelChildInfo
    {
        public required string To { get; init; }
    }

    public sealed class UpsertKeyInfo
    {
        public required string Entity { get; init; }
        public required string Key { get; init; }
    }

    public sealed class GraphMapInfo
    {
        public required string GraphName { get; init; }
    }

    public sealed class NavigationInfo
    {
        public required string NavigationName { get; init; }
        public required INamedTypeSymbol RelatedEntityType { get; init; }
        public required string ForeignKeyProperty { get; init; }
        public required string PrincipalKeyProperty { get; init; }
        public bool IsCollection { get; init; }

        public bool TargetIsRoot { get; set; }
    }

    public sealed class NavigationResolutionResult
    {
        public List<NavigationInfo> Navigations { get; } = new();
        public bool HasBlockingAmbiguity { get; set; }
        public List<Diagnostic> PendingDiagnostics { get; } = new();
    }
}