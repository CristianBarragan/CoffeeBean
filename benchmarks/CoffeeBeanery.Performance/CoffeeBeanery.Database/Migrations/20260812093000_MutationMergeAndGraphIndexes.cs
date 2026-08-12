using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoffeeBeanery.Database.Migrations;

/// <summary>
/// Adds covering traversal indexes used by the Customer -> BankingRelationship
/// -> Contract -> Transaction graph workload.
///
/// The business conflict identities used by Foundgine ON CONFLICT merges are
/// already backed by unique indexes in the initial schema:
/// CustomerKey, CustomerBankingRelationshipKey, ContractKey and TransactionKey.
/// </summary>
public partial class MutationMergeAndGraphIndexes : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateIndex(
            name: "IX_CustomerBankingRelationship_CustomerId_Id",
            schema: "Banking",
            table: "CustomerBankingRelationship",
            columns: new[] { "CustomerId", "Id" });

        migrationBuilder.CreateIndex(
            name: "IX_Contract_CustomerBankingRelationshipId_Id",
            schema: "Lending",
            table: "Contract",
            columns: new[] { "CustomerBankingRelationshipId", "Id" });

        migrationBuilder.CreateIndex(
            name: "IX_Transaction_ContractId_Id",
            schema: "Lending",
            table: "Transaction",
            columns: new[] { "ContractId", "Id" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_CustomerBankingRelationship_CustomerId_Id",
            schema: "Banking",
            table: "CustomerBankingRelationship");

        migrationBuilder.DropIndex(
            name: "IX_Contract_CustomerBankingRelationshipId_Id",
            schema: "Lending",
            table: "Contract");

        migrationBuilder.DropIndex(
            name: "IX_Transaction_ContractId_Id",
            schema: "Lending",
            table: "Transaction");
    }
}
