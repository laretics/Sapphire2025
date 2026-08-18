using System;
using System.Collections.Generic;

namespace Sapphire2025Models.Aeneas
{
	/// <summary>
	/// Petición de consulta compleja de notas e incidencias (cambios de estado).
	/// </summary>
	public class IncidenceQueryRequest : BasicRequestModel
	{
		public IncidenceQueryRequest()
		{
			TrainIds = new List<Guid>();
			UserIds = new List<Guid>();
			NoteTypes = new List<byte>();
			Keywords = new List<string>();
			SystemsAffected = new List<byte>();
			Statuses = new List<byte>();
			Operations = new List<byte>();
			IncludeNotes = true;
			IncludeStatusChanges = true;
			MaxRecords = 500;
		}

		public IncidenceQueryRequest(Guid token) : this()
		{
			SessionToken = token;
		}

		/// <summary>Inicio del intervalo (inclusive), preferiblemente UTC.</summary>
		public DateTime? FromUtc { get; set; }

		/// <summary>Fin del intervalo (inclusive), preferiblemente UTC.</summary>
		public DateTime? ToUtc { get; set; }

		/// <summary>Filtro por trenes. Vacío = todos.</summary>
		public List<Guid> TrainIds { get; set; }

		/// <summary>Filtro por usuarios autores. Vacío = todos.</summary>
		public List<Guid> UserIds { get; set; }

		/// <summary>Incluir notas (tabla Notes).</summary>
		public bool IncludeNotes { get; set; }

		/// <summary>Incluir cambios de estado (tabla StatusChanges).</summary>
		public bool IncludeStatusChanges { get; set; }

		/// <summary>Tipos de nota (0=mecánico, 1=parte, 2=info, 3=técnica). Vacío = todos.</summary>
		public List<byte> NoteTypes { get; set; }

		/// <summary>Palabras clave en el texto de la nota (AND). Solo aplica a notas.</summary>
		public List<string> Keywords { get; set; }

		/// <summary>Etiqueta IsValid. null = sin filtro.</summary>
		public bool? IsValid { get; set; }

		/// <summary>Etiqueta IsSymptom. null = sin filtro.</summary>
		public bool? IsSymptom { get; set; }

		/// <summary>Sistemas afectados (TrainSystem). Vacío = todos.</summary>
		public List<byte> SystemsAffected { get; set; }

		/// <summary>Estados resultantes del cambio (TrainStatus). Vacío = todos.</summary>
		public List<byte> Statuses { get; set; }

		/// <summary>Operaciones de cambio de estado (OperationType). Vacío = todas.</summary>
		public List<byte> Operations { get; set; }

		/// <summary>Máximo de registros a devolver por tipo (1..5000). 0 se interpreta como 500.</summary>
		public int MaxRecords { get; set; }
	}

	/// <summary>
	/// Un ítem unificado de la consulta (nota o cambio de estado).
	/// </summary>
	public class IncidenceQueryItem
	{
		/// <summary>"note" o "status".</summary>
		public string Kind { get; set; } = "note";

		public Guid Id { get; set; }
		public DateTime TimeStamp { get; set; }
		public Guid TrainId { get; set; }
		public string TrainName { get; set; } = string.Empty;
		public Guid UserId { get; set; }
		public string UserName { get; set; } = string.Empty;
		public string UserCf { get; set; } = string.Empty;

		// Nota
		public byte? NoteType { get; set; }
		public string? Text { get; set; }
		public bool? IsValid { get; set; }
		public bool? IsSymptom { get; set; }
		public byte? SystemAffected { get; set; }
		public DateTime? ClosureTime { get; set; }
		public Guid? ClosureUserId { get; set; }

		// Cambio de estado
		public byte? Operation { get; set; }
		public byte? Status { get; set; }

		public string KindLabel =>
			string.Equals(Kind, "status", StringComparison.OrdinalIgnoreCase) ? "Cambio de estado" : "Nota";

		public string NoteTypeName =>
			NoteType.HasValue ? Common.NoteTypeName(NoteType.Value) : string.Empty;

		public string SystemName =>
			SystemAffected.HasValue ? Common.TrainSystemName((Common.TrainSystem)SystemAffected.Value) : string.Empty;

		public string StatusName =>
			Status.HasValue ? Common.TrainStatusToString((Common.TrainStatus)Status.Value) : string.Empty;

		public string OperationName =>
			Operation.HasValue ? Common.OperationTypeName((Common.OperationType)Operation.Value) : string.Empty;
	}

	/// <summary>
	/// Respuesta de la consulta de incidencias.
	/// </summary>
	public class IncidenceQueryResponse
	{
		public IncidenceQueryResponse()
		{
			Items = new List<IncidenceQueryItem>();
		}

		public List<IncidenceQueryItem> Items { get; set; }
		public int TotalNotes { get; set; }
		public int TotalStatusChanges { get; set; }
		public bool Truncated { get; set; }

		public int TotalMatched => TotalNotes + TotalStatusChanges;
	}

	/// <summary>
	/// Paquete para la vista de impresión de la consulta de incidencias.
	/// </summary>
	public class IncidenceQueryPrintPackage
	{
		public IncidenceQueryPrintPackage()
		{
			Items = new List<IncidenceQueryItem>();
			FilterSummary = string.Empty;
			GeneratedAt = DateTime.Now;
		}

		public List<IncidenceQueryItem> Items { get; set; }
		public string FilterSummary { get; set; }
		public DateTime GeneratedAt { get; set; }
		public int TotalNotes { get; set; }
		public int TotalStatusChanges { get; set; }
		public bool Truncated { get; set; }

		/// <summary>Si false, la impresión oculta la columna de usuario.</summary>
		public bool ShowUsers { get; set; } = true;
	}
}
