using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Diamond.Motion;
using Diamond.Topo;

namespace Diamond.Timed
{
	/// <summary>
	/// Serialización binaria de un <see cref="ExploitationPlan"/> (plan de explotación).
	/// Contiene las mallas de todos los días; el día de visualización es un filtro al cargar.
	/// Archivo en disco: contenedor firmado <c>DSGN</c> (RSA-SHA256) + payload <c>DMSH</c>.
	/// Payload: magic DMSH + versión + vigencia + metadatos + specs + asimilaciones + días[].
	/// La topología no se embebe: al cargar hay que aportar un <see cref="TopoLayout"/> compatible.
	/// </summary>
	public static class MeshBinarySerializer
	{
		/// <summary>v2: multi-día + fecha de vigencia. v1: un solo día (lectura compat).</summary>
		public const int FormatVersion = 2;
		public const int FormatVersionV1 = 1;
		public const string FileExtension = ".dmesh";

		private static readonly byte[] Magic = { (byte)'D', (byte)'M', (byte)'S', (byte)'H' };

		/// <summary>Resultado de carga (plan de explotación + notas).</summary>
		public sealed class LoadResult
		{
			public LoadResult(ExploitationPlan plan, IReadOnlyList<string> notes)
			{
				Plan = plan ?? throw new ArgumentNullException(nameof(plan));
				Notes = notes ?? Array.Empty<string>();
			}

			public ExploitationPlan Plan { get; }

			public IReadOnlyList<string> Notes { get; }

			/// <summary>Compat: malla del primer día presente (o null).</summary>
			public Mesh? Mesh
			{
				get
				{
					foreach (KeyValuePair<DayOfWeek, Mesh> kv in Plan.MeshesByDay)
					{
						return kv.Value;
					}

					return null;
				}
			}

			public string PlanName
			{
				get { return Plan.PlanName; }
			}

			public string SourceScript
			{
				get { return Plan.SourceScript; }
			}

			public string TopoIncludePath
			{
				get { return Plan.TopoIncludePath; }
			}

			public string TopoContentHash
			{
				get { return Plan.TopoContentHash; }
			}

			public DateOnly? ValidityStart
			{
				get { return Plan.ValidityStart; }
			}
		}

		/// <summary>
		/// Guarda un plan de explotación completo (todos los días), firmado digitalmente.
		/// </summary>
		public static void Save(ExploitationPlan plan, Stream stream, TopoLayout? topoForHash = null)
		{
			if (plan is null)
			{
				throw new ArgumentNullException(nameof(plan));
			}

			if (stream is null)
			{
				throw new ArgumentNullException(nameof(stream));
			}

			using MemoryStream payload = new MemoryStream();
			WriteUnsignedPayload(plan, payload, topoForHash);
			MeshSigning.WriteSignedContainer(stream, payload.ToArray());
		}

