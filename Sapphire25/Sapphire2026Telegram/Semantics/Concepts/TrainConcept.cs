using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Primitives;
using Sapphire2026.Data.Models;
using System.Diagnostics;
using System.Diagnostics.Eventing.Reader;
using System.Net.Http.Headers;
using System.Net.Mime;
using System.Text;
using System.Text.RegularExpressions;

namespace Sapphire2025Server.Telegram.Semantics.Concepts
{
	/// <summary>
	/// Es un tipo de concepto que representa un tren o una operación relacionada con un tren.
	/// </summary>
	internal class TrainConcept: GeneralConcept
	{
		internal List<Train> mcolTrains;
		public TrainConcept(IConfiguration config):base(config)
		{
			AddTokens(new string[]{"ut","tren","material","movil","unidad",
				"coche","remolque","vehículo","convoy" });
			mcolTrains = new List<Train>(); 
		}

		/// <summary>
		/// Esta función busca los trenes de la flota en la base de datos e intenta encontrar alusiones a ellos en la
		/// lista de tokens.
		/// </summary>
		/// <param name="tokens">Cadena preprocesada de petición</param>
		/// <returns>Nada... la lista de trenes se carga en mcolTrains</returns>
		internal async Task LocateTrains(string[] tokens)
		{
			using (DataStorage almacen = new DataStorage(mvarConfig))
			{
				List<Train> dbTrains = await almacen.Trains.ToListAsync();
                foreach (var item in dbTrains)
                {
                    if(!mcolTrains.Contains(item))
					{
						foreach(string trainToken in item.NameCloud.Split(","))
						{
							if (tokens.Contains(trainToken))
							{
								mcolTrains.Add(item);
								break; //Sale del bucle. No quiero añadir dos veces un tren.
							}								
						}
					}
                }
            }
		}

		/// <summary>
		/// Devuelve la enumeración del tren o trenes que contiene este concepto
		/// </summary>
		/// <returns></returns>
		internal string TrainVerbose(bool female = false, bool acusative = false)
		{
			if(mcolTrains.Count<1)
			{
				if(female)
				{
					if (acusative)
						return "a ninguna unidad";
					else
						return "ninguna unidad";
				}
				else
				{
					if (acusative)
						return "a ningún tren";
					else
						return "ningún tren";
				}
			}
			else if(1==mcolTrains.Count)
			{
				if(female)
				{
					if(acusative)
						return string.Format("a la unidad {0}",mcolTrains.First().Name);
					else
						return string.Format("la unidad {0}", mcolTrains.First().Name);
				}
				else
				{
					if (acusative)
						return string.Format("al tren {0}", mcolTrains.First().Name);
					else
						return string.Format("el tren {0}", mcolTrains.First().Name);
				}
			}
			else
			{
				if (female)
				{
					if (acusative)
						return string.Format("a las unidades {0}", TrainEnumeration());
					else
						return string.Format("las unidades {0}", TrainEnumeration());
				}
				else
				{
					if (acusative)
						return string.Format("a los trenes {0}", TrainEnumeration());
					else
						return string.Format("los trenes {0}", TrainEnumeration());
				}
			}
		}

		private string TrainEnumeration()
		{
			StringBuilder salida = new StringBuilder();
			foreach (var item in mcolTrains)
			{
				if (item == mcolTrains.Last())
					salida.Append("y ");
				else if (item != mcolTrains.First())
					salida.Append(", ");

				salida.Append(item.Name);					
			}
			return salida.ToString();
		}
	}
}
