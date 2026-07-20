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
		
		public DateTime? ClosureTime{ get; set; }
		//Fecha y hora del cierre de la nota.
		public Guid? ClosureUser{ get; set; }
		//Usuario que cierra la nota.

		/// <summary>
		/// Campos para el etiquetado de las notas.
		/// Es el primer paso para el procesamiento mediante modelos del lenguaje natural.
		/// </summary>
		public bool IsValid { get; set; } //Es una nota válida para el procesamiento.
		public bool IsSympthom { get; set; } //Este registro describe o informa de una avería. (En caso contrario sería una resolución)
		public byte SystemAffected { get; set; } //Sistema del tren afectado.

	}
}
