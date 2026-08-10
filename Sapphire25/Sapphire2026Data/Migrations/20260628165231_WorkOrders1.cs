using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sapphire2026Data.Migrations
{
    /// <inheritdoc />
    public partial class WorkOrders1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LastPlatformAssign",
                table: "Trains",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "OpenUserId",
                table: "GMAOWorkOrders",
                type: "char(36)",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "char(36)");

            migrationBuilder.AlterColumn<DateTime>(
                name: "OpenTime",
                table: "GMAOWorkOrders",
                type: "datetime(6)",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "datetime(6)");

            migrationBuilder.AddColumn<bool>(
                name: "Rejected",
                table: "GMAOWorkOrders",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "RequestTime",
                table: "GMAOWorkOrders",
                type: "datetime(6)",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<Guid>(
                name: "RequestUserId",
                table: "GMAOWorkOrders",
                type: "char(36)",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastPlatformAssign",
                table: "Trains");

            migrationBuilder.DropColumn(
                name: "Rejected",
                table: "GMAOWorkOrders");

            migrationBuilder.DropColumn(
                name: "RequestTime",
                table: "GMAOWorkOrders");

            migrationBuilder.DropColumn(
                name: "RequestUserId",
                table: "GMAOWorkOrders");

            migrationBuilder.AlterColumn<Guid>(
                name: "OpenUserId",
                table: "GMAOWorkOrders",
                type: "char(36)",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "char(36)",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "OpenTime",
                table: "GMAOWorkOrders",
                type: "datetime(6)",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                oldClrType: typeof(DateTime),
                oldType: "datetime(6)",
                oldNullable: true);
        }
    }
}
