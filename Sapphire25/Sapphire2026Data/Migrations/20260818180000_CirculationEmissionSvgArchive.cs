using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Sapphire2026.Data;

#nullable disable

namespace Sapphire2026Data.Migrations
{
	[DbContext(typeof(DataStorage))]
	[Migration("20260818180000_CirculationEmissionSvgArchive")]
	public partial class CirculationEmissionSvgArchive : Migration
	{
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			// MySQL no admite DEFAULT en longtext; se añade nullable y se rellena.
			migrationBuilder.Sql(
				"ALTER TABLE `DiamondCirculationEmissions` ADD COLUMN `SvgArchive` longtext NULL");
			migrationBuilder.Sql(
				"UPDATE `DiamondCirculationEmissions` SET `SvgArchive` = '' WHERE `SvgArchive` IS NULL");
		}

		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.DropColumn(
				name: "SvgArchive",
				table: "DiamondCirculationEmissions");
		}
	}
}
