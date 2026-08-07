using System.IO.Compression;
using System.Text;
using Diamond.Timed;
using Diamond.Topo;
using Sapphire2025Models.Diamond;

namespace Sapphire2025.Storage
{
	/// <summary>
	/// Precarga las topologías Diamond del almacén Zafiro (API SapphireDiamond)
	/// en un <see cref="DictionaryTopoIncludeResolver"/> para que
	/// <c>include nombre</c> resuelva entradas del almacén.
	/// </summary>
	public sealed class DiamondStoreTopoCatalog
	{
		private readonly DiamondClient mvarClient;
		private DictionaryTopoIncludeResolver mvarResolver;
		private string mvarStatus;

		public DiamondStoreTopoCatalog(DiamondClient client)
		{
			mvarClient = client ?? throw new ArgumentNullException(nameof(client));
			mvarResolver = new DictionaryTopoIncludeResolver();
			mvarStatus = string.Empty;
		}

		public ITopoIncludeResolver Resolver
		{
			get { return mvarResolver; }
		}

		public int Count
		{
			get { return mvarResolver.Count; }
		}

		public string Status
		{
			get { return mvarStatus; }
		}

		public IReadOnlyList<string> DisplayNames
		{
			get { return mvarResolver.DisplayNames; }
		}

		/// <summary>
		/// Descarga las topologías activas del almacén y registra el resolvedor por defecto.
		/// </summary>
		public async Task RefreshAsync(bool activeOnly = true, CancellationToken cancellationToken = default)
		{
			DictionaryTopoIncludeResolver next = new DictionaryTopoIncludeResolver();
			IReadOnlyList<DiamondTopoHeaderModel> headers;
			try
			{
				headers = await mvarClient.ListToposAsync(activeOnly);
			}
			catch (Exception ex)
			{
				mvarStatus = "No se pudo listar el almacén Diamond: " + ex.Message;
				mvarResolver = next;
				TopoStorage.SetDefaultIncludeResolver(next.Count > 0 ? next : null);
				return;
			}

			int loaded = 0;
			int failed = 0;
			int i = 0;
			while (i < headers.Count)
			{
				cancellationToken.ThrowIfCancellationRequested();
				DiamondTopoHeaderModel header = headers[i];
				try
				{
					byte[]? payload = await mvarClient.DownloadTopoContentAsync(header.Id);
					if (payload is null || payload.Length == 0)
					{
						failed++;
						i++;
						continue;
					}

					byte[] xmlBytes = MaterializeXml(payload, header.Format);
					string xmlText = Encoding.UTF8.GetString(xmlBytes);
					TopoLayout layout;
					using (MemoryStream stream = new MemoryStream(xmlBytes, writable: false))
					{
						layout = TopoXmlSerializer.Load(stream);
					}

					string logical = PickLogicalName(header);
					string resolved = "zafiro:" + header.Id.ToString("N");
					TopoStorage storage = new TopoStorage(logical, resolved, layout);

					// Alias de include: fichero, nombre, layout id, guid corto.
					List<string> aliases = new List<string>();
					if (!string.IsNullOrWhiteSpace(header.SourceFileName))
					{
						aliases.Add(header.SourceFileName);
					}

					if (!string.IsNullOrWhiteSpace(header.Name))
					{
						aliases.Add(header.Name);
					}

					if (!string.IsNullOrWhiteSpace(header.LayoutId))
					{
						aliases.Add(header.LayoutId);
					}

					aliases.Add(header.Id.ToString("N"));
					aliases.Add(header.Id.ToString("D"));

					next.Add(storage, aliases.ToArray());

					// También catálogo de sesión (memoria) + XML para reexportar/subir.
					TopoStorage.RegisterInMemory(logical, layout, xmlText);
					int a = 0;
					while (a < aliases.Count)
					{
						TopoStorage.RegisterInMemory(aliases[a], layout, xmlText);
						a++;
					}

					loaded++;
				}
				catch
				{
					failed++;
				}

				i++;
			}

			mvarResolver = next;
			TopoStorage.SetDefaultIncludeResolver(next.Count > 0 ? next : null);

			if (headers.Count == 0)
			{
				mvarStatus = "Almacén Diamond vacío (ninguna topología activa).";
			}
			else
			{
				mvarStatus = string.Format(
					"Almacén Diamond: {0} topología(s) lista(s) para include{1}.",
					loaded,
					failed > 0 ? string.Format(", {0} error(es) al cargar", failed) : string.Empty);
			}
		}

		/// <summary>
		/// Añade o actualiza una entrada tras un upload exitoso (sin re-listar todo).
		/// </summary>
		public void RegisterUploaded(DiamondTopoHeaderModel header, string xmlText)
		{
			if (header is null || string.IsNullOrWhiteSpace(xmlText))
			{
				return;
			}

			using (MemoryStream stream = new MemoryStream(Encoding.UTF8.GetBytes(xmlText)))
			{
				TopoLayout layout = TopoXmlSerializer.Load(stream);
				string logical = PickLogicalName(header);
				TopoStorage storage = new TopoStorage(
					logical,
					"zafiro:" + header.Id.ToString("N"),
					layout);

				List<string> aliases = new List<string>();
				if (!string.IsNullOrWhiteSpace(header.SourceFileName))
				{
					aliases.Add(header.SourceFileName);
				}

				if (!string.IsNullOrWhiteSpace(header.Name))
				{
					aliases.Add(header.Name);
				}

				if (!string.IsNullOrWhiteSpace(header.LayoutId))
				{
					aliases.Add(header.LayoutId);
				}

				mvarResolver.Add(storage, aliases.ToArray());
				TopoStorage.RegisterInMemory(logical, layout, xmlText);
				int a = 0;
				while (a < aliases.Count)
				{
					TopoStorage.RegisterInMemory(aliases[a], layout, xmlText);
					a++;
				}

				TopoStorage.SetDefaultIncludeResolver(mvarResolver);
			}
		}

		private static string PickLogicalName(DiamondTopoHeaderModel header)
		{
			if (!string.IsNullOrWhiteSpace(header.SourceFileName))
			{
				return TopoStorage.EnsureXmlExtension(header.SourceFileName.Trim());
			}

			if (!string.IsNullOrWhiteSpace(header.Name))
			{
				return TopoStorage.EnsureXmlExtension(header.Name.Trim());
			}

			if (!string.IsNullOrWhiteSpace(header.LayoutId))
			{
				return TopoStorage.EnsureXmlExtension(header.LayoutId.Trim());
			}

			return TopoStorage.EnsureXmlExtension(header.Id.ToString("N"));
		}

		private static byte[] MaterializeXml(byte[] payload, string format)
		{
			if (string.Equals(format, "xml-gz", StringComparison.OrdinalIgnoreCase)
				|| (payload.Length >= 2 && payload[0] == 0x1f && payload[1] == 0x8b))
			{
				using MemoryStream input = new MemoryStream(payload, writable: false);
				using GZipStream gzip = new GZipStream(input, CompressionMode.Decompress);
				using MemoryStream output = new MemoryStream();
				gzip.CopyTo(output);
				return output.ToArray();
			}

			return payload;
		}
	}
}
