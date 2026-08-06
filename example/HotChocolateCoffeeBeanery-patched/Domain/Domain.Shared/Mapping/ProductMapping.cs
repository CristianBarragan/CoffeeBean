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

                EntityKey = nameof(DataEntity.Account.AccountKey),

                // IsPrimary = true
            },

            new()
            {
                Entity = typeof(DataEntity.Contract),

                ModelKey = nameof(Product.ContractKey),

                EntityKey = nameof(DataEntity.Contract.ContractKey)
            },

            new()
            {
                Entity = typeof(DataEntity.Transaction),

                ModelKey = nameof(Product.TransactionKey),

                EntityKey = nameof(DataEntity.Transaction.TransactionKey)
            },

            new()
            {
                Entity = typeof(DataEntity.CustomerBankingRelationship),

                ModelKey =
                    nameof(Product.CustomerBankingRelationshipKey),

                EntityKey =
                    nameof(DataEntity.CustomerBankingRelationship.CustomerBankingRelationshipKey)
            }
        ],

        Fields =
        [
            new()
            {
                Source = nameof(Product.ProductType),

                Entity = typeof(DataEntity.Contract),

                Destination = nameof(DataEntity.Contract.ContractType),

                EnumMapping =
                    new EnumMappingDefinition<
                        ProductType,
                        DataEntity.ContractType>
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
                Source = nameof(Product.TransactionAmount),

                Entity = typeof(DataEntity.Transaction),

                Destination = nameof(DataEntity.Transaction.Amount)
            },

            new()
            {
                Source = nameof(Product.ContractAmount),

                Entity = typeof(DataEntity.Contract),

                Destination = nameof(DataEntity.Contract.Amount)
            },
            new()
            {
                Source = nameof(Product.AccountKey),

                Entity = typeof(DataEntity.Transaction),

                Destination = nameof(DataEntity.Transaction.AccountKey),

                IsNavigationKey = true
            },

            new()
            {
                Source = nameof(Product.ContractKey),

                Entity = typeof(DataEntity.Transaction),

                Destination = nameof(DataEntity.Transaction.ContractKey),

                IsNavigationKey = true
            }
        ]
    };
}