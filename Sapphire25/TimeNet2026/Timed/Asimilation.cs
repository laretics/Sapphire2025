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
		/// <summary>
		/// Devuelve la estación con el PK más próximo al punto pasado.
		/// O devuelve -1 si ninguna estación está contenida en este eje.
		/// </summary>
		/// <param name="axis">Referencia al eje</param>
		/// <param name="auxPk">Pk en el que estamos</param>
		/// <returns>Pk del punto más cercano de la asimilación</returns>
		public long containsAxis(Axis axis, long auxPk)
		{
			long candidateDistance = long.MaxValue;
			long salida = -1;
			foreach(AsimilationStep aux in mcolSteps)
			{
				long auxDistance = (long)(Math.Abs(aux.destination.pk - auxPk));
				if(aux.axis==axis && auxDistance<candidateDistance)
				{
					candidateDistance = auxDistance;
					salida = aux.destination.pk;
				}
			}
			return salida;
		}
		/// <summary>
		/// Devuelve la lista de pasos que quedan a esta asimilación desde 
		/// la situación actual
		/// </summary>
		/// <param name="axis">Referencia al eje donde estamos</param>
		/// <param name="pk">Pk donde estamos</param>
		/// <returns>Lista de estaciones que nos quedan</returns>
		public IEnumerable<AsimilationStep> StepsFromPk(Axis axis, long pk)
		{
			if (containsAxis(axis, pk) < 0) //Estamos fuera de ruta
				return new List<AsimilationStep>(); //Lista vacía

			bool auxAscendent = isAscendent;
			TimeSpan cumul = new TimeSpan(0);
			List<AsimilationStep> salida = new List<AsimilationStep>();
			foreach(AsimilationStep aux in mcolSteps)
			{
				//Si ya tenemos elementos en la salida y los puntos que vienen NO están en el eje
				//los añadimos, porque los recorreremos después.
				AsimilationStep copia;

                if (aux.axis != axis && salida.Count > 0)
					{
						copia = new AsimilationStep(aux);
						copia.tripTime += cumul;
						cumul = new TimeSpan(0);
						salida.Add(copia);
					}					
				else if (aux.axis == axis)
				{
					if (auxAscendent && aux.destination.pk>pk)
					{
                        copia = new AsimilationStep(aux);
                        copia.tripTime += cumul;
                        cumul = new TimeSpan(0);
                        salida.Add(copia);
					}
					else if(!auxAscendent && aux.destination.pk<pk)
					{
                        copia = new AsimilationStep(aux);
                        copia.tripTime += cumul;
                        cumul = new TimeSpan(0);
                        salida.Add(copia);
					}
					else
					{
                        cumul += aux.stopTime;
                        cumul += aux.tripTime;
                    }
				}
				else
				{
                    cumul += aux.stopTime;
					cumul += aux.tripTime;
                }				
			}
			return salida;
		}		

		/// <summary>
		/// Devuelve una parte de esta asimilación desde el eje y el PK pasados
		/// </summary>
		/// <param name="axis">Eje en el que estamos situados</param>
		/// <param name="pk">PK en el que estamos situados</param>
		/// <returns>Asimilación con todos los valores de ésta, pero con menos puntos</returns>
		public Asimilation? SubAsimilation(Axis axis, long pk)
		{
			Asimilation salida = new Asimilation(Parent);
			salida.Name = this.Name;
			foreach (AsimilationStep step in StepsFromPk(axis,pk))
			{
				if (null == salida.Origin)
					salida.Origin = step.destination;
				else
					salida.mcolSteps.Add(step);
			}
			if (salida.Steps.Count() < 1) return null; //Asimilación terminada o errónea.
			return salida;
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
		/// <summary>
		/// Offset desde la salida de origen hasta la <b>llegada</b> a <paramref name="station"/>
		/// (hora de paso). En origen es cero. Suma tripTime al llegar y stopTime al partir.
		/// </summary>
		internal TimeSpan departureFrom(Station station)
		{
			if (null == station)
				return TimeSpan.MaxValue;
			if (null != Origin && (ReferenceEquals(station, Origin) || station.Id == Origin.Id))
				return TimeSpan.Zero;

			TimeSpan salida = TimeSpan.Zero;
			for (int i = 0; i < mcolSteps.Count; i++)
			{
				// Primero el trayecto hasta la estación del step; luego la parada.
				salida += mcolSteps[i].tripTime;
				Station dest = mcolSteps[i].destination;
				if (ReferenceEquals(dest, station) || (null != dest && dest.Id == station.Id))
					return salida;

				salida += mcolSteps[i].stopTime;
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
		/// <summary>
		/// Tiempo de parada en malla en <paramref name="rhs"/>.
		/// En el origen del servicio es cero; en el resto, el <see cref="AsimilationStep.stopTime"/> del paso.
		/// </summary>
		public TimeSpan stationStopTime(Station rhs)
		{
			if (rhs == Origin) return TimeSpan.Zero; //Estación de salida.
			foreach (AsimilationStep step in mcolSteps)
			{
				if (step.destination == rhs) return step.stopTime;
			}
			return TimeSpan.Zero;
		}
		internal Axis? axisByPk(long pk) //Calcula el eje de este Pk
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
		internal Axis? axisByGeoLocation(GeoLocation rhs)
		{
			if (mcolSteps.Count < 1) return null;
			Axis candidate = mcolSteps[0].axis;
			double candidateDistance = double.MaxValue;
			for(int i = 0;i<mcolSteps.Count;i++)
			{
				Axis? auxEje = mcolSteps[i].axis;

				if(null!=auxEje && null!=auxEje.Topology && auxEje !=candidate)
				{
					double auxDistance = auxEje.Topology.distanceToPoint(rhs);					
					if(auxDistance<candidateDistance)
					{
						candidate = auxEje;
						candidateDistance = auxDistance;
					}
				}
			}
			return candidate;
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
