using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sapphire2026Data.Migrations
{
	/// <inheritdoc />
	public partial class DiamondPublishedPlans : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.CreateTable(
				name: "DiamondPublishedPlans",
				columns: table => new
				{
					Id = table.Column<Guid>(type: "char(36)", nullable: false),
					SourcePlanId = table.Column<Guid>(type: "char(36)", nullable: true),
					Name = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false),
					TopoId = table.Column<Guid>(type: "char(36)", nullable: false),
					TopoContentHash = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
					TopoStructuralHash = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
					ValidFrom = table.Column<DateTime>(type: "datetime(6)", nullable: false),
					ValidTo = table.Column<DateTime>(type: "datetime(6)", nullable: true),
					CompiledUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
					ContentHash = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
					Format = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false),
					Payload = table.Column<byte[]>(type: "mediumblob", nullable: false),
					ByteLength = table.Column<int>(type: "int", nullable: false),
					CirculationCount = table.Column<int>(type: "int", nullable: false),
					AsimilationCount = table.Column<int>(type: "int", nullable: false),
					Notes = table.Column<string>(type: "longtext", nullable: false),
					IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false),
					CreatedUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
				},
				constraints: table =>
				{
					table.PrimaryKey("PK_DiamondPublishedPlans", x => x.Id);
					table.ForeignKey(
						name: "FK_DiamondPublishedPlans_DiamondPlans_SourcePlanId",
						column: x => x.SourcePlanId,
						principalTable: "DiamondPlans",
						principalColumn: "Id",
						onDelete: ReferentialAction.SetNull);
					table.ForeignKey(
						name: "FK_DiamondPublishedPlans_DiamondTopos_TopoId",
						column: x => x.TopoId,
						principalTable: "DiamondTopos",
						principalColumn: "Id",
						onDelete: ReferentialAction.Restrict);
				})
				.Annotation("MySQL:Charset", "utf8mb4");

			migrationBuilder.CreateIndex(
				name: "IX_DiamondPublishedPlans_ContentHash",
				table: "DiamondPublishedPlans",
				column: "ContentHash");

			migrationBuilder.CreateIndex(
				name: "IX_DiamondPublishedPlans_IsActive",
				table: "DiamondPublishedPlans",
				column: "IsActive");

			migrationBuilder.CreateIndex(
				name: "IX_DiamondPublishedPlans_SourcePlanId",
				table: "DiamondPublishedPlans",
				column: "SourcePlanId");

			migrationBuilder.CreateIndex(
				name: "IX_DiamondPublishedPlans_TopoId",
				table: "DiamondPublishedPlans",
				column: "TopoId");

			migrationBuilder.CreateIndex(
				name: "IX_DiamondPublishedPlans_ValidFrom",
				table: "DiamondPublishedPlans",
				column: "ValidFrom");
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.DropTable(name: "DiamondPublishedPlans");
		}
	}
}
