using System;
using System.Globalization;
using Diamond.Motion;
using Diamond.Timed;
using Diamond.Topo;

namespace Diamond.Controls.Rendering
{
	/// <summary>
	/// Etiquetas de hora en la columna de estaciones para el tren seleccionado:
	/// parada momentánea (&lt; 1 min), comercial (≥ 1 min) o paso sin parada.
	/// </summary>
	public static class StationTimeLabels
	{
		/// <summary>Umbral: dwell &lt; este valor se muestra como parada momentánea (una hora + ·).</summary>
		public static readonly TimeSpan CommercialDwellThreshold = TimeSpan.FromMinutes(1);

		public enum Kind
		{
			/// <summary>El tren no pasa por ese PK.</summary>
			None = 0,
			/// <summary>Paso sin parada (hora de paso).</summary>
			Pass = 1,
			/// <summary>Parada con dwell &lt; 1 min (una hora + ·).</summary>
			Momentary = 2,
			/// <summary>Parada comercial con dwell ≥ 1 min (llegada y salida).</summary>
			Commercial = 3,
			/// <summary>Origen de la circulación (hora de salida).</summary>
			Origin = 4,
			/// <summary>Destino de la circulación (hora de llegada).</summary>
			Destination = 5
		}

		public readonly struct Annotation
		{
			private readonly Kind mvarKind;
			private readonly TimeSpan? mvarArrival;
			private readonly TimeSpan? mvarDeparture;
			private readonly string mvarText;

			public Annotation(Kind kind, TimeSpan? arrival, TimeSpan? departure, string text)
			{
				mvarKind = kind;
				mvarArrival = arrival;
				mvarDeparture = departure;
				mvarText = text ?? string.Empty;
			}

			public Kind Kind
			{
				get { return mvarKind; }
			}

			public TimeSpan? Arrival
			{
				get { return mvarArrival; }
			}

			public TimeSpan? Departure
			{
				get { return mvarDeparture; }
			}

			/// <summary>Texto listo para pintar (p. ej. <c>08:32 ·</c>, <c>08:40–08:45</c>).</summary>
			public string Text
			{
				get { return mvarText; }
			}

			/// <summary>True = color de parada; false = color de paso.</summary>
			public bool IsStopStyle
			{
				get
				{
					return mvarKind == Kind.Momentary
						|| mvarKind == Kind.Commercial
						|| mvarKind == Kind.Origin
						|| mvarKind == Kind.Destination;
				}
			}
		}

		/// <summary>
		/// Resuelve la anotación en un PK de la <strong>misma</strong> vista de la asimilación.
		/// </summary>
		public static bool TryCreate(Circulation circulation, long asimRoutePk, out Annotation annotation)
		{
			return TryCreate(circulation, displayView: null, displayOrAsimPk: asimRoutePk, out annotation);
		}

		/// <summary>
		/// Resuelve la anotación del tren en un PK de la vista de pantalla.
		/// Si <paramref name="displayView"/> no es null, proyecta ese PK a la vista de la
		/// asimilación (imprescindible en multi-eje: p. ej. catálogo T3+T2 Palma→SPB frente
		/// a un tren SPB→PMI cuyo origen es PK 0 en su propia ruta y SPB es el extremo alto
		/// en la vista UI; sin mapear se confunde origen y destino y la “salida” sale mal).
		/// </summary>
		public static bool TryCreate(
			Circulation circulation,
			RouteView? displayView,
			long displayOrAsimPk,
			out Annotation annotation)
		{
			annotation = default;
			if (circulation is null)
			{
				return false;
			}

			Asimilation asim = circulation.Asimilation;
			long asimPk = displayOrAsimPk;
			if (displayView is not null)
			{
				if (!TryMapDisplayPkToAsim(displayView, asim.View, displayOrAsimPk, out asimPk))
				{
					return false;
				}
			}

			long originPk = asim.Origin.PK;
			long destPk = asim.Destination.PK;

			if (asimPk == originPk)
			{
				TimeSpan dep = circulation.Departure;
				annotation = new Annotation(Kind.Origin, dep, dep, FormatClock(dep));
				return true;
			}

			if (asimPk == destPk)
			{
				TimeSpan arr = circulation.Arrival;
				annotation = new Annotation(Kind.Destination, arr, arr, FormatClock(arr));
				return true;
			}

			TimeSpan? relDep = asim.TimeDepartByPK(asimPk);
			if (!relDep.HasValue)
			{
				return false;
			}

			TimeSpan? relArr = asim.TimeArriveByPK(asimPk);
			TimeSpan depAbs = circulation.Departure + relDep.Value;
			TimeSpan arrAbs = relArr.HasValue
				? circulation.Departure + relArr.Value
				: depAbs;

			TimeSpan dwell = asim.DwellAtPk(asimPk);
			bool isMandatoryStop = HasStopAtPk(asim, asimPk);

			if (dwell >= CommercialDwellThreshold)
			{
				string text = FormatClock(arrAbs) + "–" + FormatClock(depAbs);
				annotation = new Annotation(Kind.Commercial, arrAbs, depAbs, text);
				return true;
			}

			if (isMandatoryStop || dwell > TimeSpan.Zero)
			{
				// Parada momentánea: una sola hora (salida) + punto medio.
				string text = FormatClock(depAbs) + " ·";
				annotation = new Annotation(Kind.Momentary, arrAbs, depAbs, text);
				return true;
			}

			// Paso sin parada: hora de paso en color distinto.
			string passText = FormatClock(arrAbs);
			annotation = new Annotation(Kind.Pass, arrAbs, depAbs, passText);
			return true;
		}

		/// <summary>
		/// PK de la vista de pantalla → PK de ruta de la asimilación del tren.
		/// </summary>
		public static bool TryMapDisplayPkToAsim(
			RouteView displayView,
			RouteView asimView,
			long displayPk,
			out long asimPk)
		{
			asimPk = 0L;
			if (displayView is null || asimView is null)
			{
				return false;
			}

			// La API mapea source → this: queremos display → asim.
			return asimView.TryMapRoutePkFrom(displayView, displayPk, out asimPk);
		}

		public static string FormatClock(TimeSpan ts)
		{
			if (ts < TimeSpan.Zero)
			{
				ts = TimeSpan.Zero;
			}

			int h = (int)ts.TotalHours;
			int m = ts.Minutes;
			return h.ToString("00", CultureInfo.InvariantCulture)
				+ ":"
				+ m.ToString("00", CultureInfo.InvariantCulture);
		}

		private static bool HasStopAtPk(Asimilation asim, long pk)
		{
			int index = 0;
			while (index < asim.Stops.Count)
			{
				if (asim.Stops[index].PK == pk)
				{
					return true;
				}

				index++;
			}

			return false;
		}
	}
}
