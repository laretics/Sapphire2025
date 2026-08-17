using System;
using System.Collections.Generic;
using Diamond.Basis;

namespace Diamond.Topo
{
	/// <summary>
	/// Eje topográfico: entidad lineal en PK (metros) más polilínea geográfica calibrada.
	/// Los vértices ancla fijan PK conocidos; el resto se precálcula por interpolación a lo largo
	/// de la polilínea. Las consultas espaciales usan un BVH de subcadenas (divide y vencerás con poda).
	/// </summary>
	public class Axis : Lineal<long, LongAxis>
	{
		private readonly List<AxisVertex> mcolVertices;
		private readonly List<StationOnAxis> mcolStations;
		private readonly SpeedLimitMap mvarFixedLimits;
		private readonly SpeedLimitMap mvarTemporaryLimits;
		private readonly List<TemporarySpeedLimit> mcolTemporaryLimitRecords;
		/// <summary>Límites de velocidad de sesión (script de malla); no se serializan en topo XML.</summary>
		private readonly SpeedLimitMap mvarSessionLimits;
		private readonly List<long> mcolCantonFrontiers;
		private readonly List<TrackSpan> mcolTrackSpans;
		/// <summary>Tramos de vías de sesión (script); tienen prioridad sobre <see cref="TrackSpans"/> base.</summary>
		private readonly List<TrackSpan> mcolSessionTrackSpans;
		private SpatialNode? mvarSpatialRoot;
		private bool mvarIsBuilt;
		private string mvarId;
		private string mvarName;
		private string mvarComment;
		private int mvarVmax;
		private string mvarColor;
		private string mvarDarkColor;
		private int mvarDefaultTrackCount;

		public Axis()
			: base()
		{
			mcolVertices = new List<AxisVertex>();
			mcolStations = new List<StationOnAxis>();
			mvarFixedLimits = new SpeedLimitMap();
			mvarTemporaryLimits = new SpeedLimitMap();
			mcolTemporaryLimitRecords = new List<TemporarySpeedLimit>();
			mvarSessionLimits = new SpeedLimitMap();
			mcolCantonFrontiers = new List<long>();
			mcolTrackSpans = new List<TrackSpan>();
			mcolSessionTrackSpans = new List<TrackSpan>();
			mvarSpatialRoot = null;
			mvarIsBuilt = false;
			mvarId = string.Empty;
			mvarName = string.Empty;
			mvarComment = string.Empty;
			mvarVmax = 0;
			mvarColor = string.Empty;
			mvarDarkColor = string.Empty;
			mvarDefaultTrackCount = 1;
		}

		/// <summary>
		/// Identificador del eje (atributo XML id), p. ej. M1, T3.
		/// </summary>
		public string Id
		{
			get { return mvarId; }
			set { mvarId = value ?? string.Empty; }
		}

		public string Name
		{
			get { return mvarName; }
			set { mvarName = value ?? string.Empty; }
		}

		public string Comment
		{
			get { return mvarComment; }
			set { mvarComment = value ?? string.Empty; }
		}

		/// <summary>
		/// Velocidad máxima del eje (atributo XML vmax), en las unidades del fichero (típicamente km/h).
		/// </summary>
		public int Vmax
		{
			get { return mvarVmax; }
			set { mvarVmax = value; }
		}

		public string Color
		{
			get { return mvarColor; }
			set { mvarColor = value ?? string.Empty; }
		}

		public string DarkColor
		{
			get { return mvarDarkColor; }
			set { mvarDarkColor = value ?? string.Empty; }
		}

		public IReadOnlyList<AxisVertex> Vertices
		{
			get { return mcolVertices; }
		}

		/// <summary>
		/// Incidencias de estación sobre este eje (ordenadas por PK tras <see cref="RebuildStationPlacements"/>).
		/// </summary>
		public IReadOnlyList<StationOnAxis> Stations
		{
			get { return mcolStations; }
		}

		/// <summary>
		/// Limitaciones fijas (p. ej. del libro de itinerario / XML).
		/// </summary>
		public SpeedLimitMap FixedLimits
		{
			get { return mvarFixedLimits; }
		}

