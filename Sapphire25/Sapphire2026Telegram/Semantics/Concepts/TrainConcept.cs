using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sapphire2026.Data.Models;
using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;

namespace Sapphire2025Server.Telegram.Semantics.Concepts
{
	/// <summary>
	/// Es un tipo de concepto que representa un tren o una operación relacionada con un tren.
	/// </summary>
	public class TrainConcept: GeneralConcept
	{
		internal List<Train> mcolTrains;
		public TrainConcept(string name,string rhs, IConfiguration config):base(name,string.Concat(rhs,",ut,tren,material,movil,unidad,coche,remolque,vehículo,convoy"),config){ mcolTrains = new List<Train>(); }

		public async override Task<byte> match(string[] text)
		{
			int instancias = 0;
			int totales = mcolTokens.Length;
			//Primero busca las palabras clave en el contexto.
			foreach( string palabra in text)
			{
				if (mcolTokens.Contains(palabra))
					instancias++;
			}
			//Ahora busca tokens relacionados con los trenes.
			using (DataStorage almacen = new DataStorage(mvarConfig))
			{
				mcolTrains = new List<Train>();
				List<Train> colTrains = await almacen.Trains.ToListAsync();
				foreach (Train tren in colTrains)
				{
					if(!mcolTrains.Contains(tren))
					{
						foreach (string trainToken in tren.NameCloud.Split(','))
						{
							if (text.Contains(trainToken))
							{
								instancias++;
								totales++;
								mcolTrains.Add(tren);
							}
						}
					}
				}
			}
			if(totales>0)
			{
				float resultado = (instancias * 255) / totales;
				return (byte)resultado;
			}
			return 0; //Sólo en caso de error.				
		}



	}
}
