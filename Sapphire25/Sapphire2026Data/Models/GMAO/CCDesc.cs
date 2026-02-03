using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;

namespace Sapphire2026.Data.Models.GMAO
{
	/// <summary>
	/// Elemento simple perteneciente a la jerarquía de piezas de un tren.
	/// </summary>
	[Table("GMAODesc")]
	public class CCDesc
	{
		[Key]
		public string Id { get; set; } //Código único del taller para esta pieza
		public string Name { get; set; } //Nombre de la pieza
	}
}
