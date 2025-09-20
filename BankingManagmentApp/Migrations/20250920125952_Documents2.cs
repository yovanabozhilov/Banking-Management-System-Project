using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BankingManagmentApp.Migrations
{
    /// <inheritdoc />
    public partial class Documents2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_LoanApplication_LoanId",
                table: "LoanApplication");

            migrationBuilder.CreateIndex(
                name: "IX_LoanApplication_LoanId",
                table: "LoanApplication",
                column: "LoanId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_LoanApplication_LoanId",
                table: "LoanApplication");

            migrationBuilder.CreateIndex(
                name: "IX_LoanApplication_LoanId",
                table: "LoanApplication",
                column: "LoanId",
                unique: true);
        }
    }
}
