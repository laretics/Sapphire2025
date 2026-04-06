using System.ComponentModel.DataAnnotations;
namespace TimeNet2026Data.DBStorage
{
	[MessagePack.MessagePackObject]
	public class DBRauta
    {
		[System.ComponentModel.DataAnnotations.Key]
		[MessagePack.Key(0)]
		public int Id { get; set; } //Id interno del Rauta.
		[MessagePack.Key(1)]
		public Guid HeaderId { get; set; } //Id del header.
		[MessagePack.Key(2)]
		public int TopoStorageId { get; set; } //Id interno del topoStorage.

    }
}