		/// <summary>Escribe el payload DMSH sin firmar (tests / herramientas).</summary>
		public static void WriteUnsignedPayload(
			ExploitationPlan plan,
			Stream stream,
			TopoLayout? topoForHash = null)
		{
			string hash = plan.TopoContentHash;
			if (topoForHash is not null)
			{
				hash = ComputeTopoFingerprint(topoForHash);
			}

			// Catálogo global de specs y asimilaciones (deduplicadas).
			Dictionary<string, TrainSpecs> specsMap = new Dictionary<string, TrainSpecs>(StringComparer.Ordinal);
			List<Asimilation> asims = new List<Asimilation>();
			Dictionary<Asimilation, int> asimIndex = new Dictionary<Asimilation, int>(
				ReferenceEqualityComparer<Asimilation>.Instance);
			Dictionary<string, int> asimKeyToIndex = new Dictionary<string, int>(StringComparer.Ordinal);

			foreach (KeyValuePair<DayOfWeek, Mesh> kv in plan.MeshesByDay)
			{
				CollectSpecs(kv.Value, specsMap);
				int ai = 0;
				while (ai < kv.Value.Asimilations.Count)
				{
					Asimilation asim = kv.Value.Asimilations[ai];
					string key = AsimilationDedupeKey(asim);
					int idx;
					if (!asimKeyToIndex.TryGetValue(key, out idx))
					{
						idx = asims.Count;
						asims.Add(asim);
						asimKeyToIndex[key] = idx;
					}

					asimIndex[asim] = idx;
					ai++;
				}
			}

			using BinaryWriter w = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
			w.Write(Magic);
			w.Write(FormatVersion);

			// Vigencia: yyyyMMdd o 0 = no definida
			int validityYmd = 0;
			if (plan.ValidityStart.HasValue)
			{
				DateOnly d = plan.ValidityStart.Value;
				validityYmd = d.Year * 10000 + d.Month * 100 + d.Day;
			}

			w.Write(validityYmd);
			WriteString(w, plan.PlanName);
			WriteString(w, plan.SourceScript);
			WriteString(w, plan.TopoIncludePath);
			WriteString(w, hash);

			// Specs
			w.Write(specsMap.Count);
			foreach (KeyValuePair<string, TrainSpecs> kv in specsMap)
			{
				TrainSpecs s = kv.Value;
				WriteString(w, s.Id);
				WriteString(w, s.Name);
				w.Write(s.Acceleration);
				w.Write(s.ServiceBrake);
				w.Write(s.MaxSpeedKmh);
			}

			// Asimilaciones globales
			w.Write(asims.Count);
			int a = 0;
			while (a < asims.Count)
			{
				WriteAsimilation(w, asims[a]);
				a++;
			}

			// Días
			w.Write(plan.MeshesByDay.Count);
			foreach (KeyValuePair<DayOfWeek, Mesh> kv in plan.MeshesByDay)
			{
				WriteDayMesh(w, kv.Key, kv.Value, asimIndex, specsMap);
			}

			w.Flush();
		}

		/// <summary>
		/// Atajo: un solo día (compat). Prefiere <see cref="Save(ExploitationPlan, Stream, TopoLayout?)"/>.
		/// </summary>
		public static void Save(
			Mesh mesh,
			Stream stream,
			string? planName = null,
			string? sourceScript = null,
			string? topoIncludePath = null,
			TopoLayout? topoForHash = null,
			DateOnly? validityStart = null)
		{
			if (mesh is null)
			{
				throw new ArgumentNullException(nameof(mesh));
			}

			ExploitationPlan plan = new ExploitationPlan();
			plan.PlanName = planName ?? string.Empty;
			plan.SourceScript = sourceScript ?? string.Empty;
			plan.TopoIncludePath = topoIncludePath ?? string.Empty;
			plan.ValidityStart = validityStart;
			DayOfWeek day = mesh.PlanningDay ?? DayOfWeek.Monday;
			plan.SetMesh(day, mesh);
			if (topoForHash is not null)
			{
				plan.TopoContentHash = ComputeTopoFingerprint(topoForHash);
			}

			Save(plan, stream, topoForHash);
		}

		public static void SaveToFile(ExploitationPlan plan, string path, TopoLayout? topoForHash = null)
		{
			using FileStream fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
			Save(plan, fs, topoForHash);
		}

		public static LoadResult Load(Stream stream, TopoLayout topo)
		{
			if (stream is null)
			{
				throw new ArgumentNullException(nameof(stream));
			}

			if (topo is null)
			{
				throw new ArgumentNullException(nameof(topo));
			}

			// Verificar firma digital; el payload interior es DMSH.
			byte[] payload = MeshSigning.ReadAndVerifyContainer(stream);
			using MemoryStream ms = new MemoryStream(payload, writable: false);
			return LoadUnsignedPayload(ms, topo);
		}

		/// <summary>Parsea el payload DMSH ya verificado (o sin firma en tests internos).</summary>
		internal static LoadResult LoadUnsignedPayload(Stream stream, TopoLayout topo)
		{
			using BinaryReader r = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);
			byte[] magic = r.ReadBytes(4);
			if (magic.Length != 4
				|| magic[0] != Magic[0] || magic[1] != Magic[1]
				|| magic[2] != Magic[2] || magic[3] != Magic[3])
			{
				throw new InvalidDataException("No es un archivo de malla Diamond (magic DMSH).");
			}

			int version = r.ReadInt32();
			if (version == FormatVersionV1)
			{
				return LoadV1(r, topo);
			}

