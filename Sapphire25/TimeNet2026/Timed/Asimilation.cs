using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using TimeNet2026.Auxiliar;
using TimeNet2026.Topo;

namespace TimeNet2026.Timed
{
	public class Asimilation : Entity
	{
		internal string mvarName;
		internal string mvarComment;
		internal string[] mvarColor; //Colores nocturno y diurno
		internal int mvarMaxSpeed; //Velocidad máxima de la asimilación.
		public TopoStorage Parent { get; private set; }
		private TimeSpan? mvarDuration;
		public TimeSpan? duration
		//Duración total del viaje.
		{
			get
			{
				if (null == mvarDuration)
				{
					mvarDuration = TimeSpan.Zero;
					foreach (AsimilationStep step in mcolSteps)
						mvarDuration = mvarDuration + step.stopTime + step.tripTime;
				}
				return mvarDuration;
			}
		}
		public Station? origin { get; set; }
		public Station? destination
		{
			get
			{
				if (mcolSteps.Count == 0) return origin;
				return mcolSteps[mcolSteps.Count - 1].destination;
			}
		}
		public bool isAscendent 
		{ 
			get
			{
				if (null == origin || null == destination) return true;
				return destination.pk > origin.pk;
			}				
		}
		public string id { get; set; }
		public string name { get => mvarName; set => mvarName = value; }
		public string comment { get => mvarComment; set => mvarComment = value; }
		public int maxSpeed { get => mvarMaxSpeed; }
		public string[] color { get => mvarColor; set => mvarColor = value; }
		internal List<AsimilationStep> mcolSteps;

		public IEnumerable<AsimilationStep> Steps { get => mcolSteps; }
		internal bool containsStation(Station rhs)
		{
			if (rhs == origin) return true;
			foreach (AsimilationStep aux in mcolSteps)
			{
				if (aux.destination == rhs) return true;
			}
			return false;
		}
		internal Station? stationByName(string name)
		{
			if (name.Equals(origin.name)) return origin;
			foreach (AsimilationStep aux in mcolSteps)
			{
				if (name.Equals(aux.destination.name)) return aux.destination;
			}
			return null; //No contiene la estación.
		}
		internal TimeSpan departureFrom(Station station)
		{
			if (station == origin) return TimeSpan.Zero;
			TimeSpan salida = TimeSpan.Zero;
			for (int i = 0; i < mcolSteps.Count; i++)
			{
				if (mcolSteps[i].destination == station) return salida;
				salida += mcolSteps[i].stopTime;
				salida += mcolSteps[i].tripTime;
			}
			return TimeSpan.MaxValue;
		}
		internal Station? nextStation(long pk) //Devuelve la próxima estación en este PK
		{
			if (isAscendent)
			{
				if (origin.pk > pk) return origin;
				for (int i = 0; i < mcolSteps.Count; i++)
				{
					if (mcolSteps[i].destination.pk > pk) return mcolSteps[i].destination;
				}
			}
			else
			{
				for (int i = 0; i < mcolSteps.Count; i++)
				{
					if (mcolSteps[i].destination.pk < pk) return mcolSteps[i].destination;
				}
				if (origin.pk > pk) return destination;
			}
			return null;
		}
		internal TimeSpan stationStopTime(Station rhs)
		{
			if (rhs == origin) return TimeSpan.Zero; //Estación de salida.
			foreach (AsimilationStep step in mcolSteps)
			{
				if (step.destination == rhs) return step.stopTime;
			}
			return TimeSpan.Zero;
		}
		internal Axis axisByPk(long pk) //Calcula el eje de este Pk
		{
			if (isAscendent)
			{
				for (int i = 0; i < mcolSteps.Count; i++)
				{
					if (mcolSteps[i].destination.pk > pk) return mcolSteps[i].axis;
				}
			}
			else
			{
				for (int i = 0; i < mcolSteps.Count; i++)
				{
					if (mcolSteps[i].destination.pk < pk) return mcolSteps[i].axis;
				}
			}
			return null;
		}

