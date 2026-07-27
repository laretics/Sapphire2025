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
		private string mvarServiceNumber;
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
			mvarServiceNumber = string.Empty;
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
		/// Número de tren como texto (p. ej. <c>4901</c>, <c>P1MTX</c>).
		/// Vacío si aún no se ha numerado.
		/// </summary>
		public string ServiceNumber
		{
			get { return mvarServiceNumber; }
		}

		public bool HasServiceNumber
		{
			get { return mvarServiceNumber.Length > 0; }
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
		/// Asigna el número de servicio y actualiza <see cref="Id"/> a ese texto.
		/// </summary>
		internal void AssignServiceNumber(string serviceNumber)
		{
			if (string.IsNullOrWhiteSpace(serviceNumber))
			{
				throw new ArgumentException("El número de tren no puede estar vacío.", nameof(serviceNumber));
			}

			mvarServiceNumber = serviceNumber.Trim();
			mvarId = mvarServiceNumber;
		}

		/// <summary>
		/// Color desde definición de asimilación del script. No sobrescribe un color
		/// ya fijado por el requisito de demanda.
		/// </summary>
		internal void TryAssignColorFromAsimilationDef(string color)
		{
			if (mvarColor.Length > 0)
			{
				return;
			}

			if (string.IsNullOrWhiteSpace(color))
			{
				return;
			}

			mvarColor = color.Trim();
		}

		public override string ToString()
		{
			if (mvarServiceNumber.Length > 0)
			{
				return mvarServiceNumber + " dep " + mvarDeparture.ToString();
			}

			return mvarId + " dep " + mvarDeparture.ToString();
		}
	}
}
