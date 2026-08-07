using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sapphire2026Data.Migrations
{
	/// <inheritdoc />
	public partial class DiamondPlans : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.CreateTable(
				name: "DiamondPlans",
				columns: table => new
				{
					Id = table.Column<Guid>(type: "char(36)", nullable: false),
					Name = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false),
					SourceScript = table.Column<string>(type: "longtext", nullable: false),
					ContentHash = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
					ScriptByteLength = table.Column<int>(type: "int", nullable: false),
					TopoId = table.Column<Guid>(type: "char(36)", nullable: false),
					TopoContentHash = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
					TopoStructuralHash = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
					IncludedTopoPath = table.Column<string>(type: "varchar(260)", maxLength: 260, nullable: false),
					SourceFileName = table.Column<string>(type: "varchar(260)", maxLength: 260, nullable: false),
					Author = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false),
					Notes = table.Column<string>(type: "longtext", nullable: false),
					IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false),
					ValidFrom = table.Column<DateTime>(type: "datetime(6)", nullable: true),
					CreatedUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
					UpdatedUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
				},
				constraints: table =>
				{
					table.PrimaryKey("PK_DiamondPlans", x => x.Id);
					table.ForeignKey(
						name: "FK_DiamondPlans_DiamondTopos_TopoId",
						column: x => x.TopoId,
						principalTable: "DiamondTopos",
						principalColumn: "Id",
						onDelete: ReferentialAction.Restrict);
				})
				.Annotation("MySQL:Charset", "utf8mb4");

			migrationBuilder.CreateIndex(
				name: "IX_DiamondPlans_ContentHash",
				table: "DiamondPlans",
				column: "ContentHash");

			migrationBuilder.CreateIndex(
				name: "IX_DiamondPlans_IsActive",
				table: "DiamondPlans",
				column: "IsActive");

			migrationBuilder.CreateIndex(
				name: "IX_DiamondPlans_TopoId",
				table: "DiamondPlans",
				column: "TopoId");
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.DropTable(name: "DiamondPlans");
		}
	}
}
