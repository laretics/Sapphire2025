using System.ComponentModel.DataAnnotations;
using MessagePack;
namespace TimeNet2026Data.DBStorage
{
	[MessagePack.MessagePackObject]
	public class DBAsimilation
	{
		[MessagePack.Key(0)]
		[System.ComponentModel.DataAnnotations.Key]
		public int Id { get; set; } //Código interno.
		[MessagePack.Key(1)]
		public int TopoStorageId { get; set; } //Referencia al almacén de topología.
		[MessagePack.Key(2)]
		public string AsimilationId { get; set; } = string.Empty; //Referencia a nivel Onice
		[MessagePack.Key(3)]
		public string Name { get; set; } = string.Empty;
		[MessagePack.Key(4)]
		public string Comment { get; set; } = string.Empty;
		[MessagePack.Key(5)]
		public string Color0 { get; set; } = "black";
		[MessagePack.Key(6)]
		public string Color1 { get; set; } = "black";
		[MessagePack.Key(7)]
		public int MaxSpeed { get; set; }
		[MessagePack.Key(8)]
		public int OriginStationId { get; set; } //Referencia de SqLite

	}
}
