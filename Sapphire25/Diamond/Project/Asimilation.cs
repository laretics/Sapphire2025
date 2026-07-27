using System;
using System.Collections.Generic;
using Diamond.Motion;

namespace Diamond.Project
{
	/// <summary>
	/// Perfil de marcha compartido (clonable) en el proyecto compilado.
	/// Varias <see cref="Circulation"/> pueden referenciar la misma instancia.
	/// </summary>
	public sealed class Asimilation
	{
		private readonly string mvarId;
		private readonly StationInfo mvarOrigin;
		private readonly StationInfo mvarDestination;
		private readonly CirculationSense mvarSense;
		private readonly string mvarViewId;
		private readonly string mvarPathSignature;
		private readonly string mvarFleetId;
		private readonly TimeSpan mvarTotalTime;
		private readonly List<Call> mcolCalls;
		private readonly List<Circulation> mcolCirculations;

		public Asimilation(
			string id,
			StationInfo origin,
			StationInfo destination,
			CirculationSense sense,
			string viewId,
			string pathSignature,
			string fleetId,
			TimeSpan totalTime,
			IReadOnlyList<Call> calls)
		{
			if (origin is null)
			{
				throw new ArgumentNullException(nameof(origin));
			}

			if (destination is null)
			{
				throw new ArgumentNullException(nameof(destination));
			}

			mvarId = id ?? string.Empty;
			mvarOrigin = origin;
			mvarDestination = destination;
			mvarSense = sense;
			mvarViewId = viewId ?? string.Empty;
			mvarPathSignature = pathSignature ?? string.Empty;
			mvarFleetId = fleetId ?? string.Empty;
			mvarTotalTime = totalTime;
			mcolCalls = new List<Call>();
			mcolCirculations = new List<Circulation>();

			if (calls is not null)
			{
				int i = 0;
				while (i < calls.Count)
				{
					if (calls[i] is null)
					{
						throw new ArgumentException("La lista de calls no puede contener null.", nameof(calls));
					}

					mcolCalls.Add(calls[i]);
					i++;
				}
			}
		}

		public string Id
		{
			get { return mvarId; }
		}

		public StationInfo Origin
		{
			get { return mvarOrigin; }
		}

		public StationInfo Destination
		{
			get { return mvarDestination; }
		}

		public CirculationSense Sense
		{
			get { return mvarSense; }
		}

		public string ViewId
		{
			get { return mvarViewId; }
		}

		public string PathSignature
		{
			get { return mvarPathSignature; }
		}

		public string FleetId
		{
			get { return mvarFleetId; }
		}

		public TimeSpan TotalTime
		{
			get { return mvarTotalTime; }
		}

		/// <summary>Paradas en orden de marcha (origen … destino), tiempos relativos a la salida.</summary>
		public IReadOnlyList<Call> Calls
		{
			get { return mcolCalls; }
		}

		/// <summary>Circulaciones que clonan este perfil.</summary>
		public IReadOnlyList<Circulation> Circulations
		{
			get { return mcolCirculations; }
		}

		internal void AttachCirculation(Circulation circulation)
		{
			if (circulation is null)
			{
				throw new ArgumentNullException(nameof(circulation));
			}

			mcolCirculations.Add(circulation);
		}

		public override string ToString()
		{
			return mvarId + ": " + mvarOrigin.DisplayCode + "→" + mvarDestination.DisplayCode
				+ " (" + mcolCirculations.Count.ToString() + " trenes)";
		}
	}
}