		/// <summary>
		/// Limitaciones temporales (obras, incidencias). Pueden superponerse a las fijas.
		/// </summary>
		public SpeedLimitMap TemporaryLimits
		{
			get { return mvarTemporaryLimits; }
		}

		/// <summary>
		/// Temporales crudas (motivo y observaciones) además de la capa de V.
		/// </summary>
		public IReadOnlyList<TemporarySpeedLimit> TemporaryLimitRecords
		{
			get { return mcolTemporaryLimitRecords; }
		}

		/// <summary>
		/// Limitaciones de sesión (mini-DSL de malla). No forman parte del XML de topología;
		/// se limpian y reaplica al compilar el script. Más restrictivas que fijas/temporales.
		/// </summary>
		public SpeedLimitMap SessionLimits
		{
			get { return mvarSessionLimits; }
		}

		/// <summary>
		/// PKs de frontera de acantonamiento (ordenados, únicos).
		/// Entre dos fronteras consecutivas solo puede haber un tren a la vez (por vía lógica).
		/// </summary>
		public IReadOnlyList<long> CantonFrontiers
		{
			get { return mcolCantonFrontiers; }
		}

		/// <summary>
		/// Tramos con número de vías distinto del valor por defecto (topología base).
		/// </summary>
		public IReadOnlyList<TrackSpan> TrackSpans
		{
			get { return mcolTrackSpans; }
		}

		/// <summary>
		/// Tramos de vías de sesión (script de malla). Tienen prioridad sobre <see cref="TrackSpans"/>.
		/// </summary>
		public IReadOnlyList<TrackSpan> SessionTrackSpans
		{
			get { return mcolSessionTrackSpans; }
		}

		/// <summary>
		/// Número de vías por defecto en el eje cuando no aplica ningún <see cref="TrackSpan"/> (normalmente 1).
		/// </summary>
		public int DefaultTrackCount
		{
			get { return mvarDefaultTrackCount; }
			set
			{
				if (value < 1)
				{
					throw new ArgumentOutOfRangeException(nameof(value));
				}

				mvarDefaultTrackCount = value;
			}
		}

		/// <summary>
		/// True tras un <see cref="Rebuild"/> exitoso con al menos un segmento.
		/// </summary>
		public bool IsBuilt
		{
			get { return mvarIsBuilt; }
		}

		/// <summary>
		/// Define las fronteras de acantonamiento (lista de PK). Se ordenan y deduplican.
		/// </summary>
		public void SetCantonFrontiers(IEnumerable<long> frontierPks)
		{
			if (frontierPks is null)
			{
				throw new ArgumentNullException(nameof(frontierPks));
			}

			mcolCantonFrontiers.Clear();
			SortedSet<long> set = new SortedSet<long>();
			foreach (long pk in frontierPks)
			{
				set.Add(pk);
			}

			foreach (long pk in set)
			{
				mcolCantonFrontiers.Add(pk);
			}
		}

		public void ClearCantonFrontiers()
		{
			mcolCantonFrontiers.Clear();
		}

		/// <summary>
		/// Cantones = intervalos [frontera_i, frontera_{i+1}) entre fronteras consecutivas.
		/// </summary>
		public IReadOnlyList<TrackSpan> GetCantonSections()
		{
			List<TrackSpan> sections = new List<TrackSpan>();
			if (mcolCantonFrontiers.Count < 2)
			{
				return sections;
			}

			int index = 0;
			while (index < mcolCantonFrontiers.Count - 1)
			{
				long pk0 = mcolCantonFrontiers[index];
				long pkf = mcolCantonFrontiers[index + 1];
				int tracks = GetTrackCountAt(pk0);
				sections.Add(new TrackSpan(pk0, pkf, tracks));
				index++;
			}

			return sections;
		}

		/// <summary>
		/// Asigna un número de vías en [pk0, pkf) en la topología base.
		/// Por defecto el resto del eje usa <see cref="DefaultTrackCount"/>.
		/// </summary>
		public void SetTrackCount(long pk0, long pkf, int trackCount)
		{
			mcolTrackSpans.Add(new TrackSpan(pk0, pkf, trackCount));
		}

