using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Sapphire2026.Data;

#nullable disable

namespace Sapphire2026Data.Migrations
{
	/// <inheritdoc />
	[DbContext(typeof(DataStorage))]
	[Migration("20260817120000_UserPreferences")]
	public partial class UserPreferences : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.CreateTable(
				name: "UserPreferences",
				columns: table => new
				{
					Id = table.Column<Guid>(type: "char(36)", nullable: false),
					UserId = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
					Key = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
					Value = table.Column<string>(type: "varchar(1024)", maxLength: 1024, nullable: false),
					UpdatedUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
				},
				constraints: table =>
				{
					table.PrimaryKey("PK_UserPreferences", x => x.Id);
				});

			migrationBuilder.CreateIndex(
				name: "IX_UserPreferences_UserId",
				table: "UserPreferences",
				column: "UserId");

			migrationBuilder.CreateIndex(
				name: "IX_UserPreferences_UserId_Key",
				table: "UserPreferences",
				columns: new[] { "UserId", "Key" },
				unique: true);
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.DropTable(name: "UserPreferences");
		}
	}
}
