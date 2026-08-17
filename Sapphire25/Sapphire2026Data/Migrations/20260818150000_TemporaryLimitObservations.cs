using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Sapphire2026.Data;

#nullable disable

namespace Sapphire2026Data.Migrations
{
	[DbContext(typeof(DataStorage))]
	[Migration("20260818150000_TemporaryLimitObservations")]
	public partial class TemporaryLimitObservations : Migration
	{
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.AddColumn<string>(
				name: "Observations",
				table: "DiamondTemporaryLimits",
				type: "varchar(500)",
				maxLength: 500,
				nullable: false,
				defaultValue: "");
		}

		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.DropColumn(
				name: "Observations",
				table: "DiamondTemporaryLimits");
		}
	}
}
