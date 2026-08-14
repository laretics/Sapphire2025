using System.Collections.Generic;
using System.Linq;

namespace Sapphire2025Models.Authentication
{
	/// <summary>
	/// Petición del cliente para comprobar si debe actualizar la web
	/// en función de la versión local y los roles de la sesión.
	/// </summary>
	public class VersionCheckRequest : BasicRequestModel
	{
		/// <summary>Versión compilada en el cliente WASM.</summary>
		public string ClientVersion { get; set; } = string.Empty;
	}

	/// <summary>
	/// Una nota de versión aplicable al usuario (título corto + detalle opcional).
	/// </summary>
	public class VersionChangeNote
	{
		/// <summary>Resumen del cambio (lista principal).</summary>
		public string Text { get; set; } = string.Empty;

		/// <summary>
		/// Observaciones / detalle ampliado. Si está vacío, no se ofrece «Más info».
		/// </summary>
		public string Observations { get; set; } = string.Empty;

		public bool HasObservations => !string.IsNullOrWhiteSpace(Observations);
	}

	/// <summary>
	/// Respuesta del servidor con la política de actualización filtrada por roles.
	/// </summary>
	public class VersionCheckResponse
	{
		public VersionCheckResponse()
		{
			Changes = new List<VersionChangeNote>();
		}

		/// <summary>Versión del servidor (referencia actual del despliegue).</summary>
		public string ServerVersion { get; set; } = string.Empty;

		/// <summary>True si la versión del cliente no coincide con la del servidor.</summary>
		public bool VersionMismatch { get; set; }

		/// <summary>
		/// True si el usuario debe recargar (Ctrl+F5): hay desfase de versión
		/// y al menos un cambio que le afecta y exige actualización.
		/// </summary>
		public bool NeedsUpdate { get; set; }

		/// <summary>
		/// Cambios aplicables a los roles del usuario (texto + observaciones).
		/// </summary>
		public List<VersionChangeNote> Changes { get; set; }

		/// <summary>
		/// Compatibilidad: solo los textos (misma información que Changes.Text).
		/// </summary>
		public List<string> Notes
		{
			get => Changes?.Select(c => c.Text).Where(t => !string.IsNullOrWhiteSpace(t)).ToList()
				?? new List<string>();
			set
			{
				// Deserialización antigua o asignación simple: rellena Changes sin observaciones.
				if (value is null || value.Count == 0)
					return;
				if (Changes is null)
					Changes = new List<VersionChangeNote>();
				if (Changes.Count == 0)
				{
					foreach (string t in value)
					{
						if (!string.IsNullOrWhiteSpace(t))
							Changes.Add(new VersionChangeNote { Text = t });
					}
				}
			}
		}
	}
}
