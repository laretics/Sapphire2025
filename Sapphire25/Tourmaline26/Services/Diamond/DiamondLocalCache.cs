using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using Diamond.Cabin;
using Diamond.Project;
using Diamond.Topo;
using Microsoft.EntityFrameworkCore;
using Sapphire2025.Storage;
using Sapphire2025Models.Diamond;
using Tourmaline26.Logic;
using Tourmaline26.Services.LocalDataModel;

namespace Tourmaline26.Services.CabinCache
{
	/// <summary>
	/// Caché local de topología y planes publicados Diamond (ficheros + metadatos SQLite).
	/// </summary>
	public sealed class DiamondLocalCache
	{
		public const string CacheRootRelative = "cache/diamond";

		private readonly IServiceProvider mvarServices;
		private readonly ILogger<DiamondLocalCache> mvarLogger;
		private readonly string mvarRootPath;

		public DiamondLocalCache(
			IServiceProvider services,
			ILogger<DiamondLocalCache> logger,
			IHostEnvironment? hostEnvironment = null)
		{
			mvarServices = services;
			mvarLogger = logger;
			string baseDir = hostEnvironment?.ContentRootPath
				?? AppContext.BaseDirectory;
			mvarRootPath = Path.Combine(baseDir, CacheRootRelative);
			Directory.CreateDirectory(Path.Combine(mvarRootPath, "topo"));
			Directory.CreateDirectory(Path.Combine(mvarRootPath, "plans"));
		}

		public string RootPath
		{
			get { return mvarRootPath; }
		}

		/// <summary>
		/// Sincroniza topo (por ContentHash) y planes publicados con ValidFrom ≥ hoy (y sin caducar).
		/// </summary>
		public async Task<DiamondSyncResult> SyncFromServerAsync(
			DiamondClient client,
			Guid topoId,
			CancellationToken cancellationToken = default)
		{
			DiamondSyncResult result = new DiamondSyncResult();
			if (Guid.Empty.Equals(topoId))
			{
				result.Success = false;
				result.Message = "Diamond:TopoId no configurado en appsettings.";
				return result;
			}

			try
			{
				DiamondTopoHeaderModel? remoteTopo = await client.GetTopoAsync(topoId);
				if (remoteTopo is null)
				{
					result.Success = false;
					result.Message = "Topología no encontrada en el servidor (" + topoId + ").";
					return result;
				}

				result.TopoHeader = remoteTopo;
				TopoEnsureResult topoEnsure = await EnsureTopoAsync(client, remoteTopo, cancellationToken);
				result.TopoDownloaded = topoEnsure.Downloaded;
				if (!topoEnsure.Ok)
				{
					result.Success = false;
					result.Message = "No se pudo asegurar la topología en caché.";
					return result;
				}

				IReadOnlyList<DiamondPublishedPlanHeaderModel> plans =
					await client.ListPublishedPlansAsync(activeOnly: true);
				DateTime today = DateTime.Today;
				List<DiamondPublishedPlanHeaderModel> relevant = new List<DiamondPublishedPlanHeaderModel>();
				int i = 0;
				while (i < plans.Count)
				{
					DiamondPublishedPlanHeaderModel p = plans[i];
					if (p.TopoId != topoId)
					{
						i++;
						continue;
					}

					// Desde la fecha actual en adelante: vigente hoy o con ValidFrom >= hoy.
					bool endsInFuture = !p.ValidTo.HasValue || p.ValidTo.Value.Date >= today;
					bool startsOk = p.ValidFrom.Date >= today
						|| (p.ValidFrom.Date <= today && endsInFuture);
					if (startsOk && endsInFuture)
					{
						relevant.Add(p);
					}

					i++;
				}

				int downloaded = 0;
				int skipped = 0;
				int pi = 0;
				while (pi < relevant.Count)
				{
					bool wasNew = await EnsurePublishedPlanAsync(
						client,
						relevant[pi],
						cancellationToken);
					if (wasNew)
					{
						downloaded++;
					}
					else
					{
						skipped++;
					}

					pi++;
				}

				result.PlansConsidered = relevant.Count;
				result.PlansDownloaded = downloaded;
				result.PlansUnchanged = skipped;
				result.Success = true;
				result.Message = string.Format(
					"Topo {0}; planes: {1} descargados, {2} en caché, {3} relevantes.",
					result.TopoDownloaded ? "actualizada" : "sin cambios",
					downloaded,
					skipped,
					relevant.Count);

				using (IServiceScope scope = mvarServices.CreateScope())
				{
					TourmalineContext db = scope.ServiceProvider.GetRequiredService<TourmalineContext>();
					DBLocalSystem? local = await db.LocalSystem.FirstOrDefaultAsync(cancellationToken);
					if (local is not null)
					{
						local.LastDiamondSync = DateTime.Now;
						local.LastTopoSync = DateTime.Now;
						local.LastPlanSync = DateTime.Now;
						local.CurrentTopoId = topoId;
						await db.SaveChangesAsync(cancellationToken);
					}
				}
			}
			catch (Exception ex)
			{
				mvarLogger.LogError(ex, "Error sincronizando Diamond");
				result.Success = false;
				result.Message = ex.Message;
			}

			return result;
		}

