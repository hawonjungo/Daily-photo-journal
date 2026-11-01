using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Relifes.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddGoogleApiUsage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GoogleApiUsage",
                columns: table => new
                {
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    CallCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GoogleApiUsage", x => x.Date);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GoogleApiUsage");
        }
    }
}
