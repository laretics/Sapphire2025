using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sapphire2026Data.Migrations
{
	/// <summary>
	/// Elimina las tablas del modelo TimeNet (reemplazado por Diamond).
	/// Idempotente: solo DROP IF EXISTS.
	/// </summary>
	public partial class RemoveTimeNetTables : Migration
	{
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			// Hijos primero (sin FKs formales en todos los motores, orden defensivo).
			migrationBuilder.Sql("DROP TABLE IF EXISTS `TNScheduleUnits`;");
			migrationBuilder.Sql("DROP TABLE IF EXISTS `TNSchedules`;");
			migrationBuilder.Sql("DROP TABLE IF EXISTS `TNCirculations`;");
			migrationBuilder.Sql("DROP TABLE IF EXISTS `TNCirculationBlocks`;");
			migrationBuilder.Sql("DROP TABLE IF EXISTS `TNPlans`;");
			migrationBuilder.Sql("DROP TABLE IF EXISTS `TNRautatie`;");
			migrationBuilder.Sql("DROP TABLE IF EXISTS `TNAsimilationSteps`;");
			migrationBuilder.Sql("DROP TABLE IF EXISTS `TNAsimilations`;");
			migrationBuilder.Sql("DROP TABLE IF EXISTS `TNRefPunctuals`;");
			migrationBuilder.Sql("DROP TABLE IF EXISTS `TNStations`;");
			migrationBuilder.Sql("DROP TABLE IF EXISTS `TNAxis`;");
			migrationBuilder.Sql("DROP TABLE IF EXISTS `TNTopoStorages`;");
			migrationBuilder.Sql("DROP TABLE IF EXISTS `TNHeaders`;");
		}

		protected override void Down(MigrationBuilder migrationBuilder)
		{
			// No se recrea TimeNet: el modelo ya no existe en el código.
		}
	}
}
