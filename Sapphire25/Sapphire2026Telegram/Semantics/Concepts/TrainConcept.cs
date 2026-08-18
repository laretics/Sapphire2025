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
using TorchSharp.Modules;

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
			using IServiceScope scope = mvarServiceProvider.CreateScope();
			AeneasClient auxCliente = scope.ServiceProvider.GetRequiredService<AeneasClient>();
			IEnumerable<Sapphire2025Models.Aeneas.TrainModel> auxColTrains = await auxCliente.trainsList();
			foreach (Sapphire2025Models.Aeneas.TrainModel item in auxColTrains)
			{
				if(!mcolTrains.Contains(item))
				{
					if(!string.IsNullOrEmpty(item.nameCloud))
					{
						foreach (string trainToken in item.nameCloud.Split(","))
						{
							string pattern = "^" + Regex.Escape(trainToken).Replace("\\#", "[-_]?") + "$";
							foreach (string token in tokens)
							{
								if (Regex.IsMatch(token, pattern, RegexOptions.IgnoreCase))
								{
									mcolTrains.Add(item);
									break; //Sale del bucle. No quiero añadir dos veces un tren.
								}
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
		internal string TrainVerbose(Sapphire2025Models.I18n.UiLocale locale, bool female = false, bool acusative = false)
		{
			if(mcolTrains.Count<1)
			{
				if(female)
					return Sapphire2025Models.I18n.TelegramI18n.T(locale, acusative ? "tg.train.none.f.acc" : "tg.train.none.f");
				return Sapphire2025Models.I18n.TelegramI18n.T(locale, acusative ? "tg.train.none.m.acc" : "tg.train.none.m");
			}
			if(1==mcolTrains.Count)
			{
				string name = mcolTrains.First().name;
				if(female)
					return Sapphire2025Models.I18n.TelegramI18n.T(locale, acusative ? "tg.train.one.f.acc" : "tg.train.one.f", name);
				return Sapphire2025Models.I18n.TelegramI18n.T(locale, acusative ? "tg.train.one.m.acc" : "tg.train.one.m", name);
			}

			string list = TrainEnumeration(locale);
			if (female)
				return Sapphire2025Models.I18n.TelegramI18n.T(locale, acusative ? "tg.train.many.f.acc" : "tg.train.many.f", list);
			return Sapphire2025Models.I18n.TelegramI18n.T(locale, acusative ? "tg.train.many.m.acc" : "tg.train.many.m", list);
		}

		private string TrainEnumeration(Sapphire2025Models.I18n.UiLocale locale)
		{
			StringBuilder salida = new StringBuilder();
			string conj = Sapphire2025Models.I18n.UiCatalog.Get(locale, "tg.train.and");
			foreach (var item in mcolTrains)
			{
				if (item == mcolTrains.Last())
					salida.Append(conj);
				else if (item != mcolTrains.First())
					salida.Append(", ");

				salida.Append(item.name);					
			}
			return salida.ToString();
		}
	}
}
