using Graphgine.Mapping;
using Domain.Model;
using DataEntity = Database.Entity;

namespace Api.Banking.Mapping;

/// <summary>
/// Maps Domain.Model.Product — a query-facing composite with NO backing
/// entity of its own — onto four real storage tables at once: Contract,
/// Account, Transaction and CustomerBankingRelationship (plus Customer,
/// for the one scalar field that comes from there). Ported from legacy
/// Domain.Shared/Mapping/ProductMapping.cs.
///
/// This is the case PORT-STATUS.md flagged as needing a real
/// `Entities = [...]` list rather than the single-entity `Entity`/`Key`
/// shorthand every other mapping in this folder uses — Product has no
/// single owning table, so there's no shorthand to reach for.
///
/// Each Entities[] entry below deliberately omits AliasProperty: per the
/// comment on MappingClassParser's own UpsertKeys-synthesis step,
/// Entities WITHOUT AliasProperty are read as "genuine composite backing
/// tables this same mutation inserts into directly" — Product is named
/// there as the reference example. (Legacy's ProductMapping never called
/// map.UpsertKeys.Add itself either — it relied on the same kind of
/// implicit per-entity key synthesis this port leans on.)
///
/// None of the four/five entities is marked IsPrimary; MappingClassParser
/// falls back to treating Entities[0] (Contract) as primary when none is
/// specified. That has no real meaning for a model with no owning row —
/// it only affects which entity absorbs a same-named-field tiebreak, and
/// none of Product's fields collide across entities.
///
/// -----------------------------------------------------------------------
/// LEAST VERIFIED PART OF THIS PORT — flagged per PORT-STATUS.md
/// -----------------------------------------------------------------------
/// Product.Contract / Product.Account / Product.CustomerBankingRelationship
/// (singular navigations) and Product.Transaction (collection) all target
/// models whose OWN primary entity is one of the exact same entity types
/// already listed in Product's own Entities[] above (Contract's primary
/// entity is DataEntity.Contract; Product also lists DataEntity.Contract
/// directly). Whether EntityNavigationConvention/CompositeChildAttachmentConvention
/// resolve that as a zero-hop "already the same row" attachment, versus
/// attempting (and failing to find) a real FK path from Contract-entity to
/// Contract-entity, is something I could not observe without a compiler —
/// CompositeChildAttachmentConvention.cs (719 lines) exists specifically
/// for this scenario and is completely unexercised by any working example
/// in this repository. Read its generator diagnostics first if Product's
/// navigations don't resolve as expected.
/// </summary>
public sealed class ProductMapping : IMappingDefinition
{
    public MappingDefinition Definition => new()
    {
        Model = typeof(Product),

        Entities =
        [
            new EntityDefinition
            {
                Entity = typeof(DataEntity.Contract),
                ModelKey = nameof(Product.ContractKey),
                EntityKey = nameof(DataEntity.Contract.ContractKey)
            },
            new EntityDefinition
            {
                Entity = typeof(DataEntity.Account),
                ModelKey = nameof(Product.AccountKey),
                EntityKey = nameof(DataEntity.Account.AccountKey)
            },
            new EntityDefinition
            {
                Entity = typeof(DataEntity.Transaction),
                ModelKey = nameof(Product.TransactionKey),
                EntityKey = nameof(DataEntity.Transaction.TransactionKey)
            },
            new EntityDefinition
            {
                Entity = typeof(DataEntity.CustomerBankingRelationship),
                ModelKey = nameof(Product.CustomerBankingRelationshipKey),
                EntityKey = nameof(DataEntity.CustomerBankingRelationship.CustomerBankingRelationshipKey)
            },
            new EntityDefinition
            {
                Entity = typeof(DataEntity.Customer),
                ModelKey = nameof(Product.CustomerKey),
                EntityKey = nameof(DataEntity.Customer.CustomerKey)
            }
        ],

        Fields =
        [
            new FieldDefinition
            {
                Source = nameof(Product.ContractKey),
                Entity = typeof(DataEntity.Contract),
                Destination = nameof(DataEntity.Contract.ContractKey)
            },
            new FieldDefinition
            {
                Source = nameof(Product.ContractAmount),
                Entity = typeof(DataEntity.Contract),
                Destination = nameof(DataEntity.Contract.Amount)
            },
            new FieldDefinition
            {
                // Product has no ContractType of its own — its ProductType
                // is the write-through source for Contract's ContractType
                // column. Member names differ on every value
                // (*Product suffix on this side), unlike the other enum
                // mappings in this port, so all three need an explicit
                // Overrides entry rather than relying on name matching.
                Source = nameof(Product.ProductType),
                Entity = typeof(DataEntity.Contract),
                Destination = nameof(DataEntity.Contract.ContractType),
                EnumMapping = new EnumMappingDefinition<ProductType, DataEntity.ContractType>
                {
                    Overrides =
                    {
                        [nameof(ProductType.CreditCardProduct)] = nameof(DataEntity.ContractType.CreditCard),
                        [nameof(ProductType.MortgageProduct)] = nameof(DataEntity.ContractType.Mortgage),
                        [nameof(ProductType.PersonalLoanProduct)] = nameof(DataEntity.ContractType.PersonalLoan)
                    }
                }
            },
            new FieldDefinition
            {
                Source = nameof(Product.AccountKey),
                Entity = typeof(DataEntity.Account),
                Destination = nameof(DataEntity.Account.AccountKey)
            },
            new FieldDefinition
            {
                Source = nameof(Product.AccountName),
                Entity = typeof(DataEntity.Account),
                Destination = nameof(DataEntity.Account.AccountName)
            },
            new FieldDefinition
            {
                Source = nameof(Product.AccountNumber),
                Entity = typeof(DataEntity.Account),
                Destination = nameof(DataEntity.Account.AccountNumber)
            },
            new FieldDefinition
            {
                Source = nameof(Product.TransactionKey),
                Entity = typeof(DataEntity.Transaction),
                Destination = nameof(DataEntity.Transaction.TransactionKey)
            },
            new FieldDefinition
            {
                Source = nameof(Product.TransactionAmount),
                Entity = typeof(DataEntity.Transaction),
                Destination = nameof(DataEntity.Transaction.Amount)
            },
            new FieldDefinition
            {
                Source = nameof(Product.Balance),
                Entity = typeof(DataEntity.Transaction),
                Destination = nameof(DataEntity.Transaction.Balance)
            },
            new FieldDefinition
            {
                Source = nameof(Product.CustomerBankingRelationshipKey),
                Entity = typeof(DataEntity.CustomerBankingRelationship),
                Destination = nameof(DataEntity.CustomerBankingRelationship.CustomerBankingRelationshipKey)
            },
            new FieldDefinition
            {
                Source = nameof(Product.CustomerKey),
                Entity = typeof(DataEntity.Customer),
                Destination = nameof(DataEntity.Customer.CustomerKey)
            }
        ]

        // Product.Contract / Product.Account / Product.CustomerBankingRelationship
        // / Product.Transaction navigations — left to convention/inference.
        // See the LEAST VERIFIED header comment above before trusting them.
    };
}