			if (version != FormatVersion)
			{
				throw new InvalidDataException(
					"Versión de plan de explotación no soportada: " + version.ToString(CultureInfo.InvariantCulture)
					+ " (esperada " + FormatVersion.ToString(CultureInfo.InvariantCulture) + ").");
			}

			int validityYmd = r.ReadInt32();
			DateOnly? validity = null;
			if (validityYmd > 0)
			{
				int y = validityYmd / 10000;
				int m = (validityYmd / 100) % 100;
				int d = validityYmd % 100;
				validity = new DateOnly(y, m, d);
			}

			string planName = ReadString(r);
			string sourceScript = ReadString(r);
			string topoInclude = ReadString(r);
			string topoHash = ReadString(r);

			List<string> notes = new List<string>();
			if (topoHash.Length > 0)
			{
				string now = ComputeTopoFingerprint(topo);
				if (!string.Equals(now, topoHash, StringComparison.Ordinal))
				{
					notes.Add("La huella de topología del archivo no coincide con la topo cargada; el plan puede ser incorrecto.");
				}
			}

			Dictionary<string, TrainSpecs> specsById = ReadSpecs(r);
			List<Asimilation> asims = ReadAsimilations(r, topo, specsById);

			ExploitationPlan plan = new ExploitationPlan();
			plan.PlanName = planName;
			plan.SourceScript = sourceScript;
			plan.TopoIncludePath = topoInclude;
			plan.TopoContentHash = topoHash;
			plan.ValidityStart = validity;
			int ni = 0;
			while (ni < notes.Count)
			{
				plan.AddNote(notes[ni]);
				ni++;
			}

			int dayCount = r.ReadInt32();
			int di = 0;
			while (di < dayCount)
			{
				int dayRaw = r.ReadInt32();
				if (dayRaw < 0 || dayRaw > 6)
				{
					throw new InvalidDataException("Día de semana inválido: " + dayRaw);
				}

				DayOfWeek day = (DayOfWeek)dayRaw;
				Mesh mesh = ReadDayMesh(r, day, asims, specsById);
				if (notes.Count > 0 && di == 0)
				{
					mesh.AddWarning(notes[0]);
				}

				plan.SetMesh(day, mesh);
				di++;
			}

			return new LoadResult(plan, notes);
		}

		public static LoadResult LoadFromFile(string path, TopoLayout topo)
		{
			using FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
			return Load(fs, topo);
		}

		/// <summary>Compat v1: un solo día embebido como plan de un día.</summary>
		private static LoadResult LoadV1(BinaryReader r, TopoLayout topo)
		{
			int dayRaw = r.ReadInt32();
			DayOfWeek? day = dayRaw >= 0 && dayRaw <= 6 ? (DayOfWeek)dayRaw : DayOfWeek.Monday;
			string planName = ReadString(r);
			string sourceScript = ReadString(r);
			string topoInclude = ReadString(r);
			string topoHash = ReadString(r);

			List<string> notes = new List<string>
			{
				"Archivo v1 (un solo día); se ha importado como plan de un día."
			};
			if (topoHash.Length > 0)
			{
				string now = ComputeTopoFingerprint(topo);
				if (!string.Equals(now, topoHash, StringComparison.Ordinal))
				{
					notes.Add("La huella de topología del archivo no coincide con la topo cargada.");
				}
			}

			Dictionary<string, TrainSpecs> specsById = ReadSpecs(r);
			List<Asimilation> asims = ReadAsimilations(r, topo, specsById);

			// Circulaciones del único día
			Mesh mesh = new Mesh();
			mesh.PlanningDay = day;
			int circCount = r.ReadInt32();
			int ci = 0;
			while (ci < circCount)
			{
				mesh.AddCirculation(ReadCirculation(r, asims, specsById));
				ci++;
			}

			int warnCount = r.ReadInt32();
			int wi = 0;
			while (wi < warnCount)
			{
				mesh.AddWarning(ReadString(r));
				wi++;
			}

			int errCount = r.ReadInt32();
			int ei = 0;
			while (ei < errCount)
			{
				mesh.AddError(ReadString(r));
				ei++;
			}

			if (notes.Count > 0)
			{
				mesh.AddWarning(notes[0]);
			}

			ExploitationPlan plan = new ExploitationPlan();
			plan.PlanName = planName;
			plan.SourceScript = sourceScript;
			plan.TopoIncludePath = topoInclude;
			plan.TopoContentHash = topoHash;
			plan.ValidityStart = null;
			plan.SetMesh(day ?? DayOfWeek.Monday, mesh);
			int ni = 0;
			while (ni < notes.Count)
			{
				plan.AddNote(notes[ni]);
				ni++;
			}

			return new LoadResult(plan, notes);
		}

