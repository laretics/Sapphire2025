using System;
using System.Collections.Generic;
using System.Text;

namespace Diamond.Timed
{
	/// <summary>
	/// Resolvedor de <c>include</c> basado en un diccionario en memoria
	/// (nombre lógico → <see cref="TopoStorage"/>). Típico de un almacén Zafiro precargado.
	/// </summary>
	public sealed class DictionaryTopoIncludeResolver : ITopoIncludeResolver
	{
		private readonly Dictionary<string, TopoStorage> mcolByKey;
		private readonly List<string> mcolDisplayNames;

		public DictionaryTopoIncludeResolver()
		{
			mcolByKey = new Dictionary<string, TopoStorage>(StringComparer.OrdinalIgnoreCase);
			mcolDisplayNames = new List<string>();
		}

		public int Count
		{
			get { return mcolDisplayNames.Count; }
		}

		public IReadOnlyList<string> DisplayNames
		{
			get { return mcolDisplayNames; }
		}

		/// <summary>
		/// Registra un storage bajo varias claves (nombre, fichero, id de layout, etc.).
		/// </summary>
		public void Add(TopoStorage storage, params string[] aliases)
		{
			if (storage is null)
			{
				throw new ArgumentNullException(nameof(storage));
			}

			string primary = storage.Path;
			if (primary.Length > 0)
			{
				AddKey(primary, storage);
			}

			int i = 0;
			while (i < aliases.Length)
			{
				string alias = aliases[i];
				if (!string.IsNullOrWhiteSpace(alias))
				{
					AddKey(alias.Trim(), storage);
				}

				i++;
			}

			string display = primary.Length > 0 ? primary : storage.ResolvedPath;
			if (display.Length == 0)
			{
				display = "topo";
			}

			if (!mcolDisplayNames.Contains(display))
			{
				mcolDisplayNames.Add(display);
			}
		}

		private void AddKey(string rawKey, TopoStorage storage)
		{
			string key = TopoStorage.EnsureXmlExtension(rawKey);
			if (key.Length == 0)
			{
				return;
			}

			mcolByKey[key] = storage;
			string fileOnly = System.IO.Path.GetFileName(key);
			if (fileOnly.Length > 0)
			{
				mcolByKey[fileOnly] = storage;
			}

			string bare = System.IO.Path.GetFileNameWithoutExtension(key);
			if (bare.Length > 0)
			{
				mcolByKey[bare] = storage;
				mcolByKey[TopoStorage.EnsureXmlExtension(bare)] = storage;
			}

			// También la clave cruda por si el include usa un nombre sin .xml “raro”.
			mcolByKey[rawKey] = storage;
		}

		public bool TryResolve(string logicalName, out TopoStorage? storage, out string? error)
		{
			storage = null;
			error = null;
			if (string.IsNullOrWhiteSpace(logicalName))
			{
				error = "nombre de topología vacío.";
				return false;
			}

			string logical = TopoStorage.EnsureXmlExtension(logicalName);
			if (mcolByKey.TryGetValue(logical, out TopoStorage? found) && found is not null)
			{
				storage = found;
				return true;
			}

			string fileOnly = System.IO.Path.GetFileName(logical);
			if (fileOnly.Length > 0
				&& mcolByKey.TryGetValue(fileOnly, out found)
				&& found is not null)
			{
				storage = found;
				return true;
			}

			string bare = System.IO.Path.GetFileNameWithoutExtension(logical);
			if (bare.Length > 0
				&& mcolByKey.TryGetValue(bare, out found)
				&& found is not null)
			{
				storage = found;
				return true;
			}

			if (mcolByKey.TryGetValue(logicalName.Trim(), out found) && found is not null)
			{
				storage = found;
				return true;
			}

			error = "no está en el almacén de topologías.";
			return false;
		}

		public string? FormatAvailableHint()
		{
			if (mcolDisplayNames.Count == 0)
			{
				return "ninguna topología cargada en el almacén.";
			}

			StringBuilder sb = new StringBuilder();
			sb.Append("disponibles: ");
			int i = 0;
			int max = mcolDisplayNames.Count;
			if (max > 12)
			{
				max = 12;
			}

			while (i < max)
			{
				if (i > 0)
				{
					sb.Append(", ");
				}

				sb.Append(mcolDisplayNames[i]);
				i++;
			}

			if (mcolDisplayNames.Count > max)
			{
				sb.Append("… (+");
				sb.Append((mcolDisplayNames.Count - max).ToString());
				sb.Append(')');
			}

			return sb.ToString();
		}
	}
}