		public void ClearTrackSpans()
		{
			mcolTrackSpans.Clear();
		}

		/// <summary>
		/// Asigna vías de sesión en [pk0, pkf) (script de malla; no toca la topo base).
		/// </summary>
		public void SetSessionTrackCount(long pk0, long pkf, int trackCount)
		{
			if (trackCount < 1)
			{
				throw new ArgumentOutOfRangeException(nameof(trackCount));
			}

			mcolSessionTrackSpans.Add(new TrackSpan(pk0, pkf, trackCount));
		}

		/// <summary>
		/// Limpia solo las capas de sesión (vías + límites de script). No altera fijas ni spans base.
		/// </summary>
		public void ClearSessionOverlays()
		{
			mcolSessionTrackSpans.Clear();
			mvarSessionLimits.Clear();
		}

		/// <summary>
		/// Número de vías en el PK indicado: default → spans base → spans de sesión (prioridad).
		/// </summary>
		public int GetTrackCountAt(long pk)
		{
			int tracks = mvarDefaultTrackCount;
			int index = 0;
			while (index < mcolTrackSpans.Count)
			{
				TrackSpan span = mcolTrackSpans[index];
				if (pk >= span.Pk0 && pk < span.Pkf)
				{
					tracks = span.TrackCount;
				}

				index++;
			}

			index = 0;
			while (index < mcolSessionTrackSpans.Count)
			{
				TrackSpan span = mcolSessionTrackSpans[index];
				if (pk >= span.Pk0 && pk < span.Pkf)
				{
					tracks = span.TrackCount;
				}

				index++;
			}

			return tracks;
		}

		/// <summary>
		/// True si en ese PK hay al menos doble vía (cruce en línea posible sin estación).
		/// En vía única el cruce solo es viable en estaciones de cruce.
		/// </summary>
		public bool AllowsLineCrossingAt(long pk)
		{
			return GetTrackCountAt(pk) >= 2;
		}

		/// <summary>
		/// Velocidad temporal más restrictiva en el PK, o null si no hay capa temporal.
		/// </summary>
		public int? GetTemporarySpeedLimit(long pk)
		{
			return mvarTemporaryLimits.GetMinSpeedAt(pk);
		}

		/// <summary>
		/// V para ficha/libro: fijas + sesión, y temporales solo si se piden.
		/// </summary>
		public int? GetSpeedLimitForSheet(long pk, bool includeTemporary)
		{
			int? fixedSpeed = mvarFixedLimits.GetMinSpeedAt(pk);
			int? sessionSpeed = mvarSessionLimits.GetMinSpeedAt(pk);
			int? layered = MinSpeed(fixedSpeed, sessionSpeed);
			if (includeTemporary)
			{
				layered = MinSpeed(layered, mvarTemporaryLimits.GetMinSpeedAt(pk));
			}

			if (layered.HasValue)
			{
				return layered;
			}

			if (mvarVmax > 0)
			{
				return mvarVmax;
			}

			return null;
		}

		public TemporarySpeedLimit? FindGoverningTemporary(long pk)
		{
			TemporarySpeedLimit? best = null;
			int i = 0;
			while (i < mcolTemporaryLimitRecords.Count)
			{
				TemporarySpeedLimit limit = mcolTemporaryLimitRecords[i];
				long lo = limit.PK < limit.PKEnd ? limit.PK : limit.PKEnd;
				long hi = limit.PK > limit.PKEnd ? limit.PK : limit.PKEnd;
				if (pk >= lo && pk < hi)
				{
					if (best is null
						|| limit.Speed < best.Speed
						|| (limit.Speed == best.Speed && (hi - lo) < (best.PKEnd - best.PK)))
					{
						best = limit;
					}
				}

				i++;
			}

			return best;
		}

		internal void ClearTemporaryRecords()
		{
			mcolTemporaryLimitRecords.Clear();
		}

		internal void AddTemporaryRecord(TemporarySpeedLimit limit)
		{
			if (limit is not null)
			{
				mcolTemporaryLimitRecords.Add(limit);
			}
		}

