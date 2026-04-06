using System.ComponentModel.DataAnnotations;

namespace TimeNet2026Data.DBStorage
{
	[MessagePack.MessagePackObject]
	public class DBStation
	{
		[System.ComponentModel.DataAnnotations.Key]
		[MessagePack.Key(0)]
		public int Id { get; set; } //Autonumérico para la estación
		[MessagePack.Key(1)]
		public string StationId { get; set; } = string.Empty; //Id según Onice.
		[MessagePack.Key(2)]
		public int AxisId { get; set; } //Referencia en la tabla DBRefPuntual
		[MessagePack.Key(3)]
		public long Pk { get; set; } //Es el mismo que en la tabla RefPunctual
		[MessagePack.Key(4)]
		public string Name { get; set; } = string.Empty;
		[MessagePack.Key(5)]
		public string ShortName { get; set; } = string.Empty;

	}
}
