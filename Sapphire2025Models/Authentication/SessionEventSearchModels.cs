using System;
using System.Collections.Generic;

namespace Sapphire2025Models.Authentication
{
	/// <summary>
	/// Petición de búsqueda avanzada sobre el log de eventos de sesión (SessionEvents).
	/// </summary>
	public class SessionEventSearchRequest : BasicRequestModel
	{
		public SessionEventSearchRequest()
		{
			UserIds = new List<string>();
			EventTypes = new List<byte>();
			MaxRecords = 500;
		}

		/// <summary>Inicio del intervalo (inclusive), preferiblemente UTC.</summary>
		public DateTime? FromUtc { get; set; }

		/// <summary>Fin del intervalo (inclusive), preferiblemente UTC.</summary>
		public DateTime? ToUtc { get; set; }

		/// <summary>Filtro por uno o varios usuarios (Id string / Guid). Vacío = todos.</summary>
		public List<string> UserIds { get; set; }

		/// <summary>Filtro por tipos de evento (byte del enum sessionEventType). Vacío = todos.</summary>
		public List<byte> EventTypes { get; set; }

		/// <summary>Texto contenido en hostPoint (IP u origen). Opcional.</summary>
		public string? HostContains { get; set; }

		/// <summary>Máximo de registros a devolver (1..5000). 0 se interpreta como 500.</summary>
		public int MaxRecords { get; set; }
	}

	/// <summary>
	/// Respuesta de la búsqueda avanzada de eventos.
	/// </summary>
	public class SessionEventSearchResponse
	{
		public SessionEventSearchResponse()
		{
			Records = new List<SessionEventRecordModel>();
		}

		public List<SessionEventRecordModel> Records { get; set; }

		/// <summary>Total de filas que cumplen el filtro (antes del Take).</summary>
		public int TotalMatched { get; set; }

		/// <summary>True si TotalMatched &gt; Records.Count (hay más de los devueltos).</summary>
		public bool Truncated { get; set; }
	}

	/// <summary>
	/// Un registro del log de actividad listo para UI / CSV / impresión.
	/// </summary>
	public class SessionEventRecordModel
	{
		public string Id { get; set; } = string.Empty;
		public string UserId { get; set; } = string.Empty;
		public byte EventType { get; set; }
		public DateTime TimeStamp { get; set; }
		public string HostPoint { get; set; } = string.Empty;

		public Common.sessionEventType Type
		{
			get => (Common.sessionEventType)EventType;
			set => EventType = (byte)value;
		}

		public string TypeName => Common.SessionEventTypeName(Type);
	}

	/// <summary>
	/// Paquete que se guarda en IntStorage para la vista de impresión del log.
	/// </summary>
	public class SessionEventPrintPackage
	{
		public SessionEventPrintPackage()
		{
			Records = new List<SessionEventRecordModel>();
			FilterSummary = string.Empty;
			GeneratedAt = DateTime.Now;
		}

		public List<SessionEventRecordModel> Records { get; set; }
		public string FilterSummary { get; set; }
		public DateTime GeneratedAt { get; set; }
		public int TotalMatched { get; set; }
		public bool Truncated { get; set; }
	}
}
