using System.ComponentModel.DataAnnotations.Schema;

namespace Sapphire2026.Data.Models
{
	[Table("Notes")]
	public class Note
	{
		public Guid Id { get; set; }
		public Guid Parent { get; set; } //Referencia al padre (al tren) que posee esta nota
		public DateTime TimeStamp { get; set; }
		public Guid UserId { get; set; }
		public string? Text { get; set; }
		public byte Type { get; set; } //Tipo de nota
		///Por el momento vamos a usar este tipo para recoger el tipo de
		///dato que posee este registro.
		///El cero va a ser un texto de anotación de un mecánico.
		///El uno será un parte de avería.
		

	}
}
