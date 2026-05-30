using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Primitives;
using Sapphire2025.Storage;
using Sapphire2026.Data;
using Sapphire2026.Data.Models;
using System.Diagnostics;
using System.Diagnostics.Eventing.Reader;
using System.Net.Http.Headers;
using System.Net.Mime;
using System.Text;
using System.Text.RegularExpressions;

namespace Sapphire2026Telegram.Semantics.Concepts
{
	/// <summary>
	/// Es un tipo de concepto que representa un tren o una operación relacionada con un tren.
	/// </summary>
	internal class TrainConcept: GeneralConcept
	{
		internal List<Sapphire2025Models.Aeneas.TrainModel> mcolTrains;
		public TrainConcept(IConfiguration config, IServiceProvider provider):base(config,provider)
		{
			mcolTrains = new List<Sapphire2025Models.Aeneas.TrainModel>(); 
		}

		internal async override Task AddText(string text)
		{
			await base.AddText(text);
			string[] auxTokens = text.Split(' ');
			await LocateTrains(auxTokens);
		}
		/// <summary>
		/// Esta función busca los trenes de la flota en la base de datos e intenta encontrar alusiones a ellos en la
		/// lista de tokens.
		/// </summary>
		/// <param name="tokens">Cadena preprocesada de petición</param>
		/// <returns>Nada... la lista de trenes se carga en mcolTrains</returns>
		internal async Task LocateTrains(string[] tokens)
		{
			AeneasClient auxCliente = mvarServiceProvider.GetRequiredService<AeneasClient>();
			List<Sapphire2025Models.Aeneas.TrainModel> auxColTrains = new List<Sapphire2025Models.Aeneas.TrainModel>();
			foreach (Sapphire2025Models.Aeneas.TrainModel item in auxColTrains)
			{
				if(!mcolTrains.Contains(item))
				{
					foreach(string trainToken in item.nameCloud.Split(","))
					{
						string pattern = "^" + Regex.Escape(trainToken).Replace("\\#", "[-_]?") + "$";
						foreach (string token in tokens)
						{
							if(Regex.IsMatch(token,pattern,RegexOptions.IgnoreCase))
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
						return string.Format("a la unidad {0}",mcolTrains.First().name);
					else
						return string.Format("la unidad {0}", mcolTrains.First().name);
				}
				else
				{
					if (acusative)
						return string.Format("al tren {0}", mcolTrains.First().name);
					else
						return string.Format("el tren {0}", mcolTrains.First().name);
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

				salida.Append(item.name);					
			}
			return salida.ToString();
		}
	}
}
