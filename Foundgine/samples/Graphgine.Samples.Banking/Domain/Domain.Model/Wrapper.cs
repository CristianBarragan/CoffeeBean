using Graphgine.Sql;

namespace Domain.Model;

public partial class Wrapper
{
    public string CacheKey { get; set; }
    
    public List<CustomerCustomerEdge>? CustomerCustomerEdge { get; set; }

    public Model Model { get; set; }
}

public enum Model
{
    CustomerCustomerEdge,
    CustomerCustomerRelationship,
    Customer,
    // OuterCustomer,
    // InnerCustomer,
    ContactPoint,
    CustomerBankingRelationship,
    Product,
    Contract,
    Account,
    Transaction
}