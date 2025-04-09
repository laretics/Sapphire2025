using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Sapphire2025Server.Models
{
	[Table("ExtendedRoles")]
	public class ExtendedRole
	{
		[Key]
		public uint Id { get; set; }
		public string Name { get; set; } //Nombre del rol extendido
		public string? Comment {  get; set; } //Comentarios del rol.		
	}
}
