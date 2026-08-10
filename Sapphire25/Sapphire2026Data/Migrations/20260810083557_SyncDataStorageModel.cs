using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sapphire2026Data.Migrations
{
	/// <summary>
	/// Alinea el snapshot con el modelo actual tras retirar TimeNet.
	/// DiamondPublishedPlans: solo crea si no existe (puede haberse aplicado antes).
	/// SessionEvents.hostPoint: longtext → varchar(255) según el modelo.
	/// </summary>
	public partial class SyncDataStorageModel : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			// Ajuste de columna (MySQL). Si ya es varchar(255), el ALTER es inocuo en la práctica
			// o fallará de forma legible; se deja explícito.
			migrationBuilder.Sql("""
				ALTER TABLE `SessionEvents`
				MODIFY COLUMN `hostPoint` varchar(255) NOT NULL;
				""");

			// Tabla de planes publicados: no fallar si ya está en la BD.
			migrationBuilder.Sql("""
				CREATE TABLE IF NOT EXISTS `DiamondPublishedPlans` (
					`Id` char(36) NOT NULL,
					`SourcePlanId` char(36) NULL,
					`Name` varchar(200) NOT NULL,
					`TopoId` char(36) NOT NULL,
					`TopoContentHash` varchar(64) NOT NULL,
					`TopoStructuralHash` varchar(64) NOT NULL,
					`ValidFrom` datetime(6) NOT NULL,
					`ValidTo` datetime(6) NULL,
					`CompiledUtc` datetime(6) NOT NULL,
					`ContentHash` varchar(64) NOT NULL,
					`Format` varchar(32) NOT NULL,
					`Payload` mediumblob NOT NULL,
					`ByteLength` int NOT NULL,
					`CirculationCount` int NOT NULL,
					`AsimilationCount` int NOT NULL,
					`Notes` longtext NOT NULL,
					`IsActive` tinyint(1) NOT NULL,
					`CreatedUtc` datetime(6) NOT NULL,
					PRIMARY KEY (`Id`),
					KEY `IX_DiamondPublishedPlans_ContentHash` (`ContentHash`),
					KEY `IX_DiamondPublishedPlans_IsActive` (`IsActive`),
					KEY `IX_DiamondPublishedPlans_SourcePlanId` (`SourcePlanId`),
					KEY `IX_DiamondPublishedPlans_TopoId` (`TopoId`),
					KEY `IX_DiamondPublishedPlans_ValidFrom` (`ValidFrom`),
					CONSTRAINT `FK_DiamondPublishedPlans_DiamondPlans_SourcePlanId`
						FOREIGN KEY (`SourcePlanId`) REFERENCES `DiamondPlans` (`Id`) ON DELETE SET NULL,
					CONSTRAINT `FK_DiamondPublishedPlans_DiamondTopos_TopoId`
						FOREIGN KEY (`TopoId`) REFERENCES `DiamondTopos` (`Id`) ON DELETE RESTRICT
				) CHARACTER SET=utf8mb4;
				""");
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			// No se elimina DiamondPublishedPlans en Down (puede tener datos reales).
			migrationBuilder.Sql("""
				ALTER TABLE `SessionEvents`
				MODIFY COLUMN `hostPoint` longtext NOT NULL;
				""");
		}
	}
}
