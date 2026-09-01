using Foundgine.Abstractions;
using Foundgine.Metadata;
using Foundgine.Semantics;
using Microsoft.EntityFrameworkCore;

namespace Foundgine.Testing;

/// <summary>Canonical relational model shared by Foundgine-vs-EF differential tests.</summary>
public static class CanonicalBanking
{
    public static readonly EntityId Customer = new(1);
    public static readonly EntityId Account = new(2);
    public static readonly EntityId Transaction = new(3);
    public static readonly RelationshipId CustomerAccounts = new(1);
    public static readonly RelationshipId AccountTransactions = new(2);
    public static readonly RelationshipId AccountCustomer = new(3);
    public static readonly RelationshipId TransactionAccount = new(4);

    public static SemanticModel BuildModel() =>
        new SemanticModelBuilder()
            .Entity(Customer, "Customer", customer => customer
                .Identity(new FieldId(1), "Id")
                .Field(new FieldId(2), "Name", typeof(string))
                .Relationship(CustomerAccounts, "Accounts", Account, RelationshipCardinality.Many))
            .Entity(Account, "Account", account => account
                .Identity(new FieldId(1), "Id")
                .Field(new FieldId(3), "Balance", typeof(decimal))
                .Field(new FieldId(4), "CustomerId", typeof(int))
                .Field(new FieldId(5), "Status", typeof(string))
                .Relationship(AccountTransactions, "Transactions", Transaction, RelationshipCardinality.Many)
                .Relationship(AccountCustomer, "Customer", Customer, RelationshipCardinality.One))
            .Entity(Transaction, "Transaction", transaction => transaction
                .Identity(new FieldId(1), "Id")
                .Field(new FieldId(2), "AccountId", typeof(int))
                .Field(new FieldId(3), "Amount", typeof(decimal))
                .Field(new FieldId(4), "TransactionDate", typeof(DateTime))
                .Relationship(TransactionAccount, "Account", Account, RelationshipCardinality.One))
            .Build();

    public static MetadataRegistry BuildMetadata()
    {
        var registry = new MetadataRegistry();
        registry.Register(new EntityMetadata(Customer, "Customer",
            [new ColumnMetadata(new ColumnId(1), "Id"), new ColumnMetadata(new ColumnId(2), "Name")],
            null,
            [
                new FieldMetadata(new FieldId(1), "Id", typeof(int), new ColumnReference(Customer, new ColumnId(1))),
                new FieldMetadata(new FieldId(2), "Name", typeof(string), new ColumnReference(Customer, new ColumnId(2)))
            ],
            new ColumnReference(Customer, new ColumnId(1))));
        registry.Register(new EntityMetadata(Account, "Account",
            [new ColumnMetadata(new ColumnId(1), "Id"), new ColumnMetadata(new ColumnId(2), "CustomerId"), new ColumnMetadata(new ColumnId(3), "Balance"), new ColumnMetadata(new ColumnId(4), "Status")],
            null,
            [
                new FieldMetadata(new FieldId(1), "Id", typeof(int), new ColumnReference(Account, new ColumnId(1))),
                new FieldMetadata(new FieldId(3), "Balance", typeof(decimal), new ColumnReference(Account, new ColumnId(3))),
                new FieldMetadata(new FieldId(4), "CustomerId", typeof(int), new ColumnReference(Account, new ColumnId(2))),
                new FieldMetadata(new FieldId(5), "Status", typeof(string), new ColumnReference(Account, new ColumnId(4)))
            ],
            new ColumnReference(Account, new ColumnId(1))));
        registry.Register(new EntityMetadata(Transaction, "Transaction",
            [new ColumnMetadata(new ColumnId(1), "Id"), new ColumnMetadata(new ColumnId(2), "AccountId"), new ColumnMetadata(new ColumnId(3), "Amount"), new ColumnMetadata(new ColumnId(4), "TransactionDate")],
            null,
            [
                new FieldMetadata(new FieldId(1), "Id", typeof(int), new ColumnReference(Transaction, new ColumnId(1))),
                new FieldMetadata(new FieldId(2), "AccountId", typeof(int), new ColumnReference(Transaction, new ColumnId(2))),
                new FieldMetadata(new FieldId(3), "Amount", typeof(decimal), new ColumnReference(Transaction, new ColumnId(3))),
                new FieldMetadata(new FieldId(4), "TransactionDate", typeof(DateTime), new ColumnReference(Transaction, new ColumnId(4)))
            ],
            new ColumnReference(Transaction, new ColumnId(1))));

        registry.Register(new RelationshipMetadata(CustomerAccounts, Customer, Account, "Accounts",
            new ColumnReference(Customer, new ColumnId(1)), new ColumnReference(Account, new ColumnId(2))));
        registry.Register(new RelationshipMetadata(AccountTransactions, Account, Transaction, "Transactions",
            new ColumnReference(Account, new ColumnId(1)), new ColumnReference(Transaction, new ColumnId(2))));
        registry.Register(new RelationshipMetadata(AccountCustomer, Account, Customer, "Customer",
            new ColumnReference(Account, new ColumnId(2)), new ColumnReference(Customer, new ColumnId(1))));
        registry.Register(new RelationshipMetadata(TransactionAccount, Transaction, Account, "Account",
            new ColumnReference(Transaction, new ColumnId(2)), new ColumnReference(Account, new ColumnId(1))));
        return registry;
    }
}

public sealed class CanonicalBankingDbContext(DbContextOptions<CanonicalBankingDbContext> options) : DbContext(options)
{
    public DbSet<CustomerRow> Customers => Set<CustomerRow>();
    public DbSet<AccountRow> Accounts => Set<AccountRow>();
    public DbSet<TransactionRow> Transactions => Set<TransactionRow>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("fg_query");
        modelBuilder.Entity<CustomerRow>(e =>
        {
            e.ToTable("Customer", "fg_query"); e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("Id"); e.Property(x => x.Name).HasColumnName("Name");
            e.HasMany(x => x.Accounts).WithOne().HasForeignKey(x => x.CustomerId);
        });
        modelBuilder.Entity<AccountRow>(e =>
        {
            e.ToTable("Account", "fg_query"); e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("Id"); e.Property(x => x.CustomerId).HasColumnName("CustomerId"); e.Property(x => x.Balance).HasColumnName("Balance"); e.Property(x => x.Status).HasColumnName("Status");
            e.HasMany(x => x.Transactions).WithOne().HasForeignKey(x => x.AccountId);
        });
        modelBuilder.Entity<TransactionRow>(e =>
        {
            e.ToTable("Transaction", "fg_query"); e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("Id"); e.Property(x => x.AccountId).HasColumnName("AccountId"); e.Property(x => x.Amount).HasColumnName("Amount"); e.Property(x => x.TransactionDate).HasColumnName("TransactionDate");
        });
    }
}

public sealed class CustomerRow { public int Id { get; set; } public string Name { get; set; } = ""; public List<AccountRow> Accounts { get; set; } = []; }
public sealed class AccountRow { public int Id { get; set; } public int CustomerId { get; set; } public decimal Balance { get; set; } public string Status { get; set; } = ""; public List<TransactionRow> Transactions { get; set; } = []; }
public sealed class TransactionRow { public int Id { get; set; } public int AccountId { get; set; } public decimal Amount { get; set; } public DateTime TransactionDate { get; set; } }
