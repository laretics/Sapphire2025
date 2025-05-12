using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
namespace Sapphire2025Server.Models
{
	[Table("TimeCache")]
	public class TimeCache
	{
		[Key]
		public Guid Id { get; set; }
		public byte Key { get; set; } //Clave de la tabla en caché
		public DateTime TimeStamp { get; set; } //Último cambio en esta tabla
	}
}
