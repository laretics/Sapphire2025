namespace Sapphire2025Models.Authentication
{
	/// <summary>
	/// Broadcast de Telegram con adjunto (foto, vídeo o documento).
	/// El worker lee el fichero de <see cref="MediaPath"/> (misma máquina que el API).
	/// </summary>
	public class TelegramMediaBroadcastModel
	{
		public string Message { get; set; } = string.Empty;
		public bool Priority { get; set; }
		public Common.UserRole[] Roles { get; set; } = Array.Empty<Common.UserRole>();
		public string? CatalogKey { get; set; }
		public string[]? Args { get; set; }
		public string? MediaPath { get; set; }
		/// <summary>photo | video | animation | document</summary>
		public string MediaKind { get; set; } = "document";
		public string? FileName { get; set; }
	}
}
