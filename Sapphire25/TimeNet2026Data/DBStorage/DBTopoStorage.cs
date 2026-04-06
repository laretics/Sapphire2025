using System.ComponentModel.DataAnnotations;
namespace TimeNet2026.DBStorage
{
	[MessagePack.MessagePackObject]
	public class DBTopoStorage
	{
		[System.ComponentModel.DataAnnotations.Key]
		[MessagePack.Key(0)]
		public int Id { get; set; } //Id interno del storage.
		[MessagePack.Key(1)]
		public Guid HeaderId { get; set; } //Id del header.												 
	}
}
