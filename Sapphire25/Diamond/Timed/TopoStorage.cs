using System;
using System.IO;
using Diamond.Topo;

namespace Diamond.Timed
{
	/// <summary>
	/// Almacén de topología usado por un plan de malla: ruta del XML de Diamond y layout cargado.
	/// Se obtiene normalmente vía la directiva <c>include</c> del mini-DSL.
	/// </summary>
	public sealed class TopoStorage
	{
		private readonly string mvarPath;
		private readonly string mvarResolvedPath;
		private readonly TopoLayout mvarLayout;

		public TopoStorage(string path, string resolvedPath, TopoLayout layout)
		{
			if (layout is null)
			{
				throw new ArgumentNullException(nameof(layout));
			}

			mvarPath = path ?? string.Empty;
			mvarResolvedPath = resolvedPath ?? string.Empty;
			mvarLayout = layout;
		}

		/// <summary>
		/// Ruta tal como aparece en el script (<c>include</c>), sin normalizar.
		/// </summary>
		public string Path
		{
			get { return mvarPath; }
		}

		/// <summary>
		/// Ruta absoluta resuelta desde la que se cargó el XML.
		/// </summary>
		public string ResolvedPath
		{
			get { return mvarResolvedPath; }
		}

		/// <summary>
		/// Topología en memoria.
		/// </summary>
		public TopoLayout Layout
		{
			get { return mvarLayout; }
		}

		/// <summary>
		/// Carga un XML de topología Diamond (canónico o legacy Onice).
		/// </summary>
		/// <param name="path">Ruta absoluta o relativa al directorio base.</param>
		/// <param name="baseDirectory">
		/// Directorio base para rutas relativas (p. ej. carpeta del script o de samples).
		/// Si es null o vacío, se resuelve respecto al directorio de trabajo actual.
		/// </param>
		public static TopoStorage LoadFromXml(string path, string? baseDirectory = null)
		{
			string? error;
			TopoStorage? storage;
			if (!TryLoadFromXml(path, baseDirectory, out storage, out error) || storage is null)
			{
				throw new InvalidOperationException(error ?? "No se pudo cargar la topología.");
			}

			return storage;
		}

		/// <summary>
		/// Intenta cargar el XML de topología. No lanza: devuelve el error en <paramref name="error"/>.
		/// </summary>
		public static bool TryLoadFromXml(
			string path,
			string? baseDirectory,
			out TopoStorage? storage,
			out string? error)
		{
			storage = null;
			error = null;

			if (string.IsNullOrWhiteSpace(path))
			{
				error = "ruta de topología vacía.";
				return false;
			}

			string logical = EnsureXmlExtension(path);
			string resolved;
			try
			{
				resolved = ResolvePath(logical, baseDirectory);
			}
			catch (Exception ex)
			{
				error = "ruta de topología no válida '" + logical + "': " + ex.Message;
				return false;
			}

			if (!File.Exists(resolved))
			{
				error = "no se encontró el XML de topología '" + resolved + "'.";
				return false;
			}

			try
			{
				TopoLayout layout = TopoXmlSerializer.Load(resolved);
				storage = new TopoStorage(logical, resolved, layout);
				return true;
			}
			catch (Exception ex)
			{
				error = "error al cargar topología '" + resolved + "': " + ex.Message;
				return false;
			}
		}

		/// <summary>
		/// Asegura la extensión <c>.xml</c>. El include admite solo el nombre de la topología
		/// (<c>include toposfm227</c> → <c>toposfm227.xml</c>). Si la ruta ya termina en
		/// <c>.xml</c> (mayúsculas o minúsculas), no se modifica.
		/// </summary>
		public static string EnsureXmlExtension(string path)
		{
			if (path is null)
			{
				return string.Empty;
			}

			string trimmed = path.Trim();
			if (trimmed.Length == 0)
			{
				return string.Empty;
			}

			if (trimmed.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
			{
				return trimmed;
			}

			return trimmed + ".xml";
		}

		/// <summary>
		/// Resuelve una ruta de include a ruta absoluta (añade <c>.xml</c> si falta).
		/// </summary>
		public static string ResolvePath(string path, string? baseDirectory)
		{
			if (path is null)
			{
				throw new ArgumentNullException(nameof(path));
			}

			string trimmed = EnsureXmlExtension(path);
			if (trimmed.Length == 0)
			{
				throw new ArgumentException("La ruta está vacía.", nameof(path));
			}

			if (System.IO.Path.IsPathRooted(trimmed))
			{
				return System.IO.Path.GetFullPath(trimmed);
			}

			if (!string.IsNullOrWhiteSpace(baseDirectory))
			{
				return System.IO.Path.GetFullPath(System.IO.Path.Combine(baseDirectory.Trim(), trimmed));
			}

			return System.IO.Path.GetFullPath(trimmed);
		}

		public override string ToString()
		{
			if (mvarResolvedPath.Length > 0)
			{
				return mvarResolvedPath;
			}

			return mvarPath.Length > 0 ? mvarPath : "TopoStorage";
		}
	}
}
