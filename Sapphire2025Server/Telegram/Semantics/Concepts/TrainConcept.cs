using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sapphire2025Server.Models;
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
		internal List<Models.Train> mcolTrains;
		public TrainConcept(string name,string rhs):base(name,string.Concat(rhs,",ut,tren,material,movil,unidad,coche,remolque,vehículo,convoy")){ mcolTrains = new List<Train>(); }

		public async override Task<byte> match(List<string> text)
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
			using (DataStorage almacen = new DataStorage(BotSoul.config))
			{
				mcolTrains = new List<Train>();
				List<Models.Train> colTrains = await almacen.Trains.ToListAsync();
				foreach (Models.Train tren in colTrains)
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