		/// <summary>
		/// Velocidad efectiva en un PK: la más restrictiva entre fijas, temporales y de sesión.
		/// Si no hay limitación en capas, devuelve <see cref="Vmax"/> del eje cuando es &gt; 0; si no, null.
		/// </summary>
		public int? GetEffectiveSpeedLimit(long pk)
		{
			int? fixedSpeed = mvarFixedLimits.GetMinSpeedAt(pk);
			int? temporarySpeed = GetTemporarySpeedLimit(pk);
			int? sessionSpeed = mvarSessionLimits.GetMinSpeedAt(pk);

			int? layered = MinSpeed(fixedSpeed, temporarySpeed);
			layered = MinSpeed(layered, sessionSpeed);

			if (layered.HasValue)
			{
				return layered;
			}

			if (mvarVmax > 0)
			{
				return mvarVmax;
			}

			return null;
		}

		private static int? MinSpeed(int? a, int? b)
		{
			if (a.HasValue && b.HasValue)
			{
				return a.Value < b.Value ? a : b;
			}

			if (a.HasValue)
			{
				return a;
			}

			return b;
		}

		public void AddVertex(double latitude, double longitude)
		{
			mcolVertices.Add(new AxisVertex(latitude, longitude));
			mvarIsBuilt = false;
		}

		public void AddVertex(double latitude, double longitude, long anchorPk)
		{
			mcolVertices.Add(new AxisVertex(latitude, longitude, anchorPk));
			mvarIsBuilt = false;
		}

		public void AddVertex(AxisVertex vertex)
		{
			if (vertex is null)
			{
				throw new ArgumentNullException(nameof(vertex));
			}

			mcolVertices.Add(vertex);
			mvarIsBuilt = false;
		}

		public void ClearVertices()
		{
			mcolVertices.Clear();
			mcolStations.Clear();
			mvarSpatialRoot = null;
			mvarIsBuilt = false;
			PK = 0L;
			Length = 0L;
		}

		/// <summary>
		/// Recalcula el PK de todos los vértices a partir de las anclas, reconstruye el índice espacial
		/// y regenera las incidencias <see cref="StationOnAxis"/> desde los vértices con estación.
		/// </summary>
		public void Rebuild()
		{
			mvarIsBuilt = false;
			mvarSpatialRoot = null;

			if (mcolVertices.Count == 0)
			{
				mcolStations.Clear();
				PK = 0L;
				Length = 0L;
				return;
			}

			RecalculatePks();

			if (mcolVertices.Count >= 2)
			{
				mvarSpatialRoot = BuildSpatialNode(0, mcolVertices.Count - 1);
			}

			long startPk = mcolVertices[0].PK;
			long endPk = mcolVertices[mcolVertices.Count - 1].PK;
			PK = startPk;
			Length = endPk - startPk;
			Normalize();

			RebuildStationPlacements();

			mvarIsBuilt = mcolVertices.Count >= 2;
		}

		/// <summary>
		/// Reconstruye <see cref="Stations"/> a partir de vértices con <see cref="AxisVertex.Station"/> y ancla.
		/// </summary>
		public void RebuildStationPlacements()
		{
			mcolStations.Clear();

			int index = 0;
			while (index < mcolVertices.Count)
			{
				AxisVertex vertex = mcolVertices[index];
				if (vertex.Station is not null && vertex.IsAnchor && vertex.AnchorPk.HasValue)
				{
					mcolStations.Add(new StationOnAxis(vertex.Station, vertex.AnchorPk.Value));
				}

				index++;
			}

			mcolStations.Sort(CompareStationOnAxisByPk);
		}

		private static int CompareStationOnAxisByPk(StationOnAxis left, StationOnAxis right)
		{
			return left.PK.CompareTo(right.PK);
		}

