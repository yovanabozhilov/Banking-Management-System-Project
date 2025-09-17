using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BankingManagmentApp.Migrations
{
    /// <inheritdoc />
    public partial class PersonalQnAWithChatTools : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Keyword",
                table: "TemplateAnswer",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "FunctionName",
                table: "TemplateAnswer",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.CreateIndex(
                name: "IX_TemplateAnswer_Keyword",
                table: "TemplateAnswer",
                column: "Keyword");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TemplateAnswer_Keyword",
                table: "TemplateAnswer");

            migrationBuilder.AlterColumn<string>(
                name: "Keyword",
                table: "TemplateAnswer",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AlterColumn<string>(
                name: "FunctionName",
                table: "TemplateAnswer",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);
        }
    }
}
