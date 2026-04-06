using System.ComponentModel.DataAnnotations;
namespace TimeNet2026Data.DBStorage
{
	[MessagePack.MessagePackObject]
	public class DBCirculation
    {
		[MessagePack.Key(0)]
		[System.ComponentModel.DataAnnotations.Key]
		public int Id { get; set; } //Código interno de la circulación
		[MessagePack.Key(1)]
		public int BlockId { get; set; } //Referencia al bloque que contiene esta circulación
		[MessagePack.Key(2)]
		public string Name { get; set; } = string.Empty;
		[MessagePack.Key(3)]
		public TimeSpan Departure { get; set; }
		[MessagePack.Key(4)]
		public string Comment { get; set; } = string.Empty;
		[MessagePack.Key(5)]
		public string Color0 { get; set; } = string.Empty;
		[MessagePack.Key(6)]
		public string Color1 { get; set; } = string.Empty;
    }
}
