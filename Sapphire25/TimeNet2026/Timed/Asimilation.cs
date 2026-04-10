using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using TimeNet2026.Auxiliar;
using TimeNet2026.Topo;

namespace TimeNet2026.Timed
{
	public class Asimilation : Entity
	{
		internal string[] mvarColor; //Colores nocturno y diurno
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
		public Station? Origin { get; set; }
		public Station? Destination
		{
			get
			{
				if (mcolSteps.Count == 0) return Origin;
				return mcolSteps[mcolSteps.Count - 1].destination;
			}
		}
		public bool isAscendent 
		{ 
			get
			{
				if (null == Origin || null == Destination) return true;
				return Destination.pk > Origin.pk;
			}				
		}
		public string TourmalineString
		{
			get => string.Format("{0} - {1}",Origin?.Name,Destination?.Name);
		}
		public string id { get; set; }
		public string Name { get; internal set; }
		string Entity.name { get => Name; set => Name=value; }
		public string Comment { get; set; }
		string Entity.comment { get => Comment; set => Comment = value; }
		public int MaxSpeed { get; internal set; }
		public string[] color { get => mvarColor; set => mvarColor = value; }
		string[] Entity.color { get => mvarColor; set => mvarColor = value; }	
		internal List<AsimilationStep> mcolSteps;
		public string ForeColor { get => mvarColor[1]; }
		public string BackColor 
		{
			  get 
			  {
				if (mvarColor[0] == mvarColor[1])
					return Entity.AtenuateColor(mvarColor[0]);
				else
					return ForeColor;
			  }
		}
		public IEnumerable<AsimilationStep> Steps { get => mcolSteps; }
		internal bool containsStation(Station rhs)
		{
			if (rhs == Origin) return true;
			foreach (AsimilationStep aux in mcolSteps)
			{
				if (aux.destination == rhs) return true;
			}
			return false;
		}
		internal Station? stationByName(string name)
		{			
			if (null!=Origin && name.Equals(Origin.Name)) return Origin;
			foreach (AsimilationStep aux in mcolSteps)
			{
				if (name.Equals(aux.destination.Name)) return aux.destination;
			}
			return null; //No contiene la estación.
		}
		internal TimeSpan departureFrom(Station station)
		{
			if (station == Origin) return TimeSpan.Zero;
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
			if (null == Origin) return null;
			if (isAscendent)
			{
				if (Origin.pk > pk) return Origin;
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
				if (Origin.pk > pk) return Destination;
			}
			return null;
		}
		internal TimeSpan stationStopTime(Station rhs)
		{
			if (rhs == Origin) return TimeSpan.Zero; //Estación de salida.
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
			if(null!=Origin && null!=Destination)
			{
				Station lastStation = Origin;
				if (isAscendent)
				{
					if (currentPk <= Origin.pk) return auxLapse; //En origen el tiempo es cero.
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
					if (currentPk >= Origin.pk) return auxLapse; //En origen el tiempo es cero.
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
			if (null == Origin || null == Destination)
				return "[Empty]";
			return string.Format("{0}-{1}", Origin.Name, Destination.Name);
		}
		public Asimilation(TopoStorage parent)
		{
			id = string.Empty;
			Name = string.Empty;
			Comment = string.Empty;
			MaxSpeed = 100;
			mcolSteps = new List<AsimilationStep>();
			mvarDuration = null;
			mvarColor = new string[2];
			this.Parent = parent;
		}
		internal Asimilation(Axis auxEje, TopoStorage parent):this(parent)
		{
			//Genera una asimilación a partir de un eje dado.
			mcolSteps = new List<AsimilationStep>();
			mvarColor = new string[2];
			mvarDuration = null;
			Name = auxEje.Name;
			mvarColor[0] = auxEje.mvarColor[0];
			mvarColor[1] = auxEje.mvarColor[1];
			MaxSpeed = auxEje.MaxSpeed;
			Origin = null;
			if(null!=auxEje.Stations)
			{
				foreach (Station auxEstacion in auxEje.Stations)
				{
					if (null == Origin)
					{
						Origin = auxEstacion;
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
