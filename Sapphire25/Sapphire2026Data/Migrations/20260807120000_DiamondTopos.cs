using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sapphire2026Data.Migrations
{
	/// <inheritdoc />
	public partial class DiamondTopos : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.CreateTable(
				name: "DiamondTopos",
				columns: table => new
				{
					Id = table.Column<Guid>(type: "char(36)", nullable: false),
					Name = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false),
					ContentHash = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
					StructuralHash = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
					Format = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: false),
					Payload = table.Column<byte[]>(type: "mediumblob", nullable: false),
					ByteLength = table.Column<int>(type: "int", nullable: false),
					SourceFileName = table.Column<string>(type: "varchar(260)", maxLength: 260, nullable: false),
					Author = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false),
					LayoutId = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
					StationCount = table.Column<int>(type: "int", nullable: false),
					AxisCount = table.Column<int>(type: "int", nullable: false),
					Notes = table.Column<string>(type: "longtext", nullable: false),
					IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false),
					ValidFrom = table.Column<DateTime>(type: "datetime(6)", nullable: true),
					CreatedUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
				},
				constraints: table =>
				{
					table.PrimaryKey("PK_DiamondTopos", x => x.Id);
				})
				.Annotation("MySQL:Charset", "utf8mb4");

			migrationBuilder.CreateIndex(
				name: "IX_DiamondTopos_ContentHash",
				table: "DiamondTopos",
				column: "ContentHash",
				unique: true);

			migrationBuilder.CreateIndex(
				name: "IX_DiamondTopos_IsActive",
				table: "DiamondTopos",
				column: "IsActive");

			migrationBuilder.CreateIndex(
				name: "IX_DiamondTopos_StructuralHash",
				table: "DiamondTopos",
				column: "StructuralHash");
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.DropTable(
				name: "DiamondTopos");
		}
	}
}