		private static void WriteDayMesh(
			BinaryWriter w,
			DayOfWeek day,
			Mesh mesh,
			Dictionary<Asimilation, int> asimIndex,
			Dictionary<string, TrainSpecs> specsMap)
		{
			w.Write((int)day);
			w.Write(mesh.Circulations.Count);
			int ci = 0;
			while (ci < mesh.Circulations.Count)
			{
				Circulation c = mesh.Circulations[ci];
				int aIdx;
				if (!asimIndex.TryGetValue(c.Asimilation, out aIdx))
				{
					// Misma clave por si la referencia no está (otra instancia equivalente).
					string key = AsimilationDedupeKey(c.Asimilation);
					throw new InvalidOperationException(
						"Circulación " + c.TechnicalId + " sin índice de asimilación (" + key + ").");
				}

				WriteString(w, c.TechnicalId);
				WriteString(w, c.DemandId);
				WriteString(w, c.ServiceNumber);
				WriteString(w, c.Color);
				w.Write(c.Departure.Ticks);
				WriteString(w, c.Specs.Id);
				w.Write(aIdx);
				ci++;
			}

			w.Write(mesh.Warnings.Count);
			int wi = 0;
			while (wi < mesh.Warnings.Count)
			{
				WriteString(w, mesh.Warnings[wi]);
				wi++;
			}

			w.Write(mesh.Errors.Count);
			int ei = 0;
			while (ei < mesh.Errors.Count)
			{
				WriteString(w, mesh.Errors[ei]);
				ei++;
			}
		}

		private static Mesh ReadDayMesh(
			BinaryReader r,
			DayOfWeek day,
			List<Asimilation> asims,
			Dictionary<string, TrainSpecs> specsById)
		{
			Mesh mesh = new Mesh();
			mesh.PlanningDay = day;
			int circCount = r.ReadInt32();
			int ci = 0;
			while (ci < circCount)
			{
				mesh.AddCirculation(ReadCirculation(r, asims, specsById));
				ci++;
			}

			int warnCount = r.ReadInt32();
			int wi = 0;
			while (wi < warnCount)
			{
				mesh.AddWarning(ReadString(r));
				wi++;
			}

			int errCount = r.ReadInt32();
			int ei = 0;
			while (ei < errCount)
			{
				mesh.AddError(ReadString(r));
				ei++;
			}

			return mesh;
		}

		private static Circulation ReadCirculation(
			BinaryReader r,
			List<Asimilation> asims,
			Dictionary<string, TrainSpecs> specsById)
		{
			string technicalId = ReadString(r);
			string demandId = ReadString(r);
			string serviceNumber = ReadString(r);
			string color = ReadString(r);
			long depTicks = r.ReadInt64();
			string specsId = ReadString(r);
			int aIdx = r.ReadInt32();
			if (aIdx < 0 || aIdx >= asims.Count)
			{
				throw new InvalidDataException("Índice de asimilación fuera de rango: " + aIdx);
			}

			TrainSpecs specs;
			if (!specsById.TryGetValue(specsId, out specs!))
			{
				specs = asims[aIdx].Specs;
			}

			return Circulation.CreateForDeserialization(
				technicalId,
				demandId,
				asims[aIdx],
				specs,
				TimeSpan.FromTicks(depTicks),
				color.Length > 0 ? color : null,
				serviceNumber.Length > 0 ? serviceNumber : null);
		}

		private static Dictionary<string, TrainSpecs> ReadSpecs(BinaryReader r)
		{
			int specsCount = r.ReadInt32();
			Dictionary<string, TrainSpecs> specsById = new Dictionary<string, TrainSpecs>(StringComparer.Ordinal);
			int si = 0;
			while (si < specsCount)
			{
				string id = ReadString(r);
				string name = ReadString(r);
				double accel = r.ReadDouble();
				double brake = r.ReadDouble();
				double vmax = r.ReadDouble();
				specsById[id] = new TrainSpecs(id, name, accel, brake, vmax);
				si++;
			}

			return specsById;
		}

