using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sapphire2026Data.Migrations
{
    /// <inheritdoc />
    public partial class GMaoBasic4 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LastWash",
                table: "Trains",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ClosureTime",
                table: "Notes",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ClosureUser",
                table: "Notes",
                type: "char(36)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastWash",
                table: "Trains");

            migrationBuilder.DropColumn(
                name: "ClosureTime",
                table: "Notes");

            migrationBuilder.DropColumn(
                name: "ClosureUser",
                table: "Notes");
        }
    }
}