		/// <summary>
		/// Distancia mínima en metros desde (lat, lon) hasta la polilínea del eje.
		/// </summary>
		public double Distance(double latitude, double longitude)
		{
			EnsureBuilt();
			if (mvarSpatialRoot is null)
			{
				return double.PositiveInfinity;
			}

			double bestDistance = double.PositiveInfinity;
			long bestPk = 0L;
			double bestLat = 0.0;
			double bestLon = 0.0;
			QueryClosest(mvarSpatialRoot, latitude, longitude, ref bestDistance, ref bestPk, ref bestLat, ref bestLon);
			return bestDistance;
		}

		/// <summary>
		/// Proyecta un punto geográfico sobre el eje. Falla si la distancia mínima supera <paramref name="maxDistanceMeters"/>.
		/// </summary>
		public AxisProjection PKFromLocation(double latitude, double longitude, double maxDistanceMeters = 1000.0)
		{
			EnsureBuilt();
			if (mvarSpatialRoot is null)
			{
				return AxisProjection.Fail(double.PositiveInfinity);
			}

			double bestDistance = double.PositiveInfinity;
			long bestPk = 0L;
			double bestLat = 0.0;
			double bestLon = 0.0;
			QueryClosest(mvarSpatialRoot, latitude, longitude, ref bestDistance, ref bestPk, ref bestLat, ref bestLon);

			if (double.IsPositiveInfinity(bestDistance) || bestDistance > maxDistanceMeters)
			{
				return AxisProjection.Fail(bestDistance);
			}

			return new AxisProjection(true, bestPk, bestDistance, bestLat, bestLon);
		}

		/// <summary>
		/// Localiza un PK sobre la polilínea. <paramref name="offsetMeters"/>: 0 = eje,
		/// negativo = izquierda, positivo = derecha (mirando en sentido de PK creciente).
		/// Los PK fuera de rango se limitan a los extremos calibrados.
		/// </summary>
		public GeoPoint LocationFromPK(long pk, double offsetMeters = 0.0)
		{
			EnsureBuilt();
			if (mcolVertices.Count == 0)
			{
				return new GeoPoint(0.0, 0.0);
			}

			if (mcolVertices.Count == 1)
			{
				return ApplyOffset(
					mcolVertices[0].Latitude,
					mcolVertices[0].Longitude,
					mcolVertices[0].Latitude,
					mcolVertices[0].Longitude,
					offsetMeters);
			}

			long minPk = mcolVertices[0].PK;
			long maxPk = mcolVertices[mcolVertices.Count - 1].PK;
			if (minPk > maxPk)
			{
				long swap = minPk;
				minPk = maxPk;
				maxPk = swap;
			}

			long clampedPk = pk;
			if (clampedPk < minPk)
			{
				clampedPk = minPk;
			}
			else if (clampedPk > maxPk)
			{
				clampedPk = maxPk;
			}

			int segmentIndex = FindSegmentIndexForPk(clampedPk);
			AxisVertex a = mcolVertices[segmentIndex];
			AxisVertex b = mcolVertices[segmentIndex + 1];

			double t = 0.0;
			long pkA = a.PK;
			long pkB = b.PK;
			if (pkA != pkB)
			{
				t = (double)(clampedPk - pkA) / (double)(pkB - pkA);
				if (t < 0.0)
				{
					t = 0.0;
				}
				else if (t > 1.0)
				{
					t = 1.0;
				}
			}

			double lat = a.Latitude + t * (b.Latitude - a.Latitude);
			double lon = a.Longitude + t * (b.Longitude - a.Longitude);

			return ApplyOffset(a.Latitude, a.Longitude, b.Latitude, b.Longitude, lat, lon, offsetMeters);
		}

		private void EnsureBuilt()
		{
			if (!mvarIsBuilt)
			{
				Rebuild();
			}
		}

