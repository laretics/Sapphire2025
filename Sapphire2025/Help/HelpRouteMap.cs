namespace Sapphire2025.Help
{
	/// <summary>
	/// Asocia rutas de la aplicación con el identificador de tema de ayuda.
	/// </summary>
	public static class HelpRouteMap
	{
		/// <summary>
		/// Devuelve el id de tema de ayuda para la ruta actual, o null si no aplica
		/// (login, impresión, administración, centro de ayuda, etc.).
		/// </summary>
		public static string? ResolveTopicId(string relativePath)
		{
			if (string.IsNullOrWhiteSpace(relativePath))
				return "nav-basica";

			string path = relativePath.Trim();
			int q = path.IndexOf('?', StringComparison.Ordinal);
			if (q >= 0)
				path = path[..q];
			int hash = path.IndexOf('#', StringComparison.Ordinal);
			if (hash >= 0)
				path = path[..hash];

			path = path.Trim('/');
			if (path.Length == 0)
				return "nav-basica";

			// Normalizar mayúsculas de segmentos conocidos
			string lower = path.ToLowerInvariant();

			// Exclusiones: no mostrar ayuda contextual
			if (lower.StartsWith("help"))
				return null;
			if (lower.StartsWith("auth/"))
				return null;
			if (IsPrintRoute(lower))
				return null;
			if (IsAdminOnlyRoute(lower))
				return null;

			// Submenús por colectivo
			if (lower.StartsWith("submenu/"))
			{
				string page = lower["submenu/".Length..];
				return page switch
				{
					"inspector" => "listado-maquinistas",
					"station" => "listado-maquinistas",
					"caps" => "jefe-maquinistas",
					"mechanic" => "taller-flujo",
					"oficial" => "taller-flujo",
					"engineer" => "disponibilidad",
					"diamond" => "diamond-explotacion",
					"anonymous" => "mi-grafico",
					_ => "nav-basica"
				};
			}

			// Rutas concretas (orden: más específicas primero)
			if (lower.StartsWith("aeneas/incidencequery"))
				return "consulta-incidencias";
			if (lower.StartsWith("trainquery"))
				return "consulta-trenes";
			if (lower.StartsWith("trainflow"))
				return "taller-flujo";
			if (lower.StartsWith("train/"))
				return "dossier-tren";
			if (lower.StartsWith("drivers/schedules"))
				return "mi-grafico";
			if (lower.StartsWith("inspector/platformreport"))
				return "vias-plataforma";
			if (lower.StartsWith("timesnap/avail"))
				return "contrato-erion";
			if (lower.StartsWith("timesnap"))
				return "disponibilidad";
			if (lower.StartsWith("tourmalineservicemode") || lower.StartsWith("tourmaline"))
				return "tourmaline";
			if (lower.StartsWith("engineer/timenet")
				|| lower.StartsWith("engineer/topostorage")
				|| lower.StartsWith("admin/manageworkshifts"))
				return "diamond-explotacion";
			if (lower.StartsWith("admin/myuserdossier"))
				return "mi-perfil";
			if (lower.StartsWith("agentsctc")
				|| lower.StartsWith("dailygraph")
				|| lower.StartsWith("monthgraph")
				|| lower.StartsWith("instantsnap")
				|| lower.StartsWith("experts/"))
				return "listado-maquinistas";
			if (lower.StartsWith("communication"))
				return "nav-basica";

			// Por defecto: guía de navegación
			return "nav-basica";
		}

		private static bool IsPrintRoute(string lower) =>
			lower.Contains("print", StringComparison.Ordinal)
			|| lower.EndsWith("printview", StringComparison.Ordinal)
			|| lower.EndsWith("printpreview", StringComparison.Ordinal);

		private static bool IsAdminOnlyRoute(string lower) =>
			lower.StartsWith("admin/usersmanage")
			|| lower.StartsWith("admin/sapphirelog")
			|| lower.StartsWith("create-user")
			|| lower.StartsWith("edit-user");
	}
}
