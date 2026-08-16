using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Sapphire2026.Data;

#nullable disable

namespace Sapphire2026Data.Migrations
{
	[DbContext(typeof(DataStorage))]
	[Migration("20260818140000_DiamondTemporaryLimits")]
	public partial class DiamondTemporaryLimits : Migration
	{
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.CreateTable(
				name: "DiamondTemporaryLimits",
				columns: table => new
				{
					Id = table.Column<Guid>(type: "char(36)", nullable: false),
					TopoId = table.Column<Guid>(type: "char(36)", nullable: false),
					AxisId = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
					Pk0 = table.Column<long>(type: "bigint", nullable: false),
					Pkf = table.Column<long>(type: "bigint", nullable: false),
					Speed = table.Column<int>(type: "int", nullable: false),
					Track = table.Column<byte>(type: "tinyint unsigned", nullable: false),
					IsNewCreation = table.Column<bool>(type: "tinyint(1)", nullable: false),
					Reason = table.Column<byte>(type: "tinyint unsigned", nullable: false),
					CreatedUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
					SignaledOnTrack = table.Column<bool>(type: "tinyint(1)", nullable: false)
				},
				constraints: table =>
				{
					table.PrimaryKey("PK_DiamondTemporaryLimits", x => x.Id);
					table.ForeignKey(
						name: "FK_DiamondTemporaryLimits_DiamondTopos_TopoId",
						column: x => x.TopoId,
						principalTable: "DiamondTopos",
						principalColumn: "Id",
						onDelete: ReferentialAction.Restrict);
				});

			migrationBuilder.CreateIndex(
				name: "IX_DiamondTemporaryLimits_TopoId",
				table: "DiamondTemporaryLimits",
				column: "TopoId");

			migrationBuilder.CreateIndex(
				name: "IX_DiamondTemporaryLimits_TopoId_AxisId_Pk0",
				table: "DiamondTemporaryLimits",
				columns: new[] { "TopoId", "AxisId", "Pk0" });
		}

		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.DropTable(name: "DiamondTemporaryLimits");
		}
	}
}