		private void RecalculatePks()
		{
			int count = mcolVertices.Count;
			double[] cumGeo = new double[count];
			cumGeo[0] = 0.0;

			int i = 1;
			while (i < count)
			{
				AxisVertex prev = mcolVertices[i - 1];
				AxisVertex curr = mcolVertices[i];
				double seg = GeoMath.HaversineMeters(
					prev.Latitude,
					prev.Longitude,
					curr.Latitude,
					curr.Longitude);
				cumGeo[i] = cumGeo[i - 1] + seg;
				i++;
			}

			List<int> mcolAnchorIndices = new List<int>();
			i = 0;
			while (i < count)
			{
				if (mcolVertices[i].IsAnchor)
				{
					mcolAnchorIndices.Add(i);
					mcolVertices[i].PK = mcolVertices[i].AnchorPk!.Value;
				}

				i++;
			}

			if (mcolAnchorIndices.Count == 0)
			{
				// Sin anclas: PK = metros geodésicos acumulados desde el origen de la polilínea.
				i = 0;
				while (i < count)
				{
					mcolVertices[i].PK = (long)Math.Round(cumGeo[i]);
					i++;
				}

				return;
			}

			if (mcolAnchorIndices.Count == 1)
			{
				int anchorIndex = mcolAnchorIndices[0];
				long anchorPk = mcolVertices[anchorIndex].PK;
				double anchorCum = cumGeo[anchorIndex];

				i = 0;
				while (i < count)
				{
					if (i != anchorIndex)
					{
						double delta = cumGeo[i] - anchorCum;
						mcolVertices[i].PK = anchorPk + (long)Math.Round(delta);
					}

					i++;
				}

				return;
			}

			// Entre anclas consecutivas: interpolación proporcional a la longitud geodésica.
			int a = 0;
			while (a < mcolAnchorIndices.Count - 1)
			{
				int i0 = mcolAnchorIndices[a];
				int i1 = mcolAnchorIndices[a + 1];
				long pk0 = mcolVertices[i0].PK;
				long pk1 = mcolVertices[i1].PK;
				double geo0 = cumGeo[i0];
				double geo1 = cumGeo[i1];
				double geoSpan = geo1 - geo0;

				int k = i0 + 1;
				while (k < i1)
				{
					if (geoSpan < 1e-9)
					{
						mcolVertices[k].PK = pk0;
					}
					else
					{
						double ratio = (cumGeo[k] - geo0) / geoSpan;
						double pk = pk0 + ratio * (pk1 - pk0);
						mcolVertices[k].PK = (long)Math.Round(pk);
					}

					k++;
				}

				a++;
			}

			// Antes de la primera ancla: extrapolar con la escala del primer tramo anclado.
			int firstAnchor = mcolAnchorIndices[0];
			int secondAnchor = mcolAnchorIndices[1];
			double scaleBefore = ComputeScale(
				mcolVertices[firstAnchor].PK,
				mcolVertices[secondAnchor].PK,
				cumGeo[firstAnchor],
				cumGeo[secondAnchor]);

			i = firstAnchor - 1;
			while (i >= 0)
			{
				double geoDelta = cumGeo[firstAnchor] - cumGeo[i];
				mcolVertices[i].PK = mcolVertices[firstAnchor].PK - (long)Math.Round(scaleBefore * geoDelta);
				i--;
			}

			// Después de la última ancla.
			int lastAnchor = mcolAnchorIndices[mcolAnchorIndices.Count - 1];
			int prevAnchor = mcolAnchorIndices[mcolAnchorIndices.Count - 2];
			double scaleAfter = ComputeScale(
				mcolVertices[prevAnchor].PK,
				mcolVertices[lastAnchor].PK,
				cumGeo[prevAnchor],
				cumGeo[lastAnchor]);

			i = lastAnchor + 1;
			while (i < count)
			{
				double geoDelta = cumGeo[i] - cumGeo[lastAnchor];
				mcolVertices[i].PK = mcolVertices[lastAnchor].PK + (long)Math.Round(scaleAfter * geoDelta);
				i++;
			}
		}

		private static double ComputeScale(long pk0, long pk1, double geo0, double geo1)
		{
			double geoSpan = geo1 - geo0;
			if (Math.Abs(geoSpan) < 1e-9)
			{
				return 1.0;
			}

			return (double)(pk1 - pk0) / geoSpan;
		}

