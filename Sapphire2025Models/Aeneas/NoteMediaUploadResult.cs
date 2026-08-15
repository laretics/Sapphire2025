namespace Sapphire2025Models.Aeneas
{
	public class NoteMediaUploadResult
	{
		public bool Success { get; set; }
		public string Message { get; set; } = string.Empty;
		public NoteModel? Note { get; set; }
	}
}
