namespace Sapphire2025Models.Aeneas
{
	/// <summary>
	/// Petición de etiquetado manual de una nota (entrenamiento del clasificador).
	/// Al aceptar, el servidor marca IsValid = true.
	/// </summary>
	public class NoteLabelRequestModel : BasicRequestModel
	{
		public Guid NoteId { get; set; }
		public bool IsSymptom { get; set; }
		public byte SystemAffected { get; set; }

		public NoteLabelRequestModel() : base()
		{
			NoteId = Guid.Empty;
		}

		public NoteLabelRequestModel(Guid token, Guid noteId, bool isSymptom, byte systemAffected)
			: base(token)
		{
			NoteId = noteId;
			IsSymptom = isSymptom;
			SystemAffected = systemAffected;
		}
	}
}
