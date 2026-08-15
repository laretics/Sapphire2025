using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sapphire2025Models.Aeneas
{
	/// <summary>
	/// Anotación sobre un tren para añadir a la base de datos
	/// Una nota puede ser un parte de avería, unas notas o contenido multimedia
	/// Las notas, de momento, van referidas a un tren, pero como son Guid, nada
	/// me impide que se puedan referir a otro tipo de objetos, o incluso a otras notas.
	/// </summary>
	public class NoteModel:BasicRequestModel
	{
		public Guid Id { get; set; }
		public string? Text { get; set; } //Texto de la nota
		public Guid parent { get; set; } //Referencia al tren que posee esta nota
		public byte Type { get; set; } //Tipo de nota
		public DateTime TimeStamp { get; set; } //Fecha y hora de la nota
		public Guid UserId { get; set; } //Referencia al usuario que ha creado la nota

		public DateTime? ClosureTime { get; set; }
		//Fecha y hora del cierre de la nota.
		public Guid? ClosureUser { get; set; }
		//Usuario que cierra la nota.

		/// <summary>Etiqueta: nota válida para procesamiento NLP / análisis.</summary>
		public bool IsValid { get; set; }
		/// <summary>Etiqueta: describe un síntoma/avería (true) o una resolución (false).</summary>
		public bool IsSymptom { get; set; }
		/// <summary>Etiqueta: sistema del tren afectado (<see cref="Common.TrainSystem"/>).</summary>
		public byte SystemAffected { get; set; }

		/// <summary>Extensión del adjunto (sin punto). Vacío = no hay fichero.</summary>
		public string? MediaExtension { get; set; }
		/// <summary>MIME del adjunto.</summary>
		public string? MediaContentType { get; set; }

		public bool HasMedia =>
			Type == 4 || !string.IsNullOrWhiteSpace(MediaExtension);
	}
}
