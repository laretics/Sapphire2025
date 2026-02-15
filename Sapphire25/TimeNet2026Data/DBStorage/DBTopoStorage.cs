using System.ComponentModel.DataAnnotations;
namespace TimeNet2026.DBStorage
{
	public class DBTopoStorage
	{
		[Key]
		public int Id { get; set; } //Id interno del storage.
		public Guid HeaderId { get; set; } //Id del header.												 
	}
}
