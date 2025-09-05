using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BankingManagmentApp.Migrations
{
    /// <inheritdoc />
    public partial class AddChatTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ChatHistory_AspNetUsers_CustomerId",
                table: "ChatHistory");

            migrationBuilder.DropPrimaryKey(
                name: "PK_TemplateAnswer",
                table: "TemplateAnswer");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ChatHistory",
                table: "ChatHistory");

            migrationBuilder.RenameTable(
                name: "TemplateAnswer",
                newName: "TemplateAnswers");

            migrationBuilder.RenameTable(
                name: "ChatHistory",
                newName: "ChatHistories");

            migrationBuilder.RenameIndex(
                name: "IX_ChatHistory_CustomerId",
                table: "ChatHistories",
                newName: "IX_ChatHistories_CustomerId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_TemplateAnswers",
                table: "TemplateAnswers",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ChatHistories",
                table: "ChatHistories",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ChatHistories_AspNetUsers_CustomerId",
                table: "ChatHistories",
                column: "CustomerId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ChatHistories_AspNetUsers_CustomerId",
                table: "ChatHistories");

            migrationBuilder.DropPrimaryKey(
                name: "PK_TemplateAnswers",
                table: "TemplateAnswers");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ChatHistories",
                table: "ChatHistories");

            migrationBuilder.RenameTable(
                name: "TemplateAnswers",
                newName: "TemplateAnswer");

            migrationBuilder.RenameTable(
                name: "ChatHistories",
                newName: "ChatHistory");

            migrationBuilder.RenameIndex(
                name: "IX_ChatHistories_CustomerId",
                table: "ChatHistory",
                newName: "IX_ChatHistory_CustomerId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_TemplateAnswer",
                table: "TemplateAnswer",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ChatHistory",
                table: "ChatHistory",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ChatHistory_AspNetUsers_CustomerId",
                table: "ChatHistory",
                column: "CustomerId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