		private static List<Asimilation> ReadAsimilations(
			BinaryReader r,
			TopoLayout topo,
			Dictionary<string, TrainSpecs> specsById)
		{
			int asimCount = r.ReadInt32();
			List<Asimilation> asims = new List<Asimilation>(asimCount);
			int ai = 0;
			while (ai < asimCount)
			{
				asims.Add(ReadAsimilation(r, topo, specsById));
				ai++;
			}

			return asims;
		}

		private static string AsimilationDedupeKey(Asimilation asim)
		{
			StringBuilder sb = new StringBuilder();
			sb.Append(asim.Specs.Id);
			sb.Append('|');
			sb.Append(asim.View.PathSignature());
			sb.Append('|');
			sb.Append(asim.Origin.PK);
			sb.Append('>');
			sb.Append(asim.Destination.PK);
			int si = 0;
			while (si < asim.Stops.Count)
			{
				AsimilationStop st = asim.Stops[si];
				if (st.PK != asim.Origin.PK && st.PK != asim.Destination.PK)
				{
					sb.Append(';');
					sb.Append(st.PK);
					sb.Append('@');
					sb.Append(st.Dwell.Ticks);
				}

				si++;
			}

			return sb.ToString();
		}

		private static void WriteAsimilation(BinaryWriter w, Asimilation asim)
		{
			RouteView view = asim.View;
			WriteString(w, view.Id);
			WriteString(w, view.Name);
			w.Write(view.Legs.Count);
			int li = 0;
			while (li < view.Legs.Count)
			{
				RouteLeg leg = view.Legs[li];
				WriteString(w, leg.Axis.Id);
				w.Write(leg.AxisFromPk);
				w.Write(leg.AxisToPk);
				li++;
			}

			WriteString(w, asim.Specs.Id);
			WriteString(w, asim.Origin.Station.Id);
			w.Write(asim.Origin.PK);
			WriteString(w, asim.Destination.Station.Id);
			w.Write(asim.Destination.PK);

			List<AsimilationStop> intermediate = new List<AsimilationStop>();
			int si = 0;
			while (si < asim.Stops.Count)
			{
				AsimilationStop st = asim.Stops[si];
				if (st.PK != asim.Origin.PK && st.PK != asim.Destination.PK)
				{
					intermediate.Add(st);
				}

				si++;
			}

			w.Write(intermediate.Count);
			int ii = 0;
			while (ii < intermediate.Count)
			{
				AsimilationStop st = intermediate[ii];
				WriteString(w, st.Placement.Station.Id);
				w.Write(st.PK);
				w.Write(st.Dwell.Ticks);
				ii++;
			}
		}

		private static Asimilation ReadAsimilation(
			BinaryReader r,
			TopoLayout topo,
			Dictionary<string, TrainSpecs> specsById)
		{
			string viewId = ReadString(r);
			string viewName = ReadString(r);
			int legCount = r.ReadInt32();
			List<(Axis Axis, long FromPk, long ToPk)> segs = new List<(Axis, long, long)>(legCount);
			int li = 0;
			while (li < legCount)
			{
				string axisId = ReadString(r);
				long fromPk = r.ReadInt64();
				long toPk = r.ReadInt64();
				Axis? axis = topo.FindAxisById(axisId);
				if (axis is null)
				{
					throw new InvalidDataException("Eje no encontrado en topo: " + axisId);
				}

				segs.Add((axis, fromPk, toPk));
				li++;
			}

			if (segs.Count == 0)
			{
				throw new InvalidDataException("Asimilación sin tramos de vista.");
			}

			RouteView view = RouteView.Concat(
				string.IsNullOrEmpty(viewId) ? "loaded" : viewId,
				string.IsNullOrEmpty(viewName) ? "loaded" : viewName,
				segs);

			string specsId = ReadString(r);
			TrainSpecs specs;
			if (!specsById.TryGetValue(specsId, out specs!))
			{
				specs = TrainSpecs.DefaultModel;
			}

			string originId = ReadString(r);
			long originPk = r.ReadInt64();
			string destId = ReadString(r);
			long destPk = r.ReadInt64();

			Station originSt = ResolveStation(topo, originId, view, originPk);
			Station destSt = ResolveStation(topo, destId, view, destPk);
			StationOnAxis origin = new StationOnAxis(originSt, originPk);
			StationOnAxis destination = new StationOnAxis(destSt, destPk);

			int stopCount = r.ReadInt32();
			List<AsimilationStop> stops = new List<AsimilationStop>(stopCount);
			int si = 0;
			while (si < stopCount)
			{
				string stId = ReadString(r);
				long pk = r.ReadInt64();
				long dwellTicks = r.ReadInt64();
				Station st = ResolveStation(topo, stId, view, pk);
				stops.Add(new AsimilationStop(new StationOnAxis(st, pk), TimeSpan.FromTicks(dwellTicks)));
				si++;
			}

			return new Asimilation(view, specs, origin, destination, stops);
		}

