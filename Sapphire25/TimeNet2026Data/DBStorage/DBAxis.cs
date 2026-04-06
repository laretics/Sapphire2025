using System.ComponentModel.DataAnnotations;

namespace TimeNet2026Data.DBStorage
{
	[MessagePack.MessagePackObject]
	public class DBAxis
	{
		[MessagePack.Key(0)]
		[System.ComponentModel.DataAnnotations.Key]
		public int Id { get; set; } //Autoincremental para este eje.
		[MessagePack.Key(1)]
		public string AxisId { get; set; } = string.Empty; //Id de TimeNet
		[MessagePack.Key(2)]
		public int StorageId { get; set; } //Id del TopoStorage en el que lo hemos almacenado.
		[MessagePack.Key(3)]
		public string Name { get; set; } = string.Empty;
		[MessagePack.Key(4)]
		public string Comment { get; set; } = string.Empty;
		[MessagePack.Key(5)]
		public string Color0 { get; set; } = "black";
		[MessagePack.Key(6)]
		public string Color1 { get; set; } = "black";

	}
}
