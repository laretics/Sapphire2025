using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Sapphire2025Server.Models
{
	[Table("Trains")]
	public class Train
	{
		private const int SHORT_COMMENT_LENGTH = 16;
		[Key]
		public Guid Guid { get; set; } //Referencia interna de este tren en la base de datos
		[Display(Name = "Id")]
		public int GmaoId { get; set; } //Referencia de la unidad en la parte de mantenimiento.                
		[Display(Name = "UT")]
		public string Name { get; set; } //Denominación pública de este tren
		public string NameCloud { get; set; } //Nube de cadenas que se detectan en esta unidad (para intérprete de telegram)
		[Display(Name = "Notas")]
		public string Comment { get; set; } //Comentario permanente sobre este tren
		public byte LastStatus { get; set; } //Caché del último estado registrado en este tren
		public Guid lastChange { get; set; } //Última operación de cambio de estado del tren.

		public Train()
		{
			Guid = Guid.NewGuid();
			Name = string.Format("New {0}", Guid.ToString()[8..]);
			Comment = string.Empty;
			NameCloud = string.Empty;
		}
	}
}
