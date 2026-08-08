using CoffeeBeanery.GraphQL.Core.Sql;

namespace Domain.Model;

public class Account : IModel
{
    public Guid? AccountKey { get; set; }

    public string? AccountNumber { get; set; }

    public string? AccountName { get; set; }
    
    public List<Transaction>? Transaction { get; set; }
    
    public Contract? Contract { get; set; }
}