		/// <summary>
		/// Carga topo + plan vigente para la fecha local en un <see cref="Diamond.Cabin.CabinEnvironment"/>.
		/// </summary>
		public async Task<bool> LoadIntoEnvironmentAsync(
			CabinEnvironment environment,
			Guid topoId,
			DateTime localNow,
			CancellationToken cancellationToken = default)
		{
			if (environment is null)
			{
				throw new ArgumentNullException(nameof(environment));
			}

			TopoLayout? layout = await LoadTopoLayoutAsync(topoId, cancellationToken);
			DBDiamondTopoCache? topoMeta = await GetTopoMetaAsync(topoId, cancellationToken);
			if (layout is null || topoMeta is null)
			{
				return false;
			}

			DBDiamondPublishedPlanCache? planMeta =
				await FindPlanForDateAsync(topoId, localNow.Date, cancellationToken);
			PublishedProjectPackage? package = null;
			Guid planId = Guid.Empty;
			string planName = string.Empty;
			DateTime? validFrom = null;
			DateTime? validTo = null;

			if (planMeta is not null)
			{
				package = await LoadPublishedPackageAsync(planMeta.PlanId, cancellationToken);
				planId = planMeta.PlanId;
				planName = planMeta.Name;
				validFrom = planMeta.ValidFrom;
				validTo = planMeta.ValidTo;
			}

			environment.ClockNow = localNow;
			environment.Load(
				layout,
				topoId,
				topoMeta.ContentHash,
				package,
				planId,
				planName,
				validFrom,
				validTo);
			return true;
		}

		public async Task<TopoLayout?> LoadTopoLayoutAsync(
			Guid topoId,
			CancellationToken cancellationToken = default)
		{
			DBDiamondTopoCache? meta = await GetTopoMetaAsync(topoId, cancellationToken);
			if (meta is null || string.IsNullOrEmpty(meta.RelativePath))
			{
				return null;
			}

			string full = ResolvePath(meta.RelativePath);
			if (!File.Exists(full))
			{
				mvarLogger.LogWarning("Fichero de topo ausente: {Path}", full);
				return null;
			}

			try
			{
				byte[] bytes = await File.ReadAllBytesAsync(full, cancellationToken);
				byte[] xmlBytes = MaybeGunzip(bytes, full);
				using (MemoryStream ms = new MemoryStream(xmlBytes, writable: false))
				{
					TopoLayout layout = TopoXmlSerializer.Load(ms);
					layout.RebuildAll();
					return layout;
				}
			}
			catch (Exception ex)
			{
				mvarLogger.LogError(ex, "No se pudo cargar topo {Id}", topoId);
				return null;
			}
		}

		public async Task<PublishedProjectPackage?> LoadPublishedPackageAsync(
			Guid planId,
			CancellationToken cancellationToken = default)
		{
			DBDiamondPublishedPlanCache? meta;
			using (IServiceScope scope = mvarServices.CreateScope())
			{
				TourmalineContext db = scope.ServiceProvider.GetRequiredService<TourmalineContext>();
				meta = await db.DiamondPublishedPlans
					.AsNoTracking()
					.FirstOrDefaultAsync(x => x.PlanId == planId, cancellationToken);
			}

			if (meta is null || string.IsNullOrEmpty(meta.RelativePath))
			{
				return null;
			}

			string full = ResolvePath(meta.RelativePath);
			if (!File.Exists(full))
			{
				return null;
			}

			try
			{
				byte[] bytes = await File.ReadAllBytesAsync(full, cancellationToken);
				return PublishedProjectPackage.FromUtf8Json(bytes);
			}
			catch (Exception ex)
			{
				mvarLogger.LogError(ex, "No se pudo deserializar plan publicado {Id}", planId);
				return null;
			}
		}

		public async Task<IReadOnlyList<DBDiamondPublishedPlanCache>> ListCachedPlansAsync(
			Guid? topoId = null,
			CancellationToken cancellationToken = default)
		{
			using (IServiceScope scope = mvarServices.CreateScope())
			{
				TourmalineContext db = scope.ServiceProvider.GetRequiredService<TourmalineContext>();
				IQueryable<DBDiamondPublishedPlanCache> q = db.DiamondPublishedPlans.AsNoTracking();
				if (topoId.HasValue && !Guid.Empty.Equals(topoId.Value))
				{
					q = q.Where(x => x.TopoId == topoId.Value);
				}

				return await q.OrderByDescending(x => x.ValidFrom).ToListAsync(cancellationToken);
			}
		}

