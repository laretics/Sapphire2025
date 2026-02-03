using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Sapphire2026.Data.Models
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
