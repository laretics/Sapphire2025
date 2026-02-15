using System.ComponentModel.DataAnnotations;
namespace TimeNet2026Data.DBStorage
{
	public class DBAsimilationStep
	{
		[Key]
		public int Id { get; set; } //Autoincremental, sólo para consistencia de base de datos.
		public int AsimilationId { get; set; } //Id de la asimilación (de SQLite)
		public int DestinationStationId { get; set; } //Id de la estación (de SQLite)
		public int AxisId { get; set; } //Id interno del eje (de SQLite)
		public TimeSpan tripTime { get; set; }
		public TimeSpan stopTime { get; set; }
	}
}
