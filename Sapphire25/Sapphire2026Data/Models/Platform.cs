using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
namespace Sapphire2026Data.Models
{
	[Table("Platforms")]
	public class Platform
	{
		[Key]
		public int Id { get; set; } //Id interno del andén.
		public string StationId { get; set; } //Nombre de la dependencia donde está este andén
		public string PlatformId { get; set; } //Nombre del propio andén
		public Platform()
		{
			StationId = string.Empty;
			PlatformId = string.Empty;
		}
	}
}
