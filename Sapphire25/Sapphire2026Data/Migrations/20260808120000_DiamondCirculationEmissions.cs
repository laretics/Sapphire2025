using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sapphire2026Data.Migrations
{
	/// <inheritdoc />
	public partial class DiamondCirculationEmissions : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.CreateTable(
				name: "DiamondCirculationEmissions",
				columns: table => new
				{
					Id = table.Column<Guid>(type: "char(36)", nullable: false),
					EmittedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
					UserId = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
					DocumentKind = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: false),
					Channel = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: false),
					SealCode = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false),
					Payload = table.Column<string>(type: "varchar(1024)", maxLength: 1024, nullable: false),
					PlanOrTrain = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false),
					EditionLabel = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false),
					DayLabel = table.Column<string>(type: "varchar(120)", maxLength: 120, nullable: false),
					SheetCount = table.Column<int>(type: "int", nullable: false),
					CertThumbprint = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
					PdfContentHash = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
					PdfCmsSignatureBase64 = table.Column<string>(type: "longtext", nullable: false),
					QrText = table.Column<string>(type: "varchar(512)", maxLength: 512, nullable: false),
					HostPoint = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false)
				},
				constraints: table =>
				{
					table.PrimaryKey("PK_DiamondCirculationEmissions", x => x.Id);
				})
				.Annotation("MySQL:Charset", "utf8mb4");

			migrationBuilder.CreateIndex(
				name: "IX_DiamondCirculationEmissions_EmittedAtUtc",
				table: "DiamondCirculationEmissions",
				column: "EmittedAtUtc");

			migrationBuilder.CreateIndex(
				name: "IX_DiamondCirculationEmissions_SealCode",
				table: "DiamondCirculationEmissions",
				column: "SealCode");

			migrationBuilder.CreateIndex(
				name: "IX_DiamondCirculationEmissions_UserId",
				table: "DiamondCirculationEmissions",
				column: "UserId");
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.DropTable(name: "DiamondCirculationEmissions");
		}
	}
}
