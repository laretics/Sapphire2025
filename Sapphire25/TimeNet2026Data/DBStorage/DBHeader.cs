using System.ComponentModel.DataAnnotations;
namespace TimeNet2026Data.DBStorage
{
	[MessagePack.MessagePackObject]
	public class DBHeader
	{
		[System.ComponentModel.DataAnnotations.Key]
		[MessagePack.Key(0)]
		public Guid Id { get; set; }
		[MessagePack.Key(1)]
		public string Name { get; set; } = string.Empty;
		[MessagePack.Key(2)]
		public string Description { get; set; } = string.Empty;
		[MessagePack.Key(3)]
		public string Comment { get; set; } = string.Empty;
		[MessagePack.Key(4)]
		public string License { get; set; } = string.Empty;
		[MessagePack.Key(5)]
		public string Author { get; set; } = string.Empty;
		[MessagePack.Key(6)]
		public DateTime FirstDate { get; set; }
		[MessagePack.Key(7)]
		public DateTime LastDate { get; set; }
		[MessagePack.Key(8)]
		public string Version { get; set; } = string.Empty;
		[MessagePack.Key(9)]
		public string Bitmap { get; set; } = string.Empty;
	}
}
