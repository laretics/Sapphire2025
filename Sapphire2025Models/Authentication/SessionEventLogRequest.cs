using System;

namespace Sapphire2025Models.Authentication
{
	/// <summary>
	/// Petición del cliente para registrar un evento de actividad en SessionEvents.
	/// Solo se admiten tipos de evento autorizados en el servidor.
	/// </summary>
	public class SessionEventLogRequest : BasicRequestModel
	{
		/// <summary>Tipo de evento (byte de <see cref="Common.sessionEventType"/>).</summary>
		public byte EventType { get; set; }

		/// <summary>
		/// Detalle opcional (fecha, turno, CF consultado…).
		/// Se almacena junto al origen en hostPoint para poder filtrarlo en el log.
		/// </summary>
		public string? Detail { get; set; }
	}
}
