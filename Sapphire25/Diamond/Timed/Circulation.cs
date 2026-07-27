using System;
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
		private string mvarDemandId;
		private Asimilation mvarAsimilation;
		private TrainSpecs mvarSpecs;
		private TimeSpan mvarDeparture;

		public Circulation(
			string id,
			string demandId,
			Asimilation asimilation,
			TrainSpecs specs,
			TimeSpan departure)
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
			mvarDemandId = demandId ?? string.Empty;
			mvarAsimilation = asimilation;
			mvarSpecs = specs;
			mvarDeparture = departure;
		}

		public string Id
		{
			get { return mvarId; }
		}

		public string DemandId
		{
			get { return mvarDemandId; }
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

		public override string ToString()
		{
			return mvarId + " dep " + mvarDeparture.ToString();
		}
	}
}