		public async Task<DBDiamondTopoCache?> GetTopoMetaAsync(
			Guid topoId,
			CancellationToken cancellationToken = default)
		{
			using (IServiceScope scope = mvarServices.CreateScope())
			{
				TourmalineContext db = scope.ServiceProvider.GetRequiredService<TourmalineContext>();
				return await db.DiamondTopos
					.AsNoTracking()
					.FirstOrDefaultAsync(x => x.TopoId == topoId, cancellationToken);
			}
		}

		/// <summary>
		/// Plan vigente para una fecha civil: ValidFrom ≤ date y (ValidTo null o ≥ date), el de ValidFrom más reciente.
		/// </summary>
		public async Task<DBDiamondPublishedPlanCache?> FindPlanForDateAsync(
			Guid topoId,
			DateTime date,
			CancellationToken cancellationToken = default)
		{
			DateTime day = date.Date;
			using (IServiceScope scope = mvarServices.CreateScope())
			{
				TourmalineContext db = scope.ServiceProvider.GetRequiredService<TourmalineContext>();
				return await db.DiamondPublishedPlans
					.AsNoTracking()
					.Where(x => x.TopoId == topoId
						&& x.IsActive
						&& x.ValidFrom <= day
						&& (x.ValidTo == null || x.ValidTo >= day))
					.OrderByDescending(x => x.ValidFrom)
					.ThenByDescending(x => x.CompiledUtc)
					.FirstOrDefaultAsync(cancellationToken);
			}
		}

		private async Task<TopoEnsureResult> EnsureTopoAsync(
			DiamondClient client,
			DiamondTopoHeaderModel remote,
			CancellationToken cancellationToken)
		{
			DBDiamondTopoCache? local = await GetTopoMetaAsync(remote.Id, cancellationToken);
			string remoteHash = (remote.ContentHash ?? string.Empty).Trim().ToUpperInvariant();
			if (local is not null
				&& string.Equals(local.ContentHash, remoteHash, StringComparison.OrdinalIgnoreCase)
				&& File.Exists(ResolvePath(local.RelativePath)))
			{
				mvarLogger.LogInformation(
					"Topo {Id} en caché (hash {Hash})",
					remote.Id,
					remoteHash);
				return new TopoEnsureResult(ok: true, downloaded: false);
			}

			byte[]? payload = await client.DownloadTopoContentAsync(remote.Id);
			if (payload is null || payload.Length == 0)
			{
				mvarLogger.LogError("Descarga de topo vacía: {Id}", remote.Id);
				return new TopoEnsureResult(ok: false, downloaded: false);
			}

			string relative = Path.Combine("topo", remote.Id.ToString("N") + PreferExtension(remote.Format, payload));
			string full = ResolvePath(relative);
			Directory.CreateDirectory(Path.GetDirectoryName(full)!);
			await File.WriteAllBytesAsync(full, payload, cancellationToken);

			string hash = string.IsNullOrEmpty(remoteHash)
				? ComputeSha256Hex(payload)
				: remoteHash;

			using (IServiceScope scope = mvarServices.CreateScope())
			{
				TourmalineContext db = scope.ServiceProvider.GetRequiredService<TourmalineContext>();
				DBDiamondTopoCache? row = await db.DiamondTopos
					.FirstOrDefaultAsync(x => x.TopoId == remote.Id, cancellationToken);
				if (row is null)
				{
					row = new DBDiamondTopoCache { TopoId = remote.Id };
					db.DiamondTopos.Add(row);
				}

				row.Name = remote.Name ?? string.Empty;
				row.ContentHash = hash;
				row.StructuralHash = remote.StructuralHash ?? string.Empty;
				row.LayoutId = remote.LayoutId ?? string.Empty;
				row.RelativePath = relative.Replace('\\', '/');
				row.ByteLength = payload.Length;
				row.DownloadedUtc = DateTime.UtcNow;
				row.ServerCreatedUtc = remote.CreatedUtc;
				await db.SaveChangesAsync(cancellationToken);
			}

			mvarLogger.LogInformation("Topo {Id} descargada ({Bytes} bytes)", remote.Id, payload.Length);
			return new TopoEnsureResult(ok: true, downloaded: true);
		}

		private readonly struct TopoEnsureResult
		{
			public TopoEnsureResult(bool ok, bool downloaded)
			{
				Ok = ok;
				Downloaded = downloaded;
			}

			public bool Ok { get; }

			public bool Downloaded { get; }
		}

