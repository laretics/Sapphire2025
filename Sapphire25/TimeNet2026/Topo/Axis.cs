using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using TimeNet2026.Auxiliar;
using TimeNet2026.Storage;
using static System.Collections.Specialized.BitVector32;

namespace TimeNet2026.Topo
{
	public class Axis : Lineal, Entity
	{
		internal const double BOUNDS_PERCENTAGE = 0.05;
		internal GeoLocation[] mvarBounds = new GeoLocation[2];
		//Utilizo el rectángulo para detectar rápidamente si un punto dado puede pertenecer a un eje.
		public string id { get; set; }
		public string mvarName { get; set; }
		internal string mvarComment { get; set; }
		internal int searchStep { get; set; } //Valor de salto para la caché de búsqueda de puntos en el eje.
		internal string[] mvarColor { get; set; }
		internal List<RefPunctual> mcolPoints; //Polilínea
		internal List<Station> mcolStations; // Estaciones en el eje
		internal List<SpeedLimit> mcolSpeedLimits; //Limitaciones de velocidad que afectan a este eje.
		internal List<Signal> mcolSignals; //Señales luminosas en la vía.
		public List<Station> Stations { get => mcolStations; }
		internal static new List<OnyxField> Descriptor()
		{
			List<OnyxField> salida = Lineal.Descriptor();
			salida.Add(new OnyxField("id", "STRING", true, true, false));
			salida.Add(new OnyxField("name", "STRING"));
			salida.Add(new OnyxField("comment", "STRING"));
			salida.Add(new OnyxField("color0", "STRING"));
			salida.Add(new OnyxField("color1", "STRING"));
			return salida;
		}
		private int mvarCurrentIndex { get; set; } //Lo uso para calcular bearing
		internal bool contains(GeoLocation point)
		{
			return (point.Latitude > mvarBounds[0].Latitude) &&
				(point.Latitude < mvarBounds[1].Latitude) &&
				(point.Longitude > mvarBounds[0].Longitude) &&
				(point.Longitude < mvarBounds[1].Longitude);
		}
		internal override bool contains(Punctual rhs)
		{
			if (rhs.GetType() == typeof(Station))
			{
				if(null!=mcolStations)
				{
					foreach (Station auxStation in mcolStations)
						if (auxStation == rhs) return true;
				}
				return false;
			}
			return base.contains(rhs);
		}
		internal int nearestPointIndex(GeoLocation point) //Devuelve el índice del punto del eje más cercano al punto dado.
		{
			if (null==mcolPoints || mcolPoints.Count == 0) return -1;
			int candidateIndex = -1;
			double candidateDistance = double.MaxValue;
			double auxDistance;
			for (int i = 0; i < mcolPoints.Count; i += searchStep)
			{
				auxDistance = point.DistanceTo(mcolPoints[i].point);
				if (auxDistance < candidateDistance)
				{
					candidateIndex = i;
					candidateDistance = auxDistance;
				}
			}
			if (candidateIndex < 0) return -1;
			return nearestPointIndexSlow(point, candidateIndex);
		}
		private int nearestPointIndexSlow(GeoLocation point, int startingIndex)
		{
			if (null == mcolPoints) return startingIndex; //Colección de puntos vacía.
			if (startingIndex < 0) return startingIndex; //Índice fuera de rango.
			double candidateDistance = mcolPoints[startingIndex].point.DistanceTo(point);
			int candidateIndex = startingIndex;
			double auxDistance;
			int minIndex, maxIndex;
			minIndex = (startingIndex > searchStep) ? startingIndex - searchStep : 0;
			maxIndex = (startingIndex + searchStep < mcolPoints.Count) ? startingIndex + searchStep : mcolPoints.Count;
			for (int i = minIndex; i < maxIndex; i++)
			{
				auxDistance = mcolPoints[i].point.DistanceTo(point);
				if (auxDistance < candidateDistance)
				{
					candidateDistance = auxDistance;
					candidateIndex = i;
				}
			}
			return candidateIndex;
		}
		internal Station? nearestStation(GeoLocation point) //Estación más cercana a este punto
		{
			if (null == mcolPoints) return null; //Colección vacía.
			int i = nearestPointIndex(point);
			if (i < 0) return null; //No tenemos nada cerca.
			int upperStation, lowerStation;
			upperStation = nextStationIndex(i, true);
			lowerStation = nextStationIndex(i, false);
			if (upperStation > -1)
			{
				if (lowerStation > -1)
				{
					//Devuelvo la que está a menos distancia...
					if (calculateDistanceAlongAxis(upperStation, i) < calculateDistanceAlongAxis(lowerStation, i))
						return (Station)mcolPoints[upperStation];
					else
						return (Station)mcolPoints[lowerStation];
				}
				else
					return (Station)mcolPoints[upperStation];
			}
			else
			{
				if (lowerStation > -1)
					return (Station)mcolPoints[lowerStation];
			}
			return null;
		}
		internal Station? nearestStation(long rhs) //Estación más cercana a este PK
		{
			if (null == mcolStations) return null; //Colección vacía.
			long candidateDistance = long.MaxValue;
			Station? candidate = null;
			foreach (Station auxStation in mcolStations)
			{
				long auxDistance = auxStation.distanceFrom(rhs);
				if (auxDistance < candidateDistance)
				{
					candidateDistance = auxDistance;
					candidate = auxStation;
				}
			}
			return candidate;
		}
		internal List<Station> nearestStations(long rhs) //Las dos estaciones más cercanas a este PK
		{
			List<Station> salida = new List<Station>();
			Station? anterior = null;
			if (null != mcolStations)
			{
				foreach (Station auxStation in mcolStations)
				{
					if (auxStation.isNear(rhs))
					{ //Estamos en esta estación, así que devolveremos sólo esa estación.
						salida.Add(auxStation);
						return salida;
					}
					else
					{
						if (anterior != null)
						{
							if ((anterior.pk < rhs) && (auxStation.pk > rhs))
							{
								salida.Add(anterior);
								salida.Add(auxStation);
								return salida;
							}
						}
						anterior = auxStation;
					}
				}
			}
			//Llegados a este punto nos hemos pasado el eje completo. Devolvemos una lista vacía.
			return salida;
		}
		internal GeoLocation? nearestPoint(GeoLocation point)
		{
			if (null == mcolPoints) return null; //Colección vacía.
			int i = nearestPointIndex(point);
			if (i < 0) return null;
			return mcolPoints[i].point;
		}
		internal double distanceToPoint(GeoLocation point)
		{
			GeoLocation? auxPoint = nearestPoint(point);
			if (null == auxPoint) return double.MaxValue;
			return ((GeoLocation)auxPoint).DistanceTo(point);
		}
		private int nextValidRefPoint(int refPointIndex, bool forwards)
		{
			int auxIndex = refPointIndex;
			if(null!=mcolPoints)
			{
				if (forwards)
				{
					while (auxIndex < mcolPoints.Count)
					{
						if (mcolPoints[auxIndex].pk >= 0) return auxIndex; //Punto válido como referencia.
						auxIndex++;
					}
				}
				else //Hacia atrás.
				{
					while (auxIndex > -1)
					{
						if (mcolPoints[auxIndex].pk >= 0) return auxIndex; //Punto válido como referencia.
						auxIndex--;
					}
				}
			}
			return -1;
		}
		private int nextStationIndex(int refPointIndex, bool forwards)
		{
			int auxIndex = refPointIndex;
			if(null!=mcolPoints)
			{
				if (forwards)
				{
					while (auxIndex < mcolPoints.Count)
					{
						if (mcolPoints[auxIndex].GetType() == typeof(Station))
							return auxIndex;
						auxIndex++;
					}
				}
				else
				{
					while (auxIndex > -1)
					{
						if (mcolPoints[auxIndex].GetType() == typeof(Station))
							return auxIndex;
						auxIndex--;
					}
				}
			}
			return -1;
		}
		internal double calculateDistanceAlongAxis(int firstIndex, int lastIndex)
		{
			double distance = 0;
			int auxFirst, auxLast;
			if (firstIndex < lastIndex)
			{
				auxFirst = firstIndex;
				auxLast = lastIndex;
			}
			else
			{
				auxFirst = lastIndex;
				auxLast = firstIndex;
			}
			GeoLocation currentLocation;
			Debug.Assert(null != mcolPoints);
			GeoLocation lastLocation = mcolPoints[firstIndex].point;
			for (int i = auxFirst + 1; i < auxLast + 1; i++)
			{
				currentLocation = mcolPoints[i].point;
				distance += currentLocation.DistanceTo(lastLocation);
				lastLocation = currentLocation;
			}
			return distance;
		}
		internal Station? stationById(string id)
		{
			if (null == mcolStations) return null;
			foreach (Station auxStation in mcolStations)
				if (id.Equals(auxStation.id)) return auxStation;
			return null;
		}
		internal long getPk(GeoLocation point)
		{
			mvarCurrentIndex = nearestPointIndex(point);
			if (mvarCurrentIndex < 0) return -1;
			//Buscamos el punto del eje más cercano al punto dado.
			return auxProjectPk(point, mvarCurrentIndex);
		}
		internal float bearing(bool forward)
		{
			if(null!=mcolPoints)
			{
				if (mvarCurrentIndex >= 0)
				{
					if (forward)
					{
						if (mvarCurrentIndex + 1 < mcolPoints.Count)
							return GeoLocation.BearingAngle(mcolPoints[mvarCurrentIndex].point, mcolPoints[mvarCurrentIndex + 1].point);
					}
					else
					{
						if (mvarCurrentIndex > 1)
							return GeoLocation.BearingAngle(mcolPoints[mvarCurrentIndex].point, mcolPoints[mvarCurrentIndex - 1].point);
						else
							return 180;
					}
				}
			}
			return 0;
		}
		internal GeoLocation? getLocation(long Pk, int offset)
		{
			if (null == mcolPoints) return null;
			//Interpolación lineal del punto de salida.
			mvarCurrentIndex = getLocationIndex(Pk);
			if (mvarCurrentIndex < 0) return null;
			GeoLocation origin = mcolPoints[mvarCurrentIndex].point;
			GeoLocation? candidateDown = null;
			GeoLocation? candidateUp = null;
			double candidateDistanceDown = double.MaxValue;
			double candidateDistanceUp = double.MaxValue;

			//Este punto rara vez va a ser igual.            
			if (mvarCurrentIndex > 0)
				candidateDown = auxInterpolation(Pk, mcolPoints[mvarCurrentIndex - 1], mcolPoints[mvarCurrentIndex], offset);
			if (mvarCurrentIndex + 1 < mcolPoints.Count)
				candidateUp = auxInterpolation(Pk, mcolPoints[mvarCurrentIndex], mcolPoints[mvarCurrentIndex + 1], offset);

			if (null != candidateDown)
				candidateDistanceDown = origin.DistanceTo(candidateDown);
			if (null != candidateUp)
				candidateDistanceUp = origin.DistanceTo(candidateUp);

			if (candidateDistanceDown < candidateDistanceUp)
				return candidateDown;
			else if (null != candidateUp)
				return candidateUp;
			else
				return origin;
		}
		internal List<GeoLocation> getSubSegment(long Pk0, long length)
		{
			List<GeoLocation> salida = new List<GeoLocation>();
			int indexInicio = getLocationIndex(Pk0);
			int indexFin = getLocationIndex(Pk0 + length);
			if (indexInicio < 0) indexInicio = 0;
			if (indexFin > mcolPoints.Count) indexFin = mcolPoints.Count;
			for (int i = indexInicio; i < indexFin; i++) salida.Add(mcolPoints[i].point);
			return salida;
		}
		internal Bounds getSubSegmentBounds(long Pk0, long length)
		{
			int indexInicio = getLocationIndex(Pk0);
			int indexFin = getLocationIndex(Pk0 + length);
			Debug.Assert(null != mcolPoints);
			List<RefPunctual> auxLista = new List<RefPunctual>();
			for (int i = indexInicio; i < indexFin; i++)
				auxLista.Add(mcolPoints[i]);
			return polyLineBounds(auxLista);
		}
		private GeoLocation auxInterpolation(long rhs, RefPunctual anterior, RefPunctual posterior, int offset)
		{
			float distPk = posterior.pk - anterior.pk;
			System.Diagnostics.Debug.Assert(distPk >= 0);
			float proporcion = (float)(rhs - anterior.pk) / distPk;
			double deltaX = (posterior.point.Latitude - anterior.point.Latitude) * proporcion;
			double deltaY = (posterior.point.Longitude - anterior.point.Longitude) * proporcion;
			if (offset > 0)
			{
				double angulo = Math.Asin(deltaY);
				//double angulo = Math.PI;
				double auxOffset = (double)offset / -100000;
				double offsetX = auxOffset * Math.Cos(angulo);
				double offsetY = auxOffset * Math.Sin(angulo);
				return new GeoLocation(anterior.point.Latitude + deltaX + offsetX, anterior.point.Longitude + deltaY - offsetY);
			}
			return new GeoLocation(anterior.point.Latitude + deltaX, anterior.point.Longitude + deltaY);
		}
		private int getLocationIndex(long rhs)
		{
			//Buscamos el punto en el eje usando divide y vencerás.
			return auxFindPkDV(rhs, 0, mcolPoints.Count - 1);
		}
		internal string neighbourhod(long rhs) //Devuelve información sobre la ubicación en lenguaje humano
		{
			List<Station> auxStations = nearestStations(rhs);
			if (auxStations.Count == 1)
			{
				return string.Format("In {0}", auxStations[0].name);
			}
			else if (auxStations.Count == 2)
			{
				int mostNear;
				mostNear = (auxStations[0].distanceFrom(rhs) < auxStations[1].distanceFrom(rhs)) ? 0 : 1;
				return string.Format("Between {0} and {1}. (At {2}m from {3})",
					auxStations[0].name,
					auxStations[1].name,
					auxStations[mostNear].distanceFrom(rhs),
					auxStations[mostNear].name);
			}
			return "";
		}
		private long auxProjectPk(GeoLocation rhs, int nearestIndex)
		{
			//Obtiene el Pk del punto más próximo al eje
			try
			{
				Debug.Assert(null != mcolPoints);
				long candidatePk = mcolPoints[nearestIndex].pk;
				candidatePk = auxProjectionFromIndex(nearestIndex - 1, rhs);
				if (candidatePk < 0)
					candidatePk = auxProjectionFromIndex(nearestIndex, rhs);
				if (candidatePk < 0)
					candidatePk = mcolPoints[nearestIndex].pk;
				return candidatePk;
			}
			catch (System.Exception ex)
			{

			}
			return -1;
		}
		private long auxProjectionFromIndex(int index, GeoLocation rhs)
		{
			if(null!=mcolPoints)
			{
				if (index > 0 && index + 1 < mcolPoints.Count)
				{
					RefPunctual anterior = mcolPoints[index];
					RefPunctual siguiente = mcolPoints[index + 1];
					if (GeoLocation.HasProjectionOnSegment(anterior.point, siguiente.point, rhs))
					{
						double auxProjection = GeoLocation.RelativeProjectionOnSegment(anterior.point, siguiente.point, rhs);
						long salida = getPkFromSegment(anterior, siguiente, auxProjection);
						return salida;
					}
				}
			}
			return -1;
		}
		private int auxFindPkDV(long Pk, int minIndex, int maxIndex)
		{
			if (minIndex == maxIndex)
				return minIndex; //Condición de salida.

			int average = (minIndex + maxIndex) / 2;
			long midPk = mcolPoints[average].pk;
			if (midPk >= Pk)
				return auxFindPkDV(Pk, minIndex, average);
			else
				return auxFindPkDV(Pk, average + 1, maxIndex);
		}
		internal GeoPolyline getPoly(Lineal segment)
		{
			GeoPolyline salida = new GeoPolyline();
			if(null!=mcolPoints)
			{
				foreach (RefPunctual point in mcolPoints)
				{
					if (segment.contains(point)) salida.add(point.point);
				}
			}
			return salida;
		}
		internal GeoPolyline getPoly()
		{
			GeoPolyline salida = new GeoPolyline();
			if(null!=mcolPoints)
			{
				foreach (RefPunctual point in mcolPoints)
					salida.add(point.point);
			}
			return salida;
		}
		string Entity.name { get => mvarName; set => mvarName = value; }
		string Entity.comment { get => mvarComment; set => mvarComment = value; }
		String[] Entity.color { get => mvarColor; set => mvarColor = value; }
		internal int mvarMaxSpeed;
		internal int maxSpeed { get => mvarMaxSpeed; }
		public Axis()
		{
			id = "Unnamed";
			mvarName = "Unnamed";
			mvarComment = string.Empty;
			mvarMaxSpeed = 100; //Valor por defecto a remplazar.
			mcolPoints = new List<RefPunctual>();
			mcolStations = new List<Station>();
			mcolSpeedLimits = new List<SpeedLimit>();
			mcolSignals = new List<Signal>();
			mvarColor = new string[2];
			searchStep = 8; //En principio nos saltamos los puntos de 8 en 8 para buscar el más cercano.
		}
		public Axis(XmlNode root):this()
		{
			deserializeXMLHeader(root);
			foreach(XmlNode hijo in root.ChildNodes)
			{
				switch(hijo.Name)
				{
					case "poly":
						deserializeXMLPoly(hijo);
						break;
					case "limit":
						deserializeXMLLimits(hijo);
						break;
					case "signal":
						deserializeXMLSignals(hijo);
						break;
				}
			}
		}
		private void deserializeXMLHeader(XmlNode root)
		{
			this.id = XMLUtil.StringParam(root, "id");
			this.mvarName = XMLUtil.StringParam(root, "name");
			this.mvarComment = XMLUtil.StringParam(root, "comment");
			this.mvarMaxSpeed = XMLUtil.IntParam(root, "vmax");
			this.mvarColor[0] = XMLUtil.StringParam(root, "color");
			this.mvarColor[1] = XMLUtil.StringParam(root, "darkcolor");
		}
		private void deserializeXMLPoly(XmlNode root)
		{
			foreach(XmlNode hijo in root.ChildNodes)
			{
				if("point"==hijo.Name)
				{
					GeoLocation auxLocation = XMLUtil.GeoLocationParam(hijo);
					string auxId = XMLUtil.StringParam(hijo, "id");
					if(string.Empty==auxId) //Punto vacío
					{
						RefPunctual auxPunto = new RefPunctual(hijo);
						mcolPoints.Add(auxPunto);
					}
					else
					{
						Station auxStation = new Station(hijo,this);
						mcolPoints.Add(auxStation);
						mcolStations.Add(auxStation);
					}
				}
			}
			if(mcolPoints.Count>0)
			{
				this.pk = mcolPoints.First().pk;
				this.length = mcolPoints.Last().pk - this.pk;
			}			
		}
		private void deserializeXMLLimits(XmlNode root)
		{
			foreach(XmlNode hijo in root.ChildNodes)
			{
				if("item"==hijo.Name)
				{
					SpeedLimit nuevo = new SpeedLimit(hijo);
					mcolSpeedLimits.Add(nuevo);
				}
			}
		}
		private void deserializeXMLSignals(XmlNode root)
		{
			foreach(XmlNode hijo in root.ChildNodes)
			{
				if("item"==hijo.Name)
				{
					Signal nueva = new Signal(hijo);
					mcolSignals.Add(nueva);
				}
			}
		}		
		internal Bounds getBounds()
		{
			List<RefPunctual> points = new List<RefPunctual>();
			foreach (Station auxEstacion in mcolStations)
				points.Add(auxEstacion);
			return polyLineBounds(points);
		}
		static internal Bounds polyLineBounds(List<RefPunctual> elements)
		{
			double minLat, maxLat;
			double minLon, maxLon;
			minLat = double.MaxValue; maxLat = double.MinValue;
			minLon = double.MaxValue; maxLon = double.MinValue;
			foreach (RefPunctual element in elements)
			{
				GeoLocation auxLoca = element.point;
				if (auxLoca.Latitude < minLat) minLat = auxLoca.Latitude;
				if (auxLoca.Longitude < minLon) minLon = auxLoca.Longitude;
				if (auxLoca.Latitude > maxLat) maxLat = auxLoca.Latitude;
				if (auxLoca.Longitude > maxLon) maxLon = auxLoca.Longitude;
			}
			return new Bounds(minLat, minLon, maxLat, maxLon);
		}
		private long getPkFromSegment(RefPunctual segmentBegin, RefPunctual segmentEnd, double distanceFromBegin)
		{
			double auxDistancia = segmentBegin.point.DistanceTo(segmentEnd.point);
			long distanciaPk = segmentEnd.pk - segmentBegin.pk;
			//distanceFromBegin  -->  auxDistancia
			//      X           --> distanciaPk
			if (auxDistancia < 0.00001) return segmentBegin.pk;

			return (long)(distanciaPk * distanceFromBegin / auxDistancia) + segmentBegin.pk;
		}
		internal List<SpeedLimit> getTemporalLimitations(byte track=255, long pkIni=-1, long pkEnd=-1, bool onlyActives=false)
		{
			List<SpeedLimit> salida = new List<SpeedLimit>();
			foreach (SpeedLimit lim in mcolSpeedLimits)
			{
				if((track&lim.Tracks)!=0)
				{
					if(pkIni==-1||pkIni<=lim.pk)
					{
						if(pkEnd==-1||pkEnd>=lim.pkEnd)
						{
							salida.Add(lim);
						}
					}
				}
			}
			return salida;
		}
		internal GeoPolyline? getLimitationPolyline(SpeedLimit item)
		{
			List<GeoLocation> puntos = getSubSegment(item.pk, item.length);
			if (puntos.Count > 0)
			{
				GeoPolyline nuevo = new GeoPolyline();
				foreach (GeoLocation punto in puntos)
					nuevo.add(punto);
				return nuevo;
			}
			return null;
		}
	}
}