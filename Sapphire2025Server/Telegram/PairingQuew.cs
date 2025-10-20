using Google.Protobuf.Reflection;

namespace Sapphire2025Server.Telegram
///Cola de emparejamientos de Telegram.
///Contiene los enlaces de emparejamiento para vincular una cuenta de Telegram
///a una cuenta de Zafiro.

{
	public class PairingQuew
	{
		private Dictionary<string,PairingIntent> mcolPairingQuew;
		public TimeSpan Expiry { get; set; }
		public PairingQuew(TimeSpan expiry)
		{
			Expiry = expiry;
			mcolPairingQuew = new Dictionary<string, PairingIntent>();
		}
		public PairingQuew()
		{
			Expiry = new TimeSpan(0, 10, 0);
			mcolPairingQuew = new Dictionary<string, PairingIntent>();
		}

		/// <summary>
		/// Devuelve el usuario que ha pedido emparejar la sesión
		/// </summary>
		/// <param name="rhs">Id de emparejamiento</param>
		/// <returns>Guid del usuario o empty si no hay usuario</returns>
		public Guid getPairingUserId(string rhs)
		{
			auxPurgePairing();
			if (mcolPairingQuew.ContainsKey(rhs))
				return mcolPairingQuew[rhs].userId;
			return Guid.Empty;
		}

		public string GenerateNew(Guid userId)
		{
			auxPurgePairing();
			PairingIntent nuevo = new PairingIntent();
			nuevo.expiry = DateTime.Now.Add(Expiry);
			nuevo.userId = userId;
			nuevo.pairingString = auxGeneratePairingId();
			mcolPairingQuew.Add(nuevo.pairingString, nuevo);
			return nuevo.pairingString;
		}
		private void auxPurgePairing()
		{
			Dictionary<string, PairingIntent> auxSalida = new Dictionary<string, PairingIntent>();
			foreach(KeyValuePair<string,PairingIntent> candidato in mcolPairingQuew)
			{
				if (candidato.Value.expiry > DateTime.Now)
					auxSalida.Add(candidato.Key,candidato.Value);
			}
			mcolPairingQuew = auxSalida;
		}
		private string auxGeneratePairingId()
		{
			const string caracteres = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
			var random = new Random();
			while (true)
			{
				string salida = new string(Enumerable.Range(0, 4)
					.Select(_ => caracteres[random.Next(caracteres.Length)]).ToArray());
				if (!mcolPairingQuew.ContainsKey(salida)) return salida;
			}
		}
	}


	public class PairingIntent
	{
		public Guid userId { get; set; }
		public DateTime expiry { get; set; }
		public string? pairingString { get; set; }
	}

}
