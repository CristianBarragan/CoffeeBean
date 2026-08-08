using CoffeeBeanery.GraphQL.Core.Sql;

namespace Domain.Model;

public class Product : IModel
{
    public Guid? CustomerBankingRelationshipKey { get; set; }
    
    public CustomerBankingRelationship? CustomerBankingRelationship { get; set; }
    

    public Guid? ContractKey { get; set; }
    
    public Contract? Contract { get; set; }
    

    public Guid? CustomerKey { get; set; }
    

    public Guid? AccountKey { get; set; }
    
    public Account? Account { get; set; }
    
    public Guid? TransactionKey { get; set; }
    
    public List<Transaction>? Transaction { get; set; }

    public string? AccountName { get; set; }
    
    public string? AccountNumber { get; set; }

    public decimal? Amount { get; set; }

    public decimal? Balance { get; set; }

    public ProductType? ProductType { get; set; }
}

public enum ProductType
{
    CreditCard,
    Mortgage,
    PersonalLoan
}