		/// <summary>
		/// BVH sobre subcadenas contiguas. <paramref name="vertexStart"/>..<paramref name="vertexEnd"/> inclusive
		/// cubre los segmentos [vertexStart, vertexEnd).
		/// </summary>
		private SpatialNode BuildSpatialNode(int vertexStart, int vertexEnd)
		{
			SpatialNode node = new SpatialNode();
			node.VertexStart = vertexStart;
			node.VertexEnd = vertexEnd;

			if (vertexEnd - vertexStart <= 1)
			{
				node.IsLeaf = true;
				AxisVertex a = mcolVertices[vertexStart];
				AxisVertex b = mcolVertices[vertexEnd];
				node.MinLat = Math.Min(a.Latitude, b.Latitude);
				node.MaxLat = Math.Max(a.Latitude, b.Latitude);
				node.MinLon = Math.Min(a.Longitude, b.Longitude);
				node.MaxLon = Math.Max(a.Longitude, b.Longitude);
				return node;
			}

			int mid = vertexStart + (vertexEnd - vertexStart) / 2;
			if (mid == vertexStart)
			{
				mid = vertexStart + 1;
			}

			node.IsLeaf = false;
			node.Left = BuildSpatialNode(vertexStart, mid);
			node.Right = BuildSpatialNode(mid, vertexEnd);
			node.MinLat = Math.Min(node.Left.MinLat, node.Right.MinLat);
			node.MaxLat = Math.Max(node.Left.MaxLat, node.Right.MaxLat);
			node.MinLon = Math.Min(node.Left.MinLon, node.Right.MinLon);
			node.MaxLon = Math.Max(node.Left.MaxLon, node.Right.MaxLon);
			return node;
		}

		private void QueryClosest(
			SpatialNode node,
			double latitude,
			double longitude,
			ref double bestDistance,
			ref long bestPk,
			ref double bestLat,
			ref double bestLon)
		{
			double boxDistance = GeoMath.PointToBoundingBoxMeters(
				latitude,
				longitude,
				node.MinLat,
				node.MaxLat,
				node.MinLon,
				node.MaxLon);

			if (boxDistance >= bestDistance)
			{
				return;
			}

			if (node.IsLeaf)
			{
				AxisVertex a = mcolVertices[node.VertexStart];
				AxisVertex b = mcolVertices[node.VertexEnd];
				double t;
				double projLat;
				double projLon;
				double distance = GeoMath.PointToSegmentMeters(
					latitude,
					longitude,
					a.Latitude,
					a.Longitude,
					b.Latitude,
					b.Longitude,
					out t,
					out projLat,
					out projLon);

				if (distance < bestDistance)
				{
					bestDistance = distance;
					bestLat = projLat;
					bestLon = projLon;
					bestPk = InterpolatePk(a.PK, b.PK, t);
				}

				return;
			}

			// Explorar primero la hija con bbox más prometedora.
			SpatialNode left = node.Left!;
			SpatialNode right = node.Right!;
			double leftBox = GeoMath.PointToBoundingBoxMeters(
				latitude,
				longitude,
				left.MinLat,
				left.MaxLat,
				left.MinLon,
				left.MaxLon);
			double rightBox = GeoMath.PointToBoundingBoxMeters(
				latitude,
				longitude,
				right.MinLat,
				right.MaxLat,
				right.MinLon,
				right.MaxLon);

			if (leftBox <= rightBox)
			{
				QueryClosest(left, latitude, longitude, ref bestDistance, ref bestPk, ref bestLat, ref bestLon);
				QueryClosest(right, latitude, longitude, ref bestDistance, ref bestPk, ref bestLat, ref bestLon);
			}
			else
			{
				QueryClosest(right, latitude, longitude, ref bestDistance, ref bestPk, ref bestLat, ref bestLon);
				QueryClosest(left, latitude, longitude, ref bestDistance, ref bestPk, ref bestLat, ref bestLon);
			}
		}

		private static long InterpolatePk(long pkA, long pkB, double t)
		{
			double pk = pkA + t * (pkB - pkA);
			return (long)Math.Round(pk);
		}

