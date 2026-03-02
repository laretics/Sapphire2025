using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using TimeNet2026.Auxiliar;
using TimeNet2026.Storage;
using static System.Collections.Specialized.BitVector32;

namespace TimeNet2026.Topo
{
	public class TopoAxis:Lineal
	{
		internal const double BOUNDS_PERCENTAGE = 0.05;
		internal GeoLocation[] mvarBounds = new GeoLocation[2];
		//Utilizo el rectángulo para detectar rápidamente si un punto dado puede pertenecer a un eje.
		internal int searchStep { get; set; } //Valor de salto para la caché de búsqueda de puntos en el eje.
		
		internal List<RefPunctual> Points; //Polilínea
		
		internal List<SpeedLimit> SpeedLimits; //Limitaciones de velocidad que afectan a este eje.
		internal List<Signal> Signals; //Señales luminosas en la vía.		
		private int mvarCurrentIndex { get; set; } //Lo uso para calcular bearing
		internal bool contains(GeoLocation point)
		{
			return (point.Latitude > mvarBounds[0].Latitude) &&
				(point.Latitude < mvarBounds[1].Latitude) &&
				(point.Longitude > mvarBounds[0].Longitude) &&
				(point.Longitude < mvarBounds[1].Longitude);
		}
		internal int nearestPointIndex(GeoLocation point) //Devuelve el índice del punto del eje más cercano al punto dado.
		{
			if (null==Points || Points.Count == 0) return -1;
			int candidateIndex = -1;
			double candidateDistance = double.MaxValue;
			double auxDistance;
			for (int i = 0; i < Points.Count; i += searchStep)
			{
				auxDistance = point.DistanceTo(Points[i].point);
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
			if (null == Points) return startingIndex; //Colección de puntos vacía.
			if (startingIndex < 0) return startingIndex; //Índice fuera de rango.
			double candidateDistance = Points[startingIndex].point.DistanceTo(point);
			int candidateIndex = startingIndex;
			double auxDistance;
			int minIndex, maxIndex;
			minIndex = (startingIndex > searchStep) ? startingIndex - searchStep : 0;
			maxIndex = (startingIndex + searchStep < Points.Count) ? startingIndex + searchStep : Points.Count;
			for (int i = minIndex; i < maxIndex; i++)
			{
				auxDistance = Points[i].point.DistanceTo(point);
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
			if (null == Points) return null; //Colección vacía.
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
						return (Station)Points[upperStation];
					else
						return (Station)Points[lowerStation];
				}
				else
					return (Station)Points[upperStation];
			}
			else
			{
				if (lowerStation > -1)
					return (Station)Points[lowerStation];
			}
			return null;
		}
		internal GeoLocation? nearestPoint(GeoLocation point)
		{
			if (null == Points) return null; //Colección vacía.
			int i = nearestPointIndex(point);
			if (i < 0) return null;
			return Points[i].point;
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
			if(null!=Points)
			{
				if (forwards)
				{
					while (auxIndex < Points.Count)
					{
						if (Points[auxIndex].pk >= 0) return auxIndex; //Punto válido como referencia.
						auxIndex++;
					}
				}
				else //Hacia atrás.
				{
					while (auxIndex > -1)
					{
						if (Points[auxIndex].pk >= 0) return auxIndex; //Punto válido como referencia.
						auxIndex--;
					}
				}
			}
			return -1;
		}
		private int nextStationIndex(int refPointIndex, bool forwards)
		{
			int auxIndex = refPointIndex;
			if(null!=Points)
			{
				if (forwards)
				{
					while (auxIndex < Points.Count)
					{
						if (Points[auxIndex].GetType() == typeof(Station))
							return auxIndex;
						auxIndex++;
					}
				}
				else
				{
					while (auxIndex > -1)
					{
						if (Points[auxIndex].GetType() == typeof(Station))
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
			Debug.Assert(null != Points);
			GeoLocation lastLocation = Points[firstIndex].point;
			for (int i = auxFirst + 1; i < auxLast + 1; i++)
			{
				currentLocation = Points[i].point;
				distance += currentLocation.DistanceTo(lastLocation);
				lastLocation = currentLocation;
			}
			return distance;
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
			if(null!=Points)
			{
				if (mvarCurrentIndex >= 0)
				{
					if (forward)
					{
						if (mvarCurrentIndex + 1 < Points.Count)
							return GeoLocation.BearingAngle(Points[mvarCurrentIndex].point, Points[mvarCurrentIndex + 1].point);
					}
					else
					{
						if (mvarCurrentIndex > 1)
							return GeoLocation.BearingAngle(Points[mvarCurrentIndex].point, Points[mvarCurrentIndex - 1].point);
						else
							return 180;
					}
				}
			}
			return 0;
		}
		internal GeoLocation? getLocation(long Pk, int offset)
		{
			if (null == Points) return null;
			//Interpolación lineal del punto de salida.
			mvarCurrentIndex = getLocationIndex(Pk);
			if (mvarCurrentIndex < 0) return null;
			GeoLocation origin = Points[mvarCurrentIndex].point;
			GeoLocation? candidateDown = null;
			GeoLocation? candidateUp = null;
			double candidateDistanceDown = double.MaxValue;
			double candidateDistanceUp = double.MaxValue;

			//Este punto rara vez va a ser igual.            
			if (mvarCurrentIndex > 0)
				candidateDown = auxInterpolation(Pk, Points[mvarCurrentIndex - 1], Points[mvarCurrentIndex], offset);
			if (mvarCurrentIndex + 1 < Points.Count)
				candidateUp = auxInterpolation(Pk, Points[mvarCurrentIndex], Points[mvarCurrentIndex + 1], offset);

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
			if (indexFin > Points.Count) indexFin = Points.Count;
			for (int i = indexInicio; i < indexFin; i++) salida.Add(Points[i].point);
			return salida;
		}
		internal Bounds getSubSegmentBounds(long Pk0, long length)
		{
			int indexInicio = getLocationIndex(Pk0);
			int indexFin = getLocationIndex(Pk0 + length);
			Debug.Assert(null != Points);
			List<RefPunctual> auxLista = new List<RefPunctual>();
			for (int i = indexInicio; i < indexFin; i++)
				auxLista.Add(Points[i]);
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
			return auxFindPkDV(rhs, 0, Points.Count - 1);
		}

		private long auxProjectPk(GeoLocation rhs, int nearestIndex)
		{
			//Obtiene el Pk del punto más próximo al eje
			try
			{
				Debug.Assert(null != Points);
				long candidatePk = Points[nearestIndex].pk;
				candidatePk = auxProjectionFromIndex(nearestIndex - 1, rhs);
				if (candidatePk < 0)
					candidatePk = auxProjectionFromIndex(nearestIndex, rhs);
				if (candidatePk < 0)
					candidatePk = Points[nearestIndex].pk;
				return candidatePk;
			}
			catch (System.Exception ex)
			{

			}
			return -1;
		}
		private long auxProjectionFromIndex(int index, GeoLocation rhs)
		{
			if(null!=Points)
			{
				if (index > 0 && index + 1 < Points.Count)
				{
					RefPunctual anterior = Points[index];
					RefPunctual siguiente = Points[index + 1];
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
			long midPk = Points[average].pk;
			if (midPk >= Pk)
				return auxFindPkDV(Pk, minIndex, average);
			else
				return auxFindPkDV(Pk, average + 1, maxIndex);
		}
		internal GeoPolyline getPoly(Lineal segment)
		{
			GeoPolyline salida = new GeoPolyline();
			if(null!=Points)
			{
				foreach (RefPunctual point in Points)
				{
					if (segment.contains(point)) salida.add(point.point);
				}
			}
			return salida;
		}
		internal GeoPolyline getPoly()
		{
			GeoPolyline salida = new GeoPolyline();
			if(null!=Points)
			{
				foreach (RefPunctual point in Points)
					salida.add(point.point);
			}
			return salida;
		}
		public TopoAxis():base()
		{
			Points = new List<RefPunctual>();			
			SpeedLimits = new List<SpeedLimit>();
			Signals = new List<Signal>();
			
			searchStep = 8; //En principio nos saltamos los puntos de 8 en 8 para buscar el más cercano.
		}
		public void RecalculateLinearBounds()
		{
			if(Points.Count>0)
			{
				this.pk = Points.First().pk;
				this.length = Points.Last().pk - this.pk;
			}
		}

		internal void deserializeXPoly(XElement root, Axis parent)
		{

			if(root is XElement element)
			{
				foreach(XElement child in element.Elements())
				{
					if("point"==child.Name.LocalName)
					{
						GeoLocation auxLocation = XUtil.GeoLocationParam(child);
						string auxId = XUtil.StringParam(child, "id");
						if (string.Empty == auxId) //Punto vacío
						{
							RefPunctual auxPunto = new RefPunctual(child);
							Points.Add(auxPunto);
						}
						else
						{
							Station auxStation = new Station(child, parent);
							Points.Add(auxStation);
							parent.Stations.Add(auxStation);
						}
					}
				}
			}
			if (Points.Count>0)
				recalculatePK(); //Asigno los PK de cada punto en función de las referencias
			RecalculateLinearBounds();
		}
		private void recalculatePK()
		{
			int lastPkIndex = 0; //Índice del último punto con contenido distinto de -1
			int nextPkIndex = auxCalculateNextPkIndex(lastPkIndex); //Índice del siguiente punto con contenido distinto de -1.
			while (-1 != nextPkIndex)
			{
				long pkIni = Points[lastPkIndex].pk;
				long pkFin = Points[nextPkIndex].pk;
				long distancia = pkFin - pkIni;
				Debug.Assert(distancia > 0);
				double distanciaGeograficaTotal = 0;
				//Primero tengo que calcular la distancia entre puntos geográficos
				for(int i=lastPkIndex+1;i<nextPkIndex;i++)
					distanciaGeograficaTotal += Points[i - 1].point.DistanceTo(Points[i].point);
				Debug.Assert(distanciaGeograficaTotal > 0);				
				//Ahora puedo calcular el pk.
				double acumulado = Points[lastPkIndex].point.DistanceTo(Points[lastPkIndex+1].point);
                for (int i=lastPkIndex+1;i<nextPkIndex;i++)
				{
					acumulado += Points[i - 1].point.DistanceTo(Points[i].point);
					long resultado = (long)((acumulado * distancia) / distanciaGeograficaTotal);
					Points[i].pk = resultado + pkIni;
					//Console.WriteLine(string.Format("Point ({0},{1}) at pk {2}", Points[i].point.Latitude, Points[i].point.Longitude, Points[i].pk));
				}

				lastPkIndex = nextPkIndex;
				nextPkIndex = auxCalculateNextPkIndex(lastPkIndex);
			}
		}
		private int auxCalculateNextPkIndex(int lastPkIndex)
		{
			for (int i =lastPkIndex+1; i<Points.Count;i++)
			{
				if (-1 != Points[i].pk)
					return i;
			}
			return -1; //Valor de error. Nos hemos salido del eje.
		}
		internal void deserializeXLimits(XNode root)
		{
			if(root is XElement element)
			{
				foreach (XElement hijo in element.Elements())
				{
					if ("item" == hijo.Name)
					{
						SpeedLimit nuevo = new SpeedLimit(hijo);
						SpeedLimits.Add(nuevo);
					}
				}
			}
		}
		internal void deserializeXSignals(XNode root)
		{
			if (root is XElement element)
			{
				foreach (XElement hijo in element.Elements())
				{
					if ("item" == hijo.Name)
					{
						Signal nueva = new Signal(hijo);
						Signals.Add(nueva);
					}
				}
			}


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
			foreach (SpeedLimit lim in SpeedLimits)
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