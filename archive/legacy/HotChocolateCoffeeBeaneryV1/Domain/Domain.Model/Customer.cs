using CoffeeBeanery.GraphQL.Core.Sql;

namespace Domain.Model;

public class Customer : IModel
{
    public Guid? CustomerKey { get; set; }

    public string? FirstNaming { get; set; }

    public string? LastNaming { get; set; }

    public string? FullNaming { get; set; }

    public CustomerType? CustomerType { get; set; }

    public List<Product>? Product { get; set; }

    public List<ContactPoint>? ContactPoint { get; set; }
}

public enum CustomerType
{
    Person,
    Organisation
}