using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sapphire2026Data.Migrations
{
    /// <inheritdoc />
    public partial class GMaoBasic3 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Atomic",
                table: "GMAOWorksCatalog",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "Atomic",
                table: "GMAOWorkOrders",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Atomic",
                table: "GMAOWorksCatalog");

            migrationBuilder.DropColumn(
                name: "Atomic",
                table: "GMAOWorkOrders");
        }
    }
}
