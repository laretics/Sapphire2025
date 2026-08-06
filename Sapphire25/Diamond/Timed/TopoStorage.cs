using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Diamond.Topo;

namespace Diamond.Timed
{
	/// <summary>
	/// Almacén de topología usado por un plan de malla: ruta del XML de Diamond y layout cargado.
	/// Se obtiene normalmente vía la directiva <c>include</c> del mini-DSL.
	/// En WASM no hay disco: se puede registrar topología en un catálogo en memoria.
	/// </summary>
	public sealed class TopoStorage
	{
		/// <summary>
		/// Catálogo en memoria (nombre lógico → layout), p. ej. "toposfm227.xml".
		/// Claves sin y con extensión .xml.
		/// </summary>
		private static readonly Dictionary<string, TopoLayout> scolMemoryLayouts =
			new Dictionary<string, TopoLayout>(StringComparer.OrdinalIgnoreCase);

		/// <summary>XML original por clave (para reexportar / subir al servidor).</summary>
		private static readonly Dictionary<string, string> scolMemoryXml =
			new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

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
		/// Registra un layout en memoria para que <c>include nombre</c> lo resuelva sin File.Exists.
		/// </summary>
		public static void RegisterInMemory(string logicalName, TopoLayout layout, string? xmlSource = null)
		{
			if (layout is null)
			{
				throw new ArgumentNullException(nameof(layout));
			}

			string key = EnsureXmlExtension(logicalName);
			if (key.Length == 0)
			{
				throw new ArgumentException("Nombre lógico de topología vacío.", nameof(logicalName));
			}

			string bare = System.IO.Path.GetFileNameWithoutExtension(key);
			string fileOnly = System.IO.Path.GetFileName(key);
			scolMemoryLayouts[key] = layout;
			scolMemoryLayouts[fileOnly] = layout;
			if (bare.Length > 0)
			{
				scolMemoryLayouts[bare] = layout;
			}

			if (!string.IsNullOrEmpty(xmlSource))
			{
				scolMemoryXml[key] = xmlSource;
				scolMemoryXml[fileOnly] = xmlSource;
				if (bare.Length > 0)
				{
					scolMemoryXml[bare] = xmlSource;
				}
			}
		}

		/// <summary>Carga XML desde texto y lo registra en memoria bajo <paramref name="logicalName"/>.</summary>
		public static TopoStorage LoadFromXmlText(string logicalName, string xmlText)
		{
			if (string.IsNullOrWhiteSpace(xmlText))
			{
				throw new ArgumentException("XML de topología vacío.", nameof(xmlText));
			}

			using MemoryStream stream = new MemoryStream(Encoding.UTF8.GetBytes(xmlText));
			TopoLayout layout = TopoXmlSerializer.Load(stream);
			string key = EnsureXmlExtension(logicalName);
			if (key.Length == 0)
			{
				key = "topo.xml";
			}

			RegisterInMemory(key, layout, xmlText);
			return new TopoStorage(key, "memory:" + key, layout);
		}

		/// <summary>True si hay al menos una topología en el catálogo en memoria.</summary>
		public static bool HasMemoryCatalog
		{
			get { return scolMemoryLayouts.Count > 0; }
		}

		/// <summary>Obtiene el XML en memoria si se registró con origen textual.</summary>
		public static string? TryGetMemoryXml(string logicalName)
		{
			string key = EnsureXmlExtension(logicalName);
			if (key.Length > 0 && scolMemoryXml.TryGetValue(key, out string? xml))
			{
				return xml;
			}

			string fileOnly = System.IO.Path.GetFileName(key);
			if (fileOnly.Length > 0 && scolMemoryXml.TryGetValue(fileOnly, out xml))
			{
				return xml;
			}

			string bare = System.IO.Path.GetFileNameWithoutExtension(key);
			if (bare.Length > 0 && scolMemoryXml.TryGetValue(bare, out xml))
			{
				return xml;
			}

			return null;
		}

		private static bool TryGetFromMemory(string logical, out TopoStorage? storage)
		{
			storage = null;
			string key = EnsureXmlExtension(logical);
			string fileOnly = System.IO.Path.GetFileName(key);
			string bare = System.IO.Path.GetFileNameWithoutExtension(key);

			TopoLayout? layout = null;
			if (scolMemoryLayouts.TryGetValue(key, out layout)
				|| scolMemoryLayouts.TryGetValue(fileOnly, out layout)
				|| (bare.Length > 0 && scolMemoryLayouts.TryGetValue(bare, out layout)))
			{
				if (layout is not null)
				{
					storage = new TopoStorage(key, "memory:" + fileOnly, layout);
					return true;
				}
			}

			return false;
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

			// 1) Catálogo en memoria (WASM / host que precargó el XML).
			if (TryGetFromMemory(logical, out storage) && storage is not null)
			{
				return true;
			}

			// 2) Disco (Diamond.Web server, tests, herramientas).
			string resolved;
			try
			{
				resolved = ResolvePath(logical, baseDirectory);
			}
			catch (Exception ex)
			{
				// En browser Path.GetFullPath puede fallar; si hay memoria, ya se intentó.
				error = "ruta de topología no válida '" + logical + "': " + ex.Message
					+ " (y no hay copia en memoria; use Cargar topo o precargue samples).";
				return false;
			}

			if (!File.Exists(resolved))
			{
				error = "no se encontró el XML de topología '" + resolved
					+ "'. En el navegador cargue el topo (botón Topo…) o deje Samples/Onice en wwwroot.";
				return false;
			}

			try
			{
				TopoLayout layout = TopoXmlSerializer.Load(resolved);
				storage = new TopoStorage(logical, resolved, layout);
				// Cache para includes posteriores sin re-leer disco.
				RegisterInMemory(logical, layout);
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
