using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Sapphire2025Server.Models
{
	[Table("RoleDictionary")]
	public class RoleDictionary
	{
		[Key]
		public uint RoleId { get; set; }
		public string Name { get; set; }
		public string Comment {  get; set; }
	}
}
