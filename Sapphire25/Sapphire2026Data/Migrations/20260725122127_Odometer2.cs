using System;
using Microsoft.EntityFrameworkCore.Migrations;
using MySql.EntityFrameworkCore.Metadata;

#nullable disable

namespace Sapphire2026Data.Migrations
{
    /// <inheritdoc />
    public partial class Odometer2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // IF EXISTS: la tabla puede no existir si se borró a mano o nunca se creó.
            migrationBuilder.Sql("DROP TABLE IF EXISTS `Odometer`;");

            // Rename IsSympthom → IsSymptom removed: column already corrected in DB
            // and NoteLabels creates IsSymptom directly.

            migrationBuilder.AddColumn<long>(
                name: "LastOdometer",
                table: "Trains",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastOdometerSet",
                table: "Trains",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Odometry",
                columns: table => new
                {
                    Guid = table.Column<Guid>(type: "char(36)", nullable: false),
                    TrainId = table.Column<Guid>(type: "char(36)", nullable: false),
                    TimeSpan = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    Odometer = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Odometry", x => x.Guid);
                })
                .Annotation("MySQL:Charset", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Odometry");

            migrationBuilder.DropColumn(
                name: "LastOdometer",
                table: "Trains");

            migrationBuilder.DropColumn(
                name: "LastOdometerSet",
                table: "Trains");

            migrationBuilder.CreateTable(
                name: "Odometer",
                columns: table => new
                {
                    InternalId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    Odometer = table.Column<long>(type: "bigint", nullable: false),
                    TimeStamp = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Odometer", x => x.InternalId);
                })
                .Annotation("MySQL:Charset", "utf8mb4");
        }
    }
}
