using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sapphire2026Data.Migrations
{
    /// <inheritdoc />
    public partial class GMaoBasic : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GMAOWorkOrders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    WorkType = table.Column<Guid>(type: "char(36)", nullable: false),
                    DestinationObjectId = table.Column<Guid>(type: "char(36)", nullable: true),
                    TrainId = table.Column<Guid>(type: "char(36)", nullable: true),
                    OpenUserId = table.Column<Guid>(type: "char(36)", nullable: false),
                    CloseUserId = table.Column<Guid>(type: "char(36)", nullable: true),
                    VerifyUserId = table.Column<Guid>(type: "char(36)", nullable: true),
                    OpenTime = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CloseTime = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GMAOWorkOrders", x => x.Id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "GMAOWorksCatalog",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    Name = table.Column<string>(type: "longtext", nullable: false),
                    Comment = table.Column<string>(type: "longtext", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GMAOWorksCatalog", x => x.Id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GMAOWorkOrders");

            migrationBuilder.DropTable(
                name: "GMAOWorksCatalog");
        }
    }
}
