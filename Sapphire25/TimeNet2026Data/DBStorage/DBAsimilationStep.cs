using System.ComponentModel.DataAnnotations;
namespace TimeNet2026Data.DBStorage
{
	[MessagePack.MessagePackObject]
	public class DBAsimilationStep
	{
		[MessagePack.Key(0)]
		[System.ComponentModel.DataAnnotations.Key]
		public int Id { get; set; } //Autoincremental, sólo para consistencia de base de datos.
		[MessagePack.Key(1)]
		public int AsimilationId { get; set; } //Id de la asimilación (de SQLite)
		[MessagePack.Key(2)]
		public int DestinationStationId { get; set; } //Id de la estación (de SQLite)
		[MessagePack.Key(3)]
		public int AxisId { get; set; } //Id interno del eje (de SQLite)
		[MessagePack.Key(4)]
		public TimeSpan tripTime { get; set; }
		[MessagePack.Key(5)]
		public TimeSpan stopTime { get; set; }
	}
}