		internal TimeSpan calculateDelay(long currentPk, TimeSpan timeFromDeparture)
		//Calcula el tiempo de retraso en un PK determinado con respecto a la hora teórica de salida
		{
			return timeFromDeparture.Subtract(calculateTime(currentPk));
		}
		internal TimeSpan calculateTime(long currentPk)
		//Calcula la hora exacta desde la salida para pasar por ese PK
		{
			TimeSpan auxLapse = TimeSpan.Zero;
			if(null!=origin && null!=destination)
			{
				Station lastStation = origin;
				if (isAscendent)
				{
					if (currentPk <= origin.pk) return auxLapse; //En origen el tiempo es cero.
					foreach (AsimilationStep step in mcolSteps)
					{
						if (step.destination.pk > currentPk)
						{
							//Hay que interpolar entre el punto anterior y éste.
							if (step.destination.pk != lastStation.pk) //Evitamos dividir por cero
								return (step.tripTime * (currentPk - lastStation.pk) / (step.destination.pk - lastStation.pk)) + auxLapse;
						}
						auxLapse += step.tripTime;
						auxLapse += step.stopTime;
						lastStation = step.destination;
					}
				}
				else
				{
					if (currentPk >= origin.pk) return auxLapse; //En origen el tiempo es cero.
					foreach (AsimilationStep step in mcolSteps)
					{
						if (step.destination.pk < currentPk)
						{
							//Interpolamos entre el punto anterior y éste.
							if (step.destination.pk != lastStation.pk) //Evitamos dividir por cero
								return (step.tripTime * (lastStation.pk - currentPk) / (lastStation.pk - step.destination.pk)) + auxLapse;
						}
						auxLapse += step.tripTime;
						auxLapse += step.stopTime;
						lastStation = step.destination;
					}
				}
			}
			return auxLapse;//En destino el tiempo es el máximo de duración de la asimilación.
		}

		internal float auxCacheY { get; set; } //Valor cacheado para representar la primera posición de una malla.
											   //IMPORTANTE: Este valor no tiene ninguna relevancia fuera de la operación de pintado.
		public override string ToString()
		{
			return string.Format("{0}-{1}", origin.name, destination.name);
		}
		public Asimilation(TopoStorage parent)
		{
			id = string.Empty;
			mvarName = string.Empty;
			mvarComment = string.Empty;
			mvarMaxSpeed = 100;
			mcolSteps = new List<AsimilationStep>();
			mvarDuration = null;
			mvarColor = new string[2];
			this.Parent = parent;
		}
		internal Asimilation(XmlNode root, TopoStorage parent):this(parent)
		{
			id = XMLUtil.StringParam(root, "id");
			mvarName = XMLUtil.StringParam(root, "name");
			mvarMaxSpeed = XMLUtil.IntParam(root, "type");
			mvarColor[0] = XMLUtil.StringParam(root, "color");
			mvarColor[1] = XMLUtil.StringParam(root, "darkcolor");
			mvarComment = XMLUtil.StringParam(root, "comment");
		}
		internal Asimilation(Axis auxEje, TopoStorage parent):this(parent)
		{
			//Genera una asimilación a partir de un eje dado.
			mcolSteps = new List<AsimilationStep>();
			mvarColor = new string[2];
			mvarDuration = null;
			mvarName = auxEje.mvarName;
			mvarColor[0] = auxEje.mvarColor[0];
			mvarColor[1] = auxEje.mvarColor[1];
			mvarMaxSpeed = auxEje.mvarMaxSpeed;
			origin = null;
			if(null!=auxEje.mcolStations)
			{
				foreach (Station auxEstacion in auxEje.mcolStations)
				{
					if (null == origin)
					{
						origin = auxEstacion;
					}
					else
					{
						AsimilationStep auxNuevo = new AsimilationStep(auxEstacion,auxEje,new TimeSpan(0), new TimeSpan(0));
						mcolSteps.Add(auxNuevo);
					}
				}
			}
		}
	}
}
