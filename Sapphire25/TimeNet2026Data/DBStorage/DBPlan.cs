using System.ComponentModel.DataAnnotations;
using MessagePack;

namespace TimeNet2026.DBStorage
{
	[MessagePack.MessagePackObject]
	public class DBPlan
	{
		[System.ComponentModel.DataAnnotations.Key]
		[MessagePack.Key(0)]		
		public int Id { get; set; }
		[MessagePack.Key(1)]
		public int RautaId { get; set; }
		[MessagePack.Key(2)]
		public string PlanId { get; set; } = string.Empty;
		[MessagePack.Key(3)]
		public string Name { get; set; } = string.Empty;
		[MessagePack.Key(4)]
		public string Comment { get; set; } = string.Empty;
		[MessagePack.Key(5)]
		public string Color0 { get; set; } = string.Empty;
		[MessagePack.Key(6)]
		public string Color1 { get; set; } = string.Empty;
	}
}