		private async Task<bool> EnsurePublishedPlanAsync(
			DiamondClient client,
			DiamondPublishedPlanHeaderModel remote,
			CancellationToken cancellationToken)
		{
			DBDiamondPublishedPlanCache? local;
			using (IServiceScope scope = mvarServices.CreateScope())
			{
				TourmalineContext db = scope.ServiceProvider.GetRequiredService<TourmalineContext>();
				local = await db.DiamondPublishedPlans
					.AsNoTracking()
					.FirstOrDefaultAsync(x => x.PlanId == remote.Id, cancellationToken);
			}

			string remoteHash = (remote.ContentHash ?? string.Empty).Trim().ToUpperInvariant();
			if (local is not null
				&& string.Equals(local.ContentHash, remoteHash, StringComparison.OrdinalIgnoreCase)
				&& File.Exists(ResolvePath(local.RelativePath)))
			{
				return false;
			}

			byte[]? payload = await client.DownloadPublishedContentAsync(remote.Id);
			if (payload is null || payload.Length == 0)
			{
				mvarLogger.LogWarning("Payload vacío plan publicado {Id}", remote.Id);
				return false;
			}

			string relative = Path.Combine("plans", remote.Id.ToString("N") + ".json");
			string full = ResolvePath(relative);
			Directory.CreateDirectory(Path.GetDirectoryName(full)!);
			await File.WriteAllBytesAsync(full, payload, cancellationToken);

			string hash = string.IsNullOrEmpty(remoteHash)
				? ComputeSha256Hex(payload)
				: remoteHash;

			using (IServiceScope scope = mvarServices.CreateScope())
			{
				TourmalineContext db = scope.ServiceProvider.GetRequiredService<TourmalineContext>();
				DBDiamondPublishedPlanCache? row = await db.DiamondPublishedPlans
					.FirstOrDefaultAsync(x => x.PlanId == remote.Id, cancellationToken);
				if (row is null)
				{
					row = new DBDiamondPublishedPlanCache { PlanId = remote.Id };
					db.DiamondPublishedPlans.Add(row);
				}

				row.TopoId = remote.TopoId;
				row.Name = remote.Name ?? string.Empty;
				row.ContentHash = hash;
				row.Format = remote.Format ?? string.Empty;
				row.ValidFrom = remote.ValidFrom.Date;
				row.ValidTo = remote.ValidTo?.Date;
				row.CompiledUtc = remote.CompiledUtc;
				row.RelativePath = relative.Replace('\\', '/');
				row.ByteLength = payload.Length;
				row.CirculationCount = remote.CirculationCount;
				row.AsimilationCount = remote.AsimilationCount;
				row.IsActive = remote.IsActive;
				row.DownloadedUtc = DateTime.UtcNow;
				await db.SaveChangesAsync(cancellationToken);
			}

			mvarLogger.LogInformation("Plan publicado {Id} descargado ({Bytes} bytes)", remote.Id, payload.Length);
			return true;
		}

		private string ResolvePath(string relative)
		{
			string norm = relative.Replace('/', Path.DirectorySeparatorChar);
			return Path.Combine(mvarRootPath, norm);
		}

		private static string PreferExtension(string? format, byte[] payload)
		{
			if (!string.IsNullOrEmpty(format)
				&& format.Contains("gz", StringComparison.OrdinalIgnoreCase))
			{
				return ".xml.gz";
			}

			if (payload.Length >= 2 && payload[0] == 0x1f && payload[1] == 0x8b)
			{
				return ".xml.gz";
			}

			return ".xml";
		}

		private static byte[] MaybeGunzip(byte[] bytes, string pathHint)
		{
			bool looksGzip = bytes.Length >= 2 && bytes[0] == 0x1f && bytes[1] == 0x8b;
			bool nameGz = pathHint.EndsWith(".gz", StringComparison.OrdinalIgnoreCase);
			if (!looksGzip && !nameGz)
			{
				return bytes;
			}

			using (MemoryStream input = new MemoryStream(bytes, writable: false))
			using (GZipStream gzip = new GZipStream(input, CompressionMode.Decompress))
			using (MemoryStream output = new MemoryStream())
			{
				gzip.CopyTo(output);
				return output.ToArray();
			}
		}

		private static string ComputeSha256Hex(byte[] payload)
		{
			byte[] hash = SHA256.HashData(payload);
			return Convert.ToHexString(hash);
		}
	}

	public sealed class DiamondSyncResult
	{
		public bool Success { get; set; }

		public string Message { get; set; } = string.Empty;

		public DiamondTopoHeaderModel? TopoHeader { get; set; }

		public bool TopoDownloaded { get; set; }

		public int PlansConsidered { get; set; }

		public int PlansDownloaded { get; set; }

		public int PlansUnchanged { get; set; }
	}
}
