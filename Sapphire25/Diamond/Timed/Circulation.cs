using System;
using System.Globalization;
using Diamond.Motion;

namespace Diamond.Timed
{
	/// <summary>
	/// Circulación concreta en la malla: una salida en el tiempo que reutiliza un perfil
	/// <see cref="Asimilation"/> (compartible con otras circulaciones del mismo patrón de marcha).
	/// </summary>
	public sealed class Circulation
	{
		private string mvarId;
		private readonly string mvarTechnicalId;
		private string mvarDemandId;
		private Asimilation mvarAsimilation;
		private TrainSpecs mvarSpecs;
		private TimeSpan mvarDeparture;
		private int mvarServiceNumber;
		private string mvarColor;

		public Circulation(
			string id,
			string demandId,
			Asimilation asimilation,
			TrainSpecs specs,
			TimeSpan departure,
			string? color = null)
		{
			if (asimilation is null)
			{
				throw new ArgumentNullException(nameof(asimilation));
			}

			if (specs is null)
			{
				throw new ArgumentNullException(nameof(specs));
			}

			mvarId = id ?? string.Empty;
			mvarTechnicalId = mvarId;
			mvarDemandId = demandId ?? string.Empty;
			mvarAsimilation = asimilation;
			mvarSpecs = specs;
			mvarDeparture = departure;
			mvarServiceNumber = 0;
			mvarColor = string.IsNullOrWhiteSpace(color) ? string.Empty : color.Trim();
		}

		/// <summary>
		/// Identificador de circulación (tras numerar, coincide con <see cref="ServiceNumber"/>).
		/// </summary>
		public string Id
		{
			get { return mvarId; }
		}

		/// <summary>
		/// Id técnico de planificación (p. ej. C12-R-T3), estable aunque se renumere el servicio.
		/// </summary>
		public string TechnicalId
		{
			get { return mvarTechnicalId; }
		}

		public string DemandId
		{
			get { return mvarDemandId; }
		}

		/// <summary>
		/// Color SVG opcional de la traza (<c>#rrggbb</c>), heredado del requisito de demanda.
		/// Vacío = el render usa el color por asimilación.
		/// </summary>
		public string Color
		{
			get { return mvarColor; }
		}

		public bool HasColor
		{
			get { return mvarColor.Length > 0; }
		}

		public Asimilation Asimilation
		{
			get { return mvarAsimilation; }
		}

		public TrainSpecs Specs
		{
			get { return mvarSpecs; }
		}

		/// <summary>
		/// Número de tren SFM (p. ej. 4901). 0 si aún no se ha numerado.
		/// Impares = sentido PK creciente; pares = PK decreciente, por corredor OD.
		/// </summary>
		public int ServiceNumber
		{
			get { return mvarServiceNumber; }
		}

		/// <summary>
		/// Hora de salida del origen (desde medianoche, <see cref="TimeSpan"/>).
		/// </summary>
		public TimeSpan Departure
		{
			get { return mvarDeparture; }
		}

		public TimeSpan Arrival
		{
			get { return mvarDeparture + mvarAsimilation.TotalTime; }
		}

		/// <summary>
		/// Instante absoluto en el que el tren está en el PK (salida + tiempo relativo del perfil).
		/// </summary>
		public TimeSpan? AbsoluteTimeAtPk(long pk)
		{
			TimeSpan? relative = mvarAsimilation.TimeByPK(pk);
			if (!relative.HasValue)
			{
				return null;
			}

			return mvarDeparture + relative.Value;
		}

		/// <summary>
		/// Asigna el número de servicio SFM y actualiza <see cref="Id"/> al dígito del número.
		/// </summary>
		internal void AssignServiceNumber(int serviceNumber)
		{
			if (serviceNumber <= 0)
			{
				throw new ArgumentOutOfRangeException(nameof(serviceNumber));
			}

			mvarServiceNumber = serviceNumber;
			mvarId = serviceNumber.ToString(CultureInfo.InvariantCulture);
		}

		public override string ToString()
		{
			if (mvarServiceNumber > 0)
			{
				return mvarServiceNumber.ToString(CultureInfo.InvariantCulture)
					+ " dep " + mvarDeparture.ToString();
			}

			return mvarId + " dep " + mvarDeparture.ToString();
		}
	}
}