		/// <summary>
		/// Índice i tal que el PK cae en el segmento vértices[i]..[i+1] (PK monótono o no).
		/// </summary>
		private int FindSegmentIndexForPk(long pk)
		{
			int last = mcolVertices.Count - 2;
			if (last < 0)
			{
				return 0;
			}

			// Búsqueda lineal robusta (PK puede no ser estrictamente monótono en geometrías raras).
			// Si es monótono creciente, la binaria acelera; aquí usamos binaria por cum si monótono.
			bool monoIncreasing = true;
			bool monoDecreasing = true;
			int i = 1;
			while (i < mcolVertices.Count)
			{
				if (mcolVertices[i].PK < mcolVertices[i - 1].PK)
				{
					monoIncreasing = false;
				}

				if (mcolVertices[i].PK > mcolVertices[i - 1].PK)
				{
					monoDecreasing = false;
				}

				i++;
			}

			if (monoIncreasing)
			{
				int lo = 0;
				int hi = last;
				while (lo < hi)
				{
					int mid = lo + (hi - lo) / 2;
					if (mcolVertices[mid + 1].PK < pk)
					{
						lo = mid + 1;
					}
					else
					{
						hi = mid;
					}
				}

				return lo;
			}

			if (monoDecreasing)
			{
				int lo = 0;
				int hi = last;
				while (lo < hi)
				{
					int mid = lo + (hi - lo) / 2;
					if (mcolVertices[mid + 1].PK > pk)
					{
						lo = mid + 1;
					}
					else
					{
						hi = mid;
					}
				}

				return lo;
			}

			// No monótono: elegir el segmento cuyo intervalo de PK contiene el valor, o el más cercano.
			int bestIndex = 0;
			double bestScore = double.PositiveInfinity;
			i = 0;
			while (i <= last)
			{
				long a = mcolVertices[i].PK;
				long b = mcolVertices[i + 1].PK;
				long min = a < b ? a : b;
				long max = a > b ? a : b;
				double score;
				if (pk >= min && pk <= max)
				{
					return i;
				}

				if (pk < min)
				{
					score = min - pk;
				}
				else
				{
					score = pk - max;
				}

				if (score < bestScore)
				{
					bestScore = score;
					bestIndex = i;
				}

				i++;
			}

			return bestIndex;
		}

		private static GeoPoint ApplyOffset(
			double lat1,
			double lon1,
			double lat2,
			double lon2,
			double offsetMeters)
		{
			return ApplyOffset(lat1, lon1, lat2, lon2, lat1, lon1, offsetMeters);
		}

		private static GeoPoint ApplyOffset(
			double lat1,
			double lon1,
			double lat2,
			double lon2,
			double baseLat,
			double baseLon,
			double offsetMeters)
		{
			if (Math.Abs(offsetMeters) < 1e-12)
			{
				return new GeoPoint(baseLat, baseLon);
			}

			double e1;
			double n1;
			GeoMath.ToLocalMeters(baseLat, baseLon, lat1, lon1, out e1, out n1);
			double e2;
			double n2;
			GeoMath.ToLocalMeters(baseLat, baseLon, lat2, lon2, out e2, out n2);

			double dirE = e2 - e1;
			double dirN = n2 - n1;
			double len = Math.Sqrt(dirE * dirE + dirN * dirN);
			if (len < 1e-9)
			{
				// Segmento degenerado: offset hacia el este arbitrario.
				dirE = 1.0;
				dirN = 0.0;
				len = 1.0;
			}

			dirE /= len;
			dirN /= len;

			// Derecha = rotación 90° horaria en (este, norte): (e,n) -> (n, -e).
			// Izquierda = opuesto. offset > 0 derecha, < 0 izquierda.
			double rightE = dirN;
			double rightN = -dirE;

			double east = rightE * offsetMeters;
			double north = rightN * offsetMeters;

			double deltaLat;
			double deltaLon;
			GeoMath.MetersToLatLonDelta(baseLat, east, north, out deltaLat, out deltaLon);
			return new GeoPoint(baseLat + deltaLat, baseLon + deltaLon);
		}

		private sealed class SpatialNode
		{
			public int VertexStart;
			public int VertexEnd;
			public double MinLat;
			public double MaxLat;
			public double MinLon;
			public double MaxLon;
			public bool IsLeaf;
			public SpatialNode? Left;
			public SpatialNode? Right;
		}
	}
}
