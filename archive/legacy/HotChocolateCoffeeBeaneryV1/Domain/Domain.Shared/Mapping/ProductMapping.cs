using CoffeeBeanery.GraphQL.Core.Mapping;
using CoffeeBeanery.GraphQL.Core.Sql;
using Domain.Model;
using DataEntity = Database.Entity;

namespace Domain.Shared.Mapping;

public class ProductMappingSet : IMappingSet<CustomerMappingType, Domain.Model.Model>
{
    public void Register(CustomerMappingType type, Domain.Model.Model model)
    {
        new ProductMapping(type.ToString(), model.ToString()).Register();
    }
}

public class ProductMapping : BaseModelMappingRegistration<Product>
{
    public ProductMapping(string alias, string model) : base(alias, model)
    {
    }

    protected override NodeMap BuildMap()
    {
        var map = new NodeMap()
        {
            ModelName = nameof(Product)
        };

        // ModelChildren — links from Product model to its child models
        map.ModelChildren.AddRange(new[]
        {
            new LinkKey
            {
                AliasFrom  = A(nameof(Product)),
                From       = nameof(Product),
                FromColumn = nameof(Product.ContractKey),
                AliasTo    = A(nameof(Contract)),
                To         = nameof(Contract),
                ToColumn   = nameof(Contract.ContractKey)
            },
            new LinkKey
            {
                AliasFrom  = A(nameof(Product)),
                From       = nameof(Product),
                FromColumn = nameof(Product.AccountKey),
                AliasTo    = A(nameof(Account)),
                To         = nameof(Account),
                ToColumn   = nameof(Account.AccountKey)
            },
            new LinkKey
            {
                AliasFrom  = A(nameof(Product)),
                From       = nameof(Product),
                FromColumn = nameof(Product.CustomerBankingRelationshipKey),
                AliasTo    = A(nameof(CustomerBankingRelationship)),
                To         = nameof(CustomerBankingRelationship),
                ToColumn   = nameof(CustomerBankingRelationship.CustomerBankingRelationshipKey)
            },
            new LinkKey
            {
                AliasFrom  = A(nameof(Product)),
                From       = nameof(Product),
                FromColumn = nameof(Product.TransactionKey),
                AliasTo    = A(nameof(Transaction)),
                To         = nameof(Transaction),
                ToColumn   = nameof(Transaction.TransactionKey)
            }
        });
        
        
        map.ModelParents.Add(new LinkKey
        {
            AliasFrom  = A(nameof(Product)),
            From       = nameof(Product),
            FromColumn = nameof(Product.CustomerKey),
            AliasTo    = A(nameof(Customer)),
            To         = nameof(Customer),
            ToColumn   = nameof(Customer.CustomerKey)
        });

        map.ModelToEntityLinks.AddRange(new[]
        {
            new LinkKey
            {
                AliasFrom  = A(nameof(Product)),
                From       = nameof(Product),
                FromColumn = nameof(Product.CustomerBankingRelationshipKey),
                AliasTo    = A(nameof(DataEntity.CustomerBankingRelationship)),
                To         = nameof(DataEntity.CustomerBankingRelationship),
                ToColumn   = nameof(DataEntity.CustomerBankingRelationship.CustomerBankingRelationshipKey)
            },
            new LinkKey
            {
                AliasFrom  = A(nameof(Product)),
                From       = nameof(Product),
                FromColumn = nameof(Product.ContractKey),
                AliasTo    = A(nameof(DataEntity.Contract)),
                To         = nameof(DataEntity.Contract),
                ToColumn   = nameof(DataEntity.Contract.ContractKey)
            },
            new LinkKey
            {
                AliasFrom  = A(nameof(Product)),
                From       = nameof(Product),
                FromColumn = nameof(Product.AccountKey),
                AliasTo    = A(nameof(DataEntity.Account)),
                To         = nameof(DataEntity.Account),
                ToColumn   = nameof(DataEntity.Account.AccountKey)
            },
            new LinkKey
            {
                AliasFrom  = A(nameof(Product)),
                From       = nameof(Product),
                FromColumn = nameof(Product.TransactionKey),
                AliasTo    = A(nameof(DataEntity.Transaction)),
                To         = nameof(DataEntity.Transaction),
                ToColumn   = nameof(DataEntity.Transaction.TransactionKey)
            }
        });

        map.FieldMaps.AddRange(new[]
        {
            new FieldMap
            {
                SourceAlias       = A(nameof(Product)),
                DestinationAlias  = A(nameof(DataEntity.Contract)),
                SourceName        = nameof(Product.ContractKey),
                DestinationEntity = nameof(DataEntity.Contract),
                DestinationName   = nameof(DataEntity.Contract.ContractKey)
            },
            new FieldMap
            {
                SourceAlias       = A(nameof(Product)),
                DestinationAlias  = A(nameof(DataEntity.Contract)),
                SourceName        = nameof(Product.ProductType),
                DestinationEntity = nameof(DataEntity.Contract),
                DestinationName   = nameof(DataEntity.Contract.ContractType),
                FromEnum = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
                {
                    { ProductType.CreditCard.ToString(),   (int)ProductType.CreditCard },
                    { ProductType.Mortgage.ToString(),     (int)ProductType.Mortgage },
                    { ProductType.PersonalLoan.ToString(), (int)ProductType.PersonalLoan }
                },
                ToEnum = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
                {
                    { ContractType.CreditCard.ToString(),   (int)ContractType.CreditCard },
                    { ContractType.Mortgage.ToString(),     (int)ContractType.Mortgage },
                    { ContractType.PersonalLoan.ToString(), (int)ContractType.PersonalLoan }
                }
            },
            new FieldMap
            {
                SourceAlias       = A(nameof(Product)),
                DestinationAlias  = A(nameof(DataEntity.Contract)),
                SourceName        = nameof(Product.Amount),
                DestinationEntity = nameof(DataEntity.Contract),
                DestinationName   = nameof(DataEntity.Contract.Amount)
            },
            new FieldMap
            {
                SourceAlias       = A(nameof(Product)),
                DestinationAlias  = A(nameof(DataEntity.Customer)),
                SourceName        = nameof(Product.CustomerKey),
                DestinationEntity = nameof(DataEntity.Customer),
                DestinationName   = nameof(DataEntity.Customer.CustomerKey)
            },
            new FieldMap
            {
                SourceAlias       = A(nameof(Product)),
                DestinationAlias  = A(nameof(DataEntity.Account)),
                SourceName        = nameof(Product.AccountKey),
                DestinationEntity = nameof(DataEntity.Account),
                DestinationName   = nameof(DataEntity.Account.AccountKey)
            },
            new FieldMap
            {
                SourceAlias       = A(nameof(Product)),
                DestinationAlias  = A(nameof(DataEntity.Account)),
                SourceName        = nameof(Product.AccountName),
                DestinationEntity = nameof(DataEntity.Account),
                DestinationName   = nameof(DataEntity.Account.AccountName)
            },
            new FieldMap
            {
                SourceAlias       = A(nameof(Product)),
                DestinationAlias  = A(nameof(DataEntity.Account)),
                SourceName        = nameof(Product.AccountNumber),
                DestinationEntity = nameof(DataEntity.Account),
                DestinationName   = nameof(DataEntity.Account.AccountNumber)
            },
            new FieldMap
            {
                SourceAlias       = A(nameof(Product)),
                DestinationAlias  = A(nameof(DataEntity.Transaction)),
                SourceName        = nameof(Product.TransactionKey),
                DestinationEntity = nameof(DataEntity.Transaction),
                DestinationName   = nameof(DataEntity.Transaction.TransactionKey)
            },
            new FieldMap
            {
                SourceAlias       = A(nameof(Product)),
                DestinationAlias  = A(nameof(DataEntity.Transaction)),
                SourceName        = nameof(Product.Amount),
                DestinationEntity = nameof(DataEntity.Transaction),
                DestinationName   = nameof(DataEntity.Transaction.Amount)
            },
            new FieldMap
            {
                SourceAlias       = A(nameof(Product)),
                DestinationAlias  = A(nameof(DataEntity.Transaction)),
                SourceName        = nameof(Product.Balance),
                DestinationEntity = nameof(DataEntity.Transaction),
                DestinationName   = nameof(DataEntity.Transaction.Balance)
            },
            new FieldMap
            {
                SourceAlias       = A(nameof(Product)),
                DestinationAlias  = A(nameof(DataEntity.CustomerBankingRelationship)),
                SourceName        = nameof(Product.CustomerBankingRelationshipKey),
                DestinationEntity = nameof(DataEntity.CustomerBankingRelationship),
                DestinationName   = nameof(DataEntity.CustomerBankingRelationship.CustomerBankingRelationshipKey)
            }
        });

        return map;
    }
}