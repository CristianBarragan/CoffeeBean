using Graphgine.Sql;

namespace Domain.Model;

public partial class CustomerBankingRelationship
{
    
    public Guid? CustomerBankingRelationshipKey { get; set; }
    public CustomerCustomerRelationshipType? CustomerCustomerRelationshipType { get; set; }
    
    public Guid? CustomerKey { get; set; }
    
    public List<Contract>? Contract { get; set; }
}