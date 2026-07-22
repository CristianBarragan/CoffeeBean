using CoffeeBeanery.GraphQL.Core.Mapping;
using Domain.Model;
using DataEntity = Database.Entity;

namespace Domain.Shared.Mapping;

public partial class ProductMapping : IMappingDefinition
{
    public MappingDefinition Definition => new()
    {
        Model = typeof(Product),

        MutationName = nameof(Product),

        Entities =
        [
            new()
            {
                Entity = typeof(DataEntity.Account),
                ModelKey = nameof(Product.AccountKey),
                EntityKey = nameof(DataEntity.Account.AccountKey)
            },

            new()
            {
                Entity = typeof(DataEntity.Contract),
                ModelKey = nameof(Product.ContractKey),
                EntityKey = nameof(DataEntity.Contract.ContractKey),
                IsPrimary = false
            },

            new()
            {
                Entity = typeof(DataEntity.Transaction),
                ModelKey = nameof(Product.TransactionKey),
                EntityKey = nameof(DataEntity.Transaction.TransactionKey),
                IsPrimary = false
            },

            new()
            {
                Entity = typeof(DataEntity.CustomerBankingRelationship),
                ModelKey = nameof(Product.CustomerBankingRelationshipKey),
                EntityKey = nameof(DataEntity.CustomerBankingRelationship.CustomerBankingRelationshipKey),
                IsPrimary = false
            }
        ],

        PrimaryKey =
        [
            new()
            {
                Entity = typeof(DataEntity.Account),
                ModelKey = nameof(Product.AccountKey),
                ColumnKey = nameof(DataEntity.Account.Id)
            }
        ],

        UpsertKeys =
        [
            new()
            {
                Entity = typeof(DataEntity.Account),
                Column = nameof(DataEntity.Account.AccountKey)
            },

            new()
            {
                Entity = typeof(DataEntity.Contract),
                Column = nameof(DataEntity.Contract.ContractKey)
            },

            new()
            {
                Entity = typeof(DataEntity.Transaction),
                Column = nameof(DataEntity.Transaction.TransactionKey)
            },

            new()
            {
                Entity = typeof(DataEntity.CustomerBankingRelationship),
                Column = nameof(DataEntity.CustomerBankingRelationship.CustomerBankingRelationshipKey)
            }
        ],

        Fields =
        [
            new()
            {
                Source = nameof(Product.ProductType),
                Entity = typeof(DataEntity.Contract),
                Destination = nameof(DataEntity.Contract.ContractType),

                EnumMapping = new EnumMappingDefinition<ProductType, DataEntity.ContractType>
                {
                    Overrides =
                    {
                        [nameof(ProductType.PersonalLoanProduct)] =
                            nameof(DataEntity.ContractType.PersonalLoan),

                        [nameof(ProductType.MortgageProduct)] =
                            nameof(DataEntity.ContractType.Mortgage),

                        [nameof(ProductType.CreditCardProduct)] =
                            nameof(DataEntity.ContractType.CreditCard)
                    }
                }
            },

            new()
            {
                Source = nameof(Product.Amount),
                Entity = typeof(DataEntity.Transaction),
                Destination = nameof(DataEntity.Transaction.Amount)
            },

            new()
            {
                Source = nameof(Product.Amount),
                Entity = typeof(DataEntity.Contract),
                Destination = nameof(DataEntity.Contract.Amount)
            },

            new()
            {
                Source = nameof(Product.CustomerBankingRelationshipKey),
                Entity = typeof(DataEntity.CustomerBankingRelationship),
                Destination = nameof(DataEntity.CustomerBankingRelationship.CustomerBankingRelationshipKey)
            },

            new()
            {
                Source = nameof(Product.ContractKey),
                Entity = typeof(DataEntity.Contract),
                Destination = nameof(DataEntity.Contract.ContractKey)
            },

            new()
            {
                Source = nameof(Product.CustomerKey),
                Entity = typeof(DataEntity.CustomerBankingRelationship),
                Destination = nameof(DataEntity.CustomerBankingRelationship.CustomerKey)
            },

            new()
            {
                Source = nameof(Product.AccountKey),
                Entity = typeof(DataEntity.Account),
                Destination = nameof(DataEntity.Account.AccountKey)
            },

            new()
            {
                Source = nameof(Product.TransactionKey),
                Entity = typeof(DataEntity.Transaction),
                Destination = nameof(DataEntity.Transaction.TransactionKey)
            },

            new()
            {
                Source = nameof(Product.AccountName),
                Entity = typeof(DataEntity.Account),
                Destination = nameof(DataEntity.Account.AccountName)
            },

            new()
            {
                Source = nameof(Product.AccountNumber),
                Entity = typeof(DataEntity.Account),
                Destination = nameof(DataEntity.Account.AccountNumber)
            },

            new()
            {
                Source = nameof(Product.Balance),
                Entity = typeof(DataEntity.Transaction),
                Destination = nameof(DataEntity.Transaction.Balance)
            }
        ]
    };
}