		private static Station ResolveStation(TopoLayout topo, string id, RouteView view, long routePk)
		{
			if (!string.IsNullOrEmpty(id))
			{
				Station? byId = topo.FindStationById(id);
				if (byId is not null)
				{
					return byId;
				}
			}

			int i = 0;
			while (i < view.Stations.Count)
			{
				if (view.Stations[i].PK == routePk)
				{
					return view.Stations[i].Station;
				}

				i++;
			}

			Station ghost = new Station(string.IsNullOrEmpty(id) ? "pk-" + routePk.ToString(CultureInfo.InvariantCulture) : id);
			ghost.Name = ghost.Id;
			return ghost;
		}

		private static void CollectSpecs(Mesh mesh, Dictionary<string, TrainSpecs> map)
		{
			int ai = 0;
			while (ai < mesh.Asimilations.Count)
			{
				TrainSpecs s = mesh.Asimilations[ai].Specs;
				if (!map.ContainsKey(s.Id))
				{
					map[s.Id] = s;
				}

				ai++;
			}

			int ci = 0;
			while (ci < mesh.Circulations.Count)
			{
				TrainSpecs s = mesh.Circulations[ci].Specs;
				if (!map.ContainsKey(s.Id))
				{
					map[s.Id] = s;
				}

				ci++;
			}
		}

		public static string ComputeTopoFingerprint(TopoLayout topo)
		{
			StringBuilder sb = new StringBuilder();
			sb.Append(topo.Stations.Count);
			sb.Append('|');
			sb.Append(topo.Axes.Count);
			int i = 0;
			while (i < topo.Axes.Count)
			{
				Axis a = topo.Axes[i];
				sb.Append(';');
				sb.Append(a.Id);
				sb.Append(':');
				sb.Append(a.Stations.Count);
				sb.Append(':');
				if (a.Vertices.Count > 0)
				{
					sb.Append(a.Vertices[0].PK);
					sb.Append('-');
					sb.Append(a.Vertices[a.Vertices.Count - 1].PK);
				}

				i++;
			}

			byte[] bytes = Encoding.UTF8.GetBytes(sb.ToString());
			byte[] hash = SHA256.HashData(bytes);
			return Convert.ToHexString(hash);
		}

		private static void WriteString(BinaryWriter w, string? s)
		{
			string v = s ?? string.Empty;
			byte[] bytes = Encoding.UTF8.GetBytes(v);
			w.Write(bytes.Length);
			w.Write(bytes);
		}

		private static string ReadString(BinaryReader r)
		{
			int len = r.ReadInt32();
			if (len < 0 || len > 50_000_000)
			{
				throw new InvalidDataException("Longitud de cadena inválida: " + len);
			}

			if (len == 0)
			{
				return string.Empty;
			}

			byte[] bytes = r.ReadBytes(len);
			if (bytes.Length != len)
			{
				throw new EndOfStreamException("Cadena truncada.");
			}

			return Encoding.UTF8.GetString(bytes);
		}
	}

	internal sealed class ReferenceEqualityComparer<T> : IEqualityComparer<T>
		where T : class
	{
		public static readonly ReferenceEqualityComparer<T> Instance = new ReferenceEqualityComparer<T>();

		public bool Equals(T? x, T? y)
		{
			return ReferenceEquals(x, y);
		}

		public int GetHashCode(T obj)
		{
			return System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
		}
	}
}
