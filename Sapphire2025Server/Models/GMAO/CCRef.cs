using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Sapphire2025Server.Models.GMAO
{
	/// <summary>
	/// Referencia a un elemento.
	/// Es el equivalente a un puntero.
	/// </summary>
	[Table("GMAORef")]
	public class CCRef
	{
		[Key]
		public Guid Id { get; set; } //Código único de la referencia.
		public string CCId { get; set; } //Referencia de la pieza (de CCDesc)
		public string? Name { get; set; } //Nombre de la ubicación.
		//IMPORTANTE: Este nombre NO es el de la pieza, sino el que da sentido a su posición
		//	en el tren o en la pieza que lo contiene. Por ejemplo, si la pieza es "faldón"
		//	Name puede ser "Faldón izquierdo" o "Faldón derecho", porque describe su función o
		//	su ubicación.
		public Guid ParentId { get; set; } //Referencia de la pieza que contiene esta referencia
		public int Position { get; set; } //Descripción numérica de la posición. En el caso de los faldones, tendré que la posición del faldón izquierdo es 1 y la del derecho es 2.
	}
}
