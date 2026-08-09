using Graphgine.Sql;

namespace Domain.Model;

public partial class Transaction
{

    public Guid TransactionKey { get; set; }

    public decimal? Amount { get; set; }

    public decimal? Balance { get; set; }
    
    public Guid? AccountKey { get; set; }
    
    public Account? Account { get; set; }
    
    public Guid? ContractKey { get; set; }
    
    public Contract? Contract { get; set; }
}