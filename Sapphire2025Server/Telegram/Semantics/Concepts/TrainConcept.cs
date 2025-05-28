using Microsoft.EntityFrameworkCore;
using Org.BouncyCastle.Security;
using Sapphire2025Server.Models;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace Sapphire2025Server.Telegram.Semantics.Concepts
{
	/// <summary>
	/// Es un tipo de concepto que representa un tren.
	/// </summary>
	public class TrainConcept : ObjectConcept
	{
		internal static Dictionary<string, Train>? mcolTrains;
		internal Train? mvarTren = null;

		public override async Task<bool> match(string[] text)
		{
			if(null==mcolTrains) await initializeTrainCollection();
			Debug.Assert(null != mcolTrains, "No se ha inicializado la colección de trenes");
			foreach (string palabra in text)
			{
				foreach (KeyValuePair<string, Train> pareja in mcolTrains)
				{
					if (MatchWithComodin(palabra, pareja.Key))
					{
						mvarTren = pareja.Value;
						return true;
					}
				}
			}
			return await base.match(text);
		}

		/// <summary>
		/// Cargamos la colección de trenes desde la base de datos.
		/// </summary>
		private async Task initializeTrainCollection()
		{
			mcolTrains = new Dictionary<string, Train>();
			using (DataStorage almacen = new DataStorage(BotTask.config))
			{
				foreach (Train tren in await almacen.Trains.ToListAsync())
				{
					string[] palabras = tren.NameCloud.Split(",");
					foreach (string palabra in palabras)
					{
						if (!mcolTrains.ContainsKey(palabra))
						{
							mcolTrains.Add(palabra, tren);
						}
					}
				}
			}
		}
			
		private bool MatchWithComodin(string entrada, string patronConAlmohadilla)
		{
			string patron = Regex.Escape(patronConAlmohadilla).Replace("\\#", "[-_/.:;]?");
			patron = $"^{patron}$";
			return Regex.IsMatch(entrada, patron, RegexOptions.IgnoreCase);
		}
	}
}
