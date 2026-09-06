namespace Sapphire2025Models.Diamond
{
	/// <summary>Metadatos del catálogo Tourmaline (places.xml) en el servidor.</summary>
	public sealed class PlacesCatalogHeaderModel
	{
		public string ContentHash { get; set; } = string.Empty;

		public int ByteLength { get; set; }

		public DateTime UpdatedUtc { get; set; }

		public bool Exists { get; set; }
	}

	/// <summary>Contenido XML + cabecera para el editor y para el tren.</summary>
	public sealed class PlacesCatalogContentModel
	{
		public string Xml { get; set; } = string.Empty;

		public string ContentHash { get; set; } = string.Empty;

		public int ByteLength { get; set; }

		public DateTime UpdatedUtc { get; set; }
	}

	public sealed class PlacesCatalogSaveRequest
	{
		public Guid SessionToken { get; set; }

		public string Xml { get; set; } = string.Empty;
	}

	public sealed class PlacesCatalogSaveResult
	{
		public bool Success { get; set; }

		public string Message { get; set; } = string.Empty;

		public string ContentHash { get; set; } = string.Empty;

		public DateTime? UpdatedUtc { get; set; }

		public bool Changed { get; set; }

		/// <summary>Errores de formato. Si hay alguno, no se escribe el archivo.</summary>
		public List<PlacesXmlIssue> Errors { get; set; } = new List<PlacesXmlIssue>();
	}

	/// <summary>Un problema de formato de places.xml, con posición en el documento.</summary>
	public sealed class PlacesXmlIssue
	{
		public int Line { get; set; }

		public int Column { get; set; }

		public string Message { get; set; } = string.Empty;
	}
}
