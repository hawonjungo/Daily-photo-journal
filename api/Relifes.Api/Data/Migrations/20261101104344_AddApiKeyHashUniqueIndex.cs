using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Relifes.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddApiKeyHashUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Users_ApiKeyHash",
                table: "Users",
                column: "ApiKeyHash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Users_ApiKeyHash",
                table: "Users");
        }
    }
}
