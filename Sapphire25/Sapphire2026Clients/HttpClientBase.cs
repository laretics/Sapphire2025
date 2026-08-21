using Sapphire2025Models;
using Sapphire2025Models.Authentication;
using Sapphire2026Clients;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace Sapphire2025.Storage
{
	/// <summary>
	/// Esto es un cliente genérico que consume un servicio HTTP rest.
	/// </summary>
	public abstract class HttpClientBase
	{
		public string controllerId { get;private set; }
		internal readonly HttpClient mvarClient;
		internal readonly IntStorageService? mvarIntStorage;
		internal readonly SessionService? mvarSessionService;

		public HttpClientBase(
		HttpClient httpClient,
		IntStorageService intStorageService, 
		SessionService? sessionService,
		string controllerId)
		{
			mvarClient = httpClient;
			mvarIntStorage = intStorageService;
			mvarSessionService = sessionService;
			this.controllerId = controllerId;
		}

		/// <summary>
		/// Obtiene el token de sesión del usuario que está realizando la operación.
		/// </summary>
		/// <returns>Guid del token asignado o Guid.empty si no hay sesión</returns>
		internal async Task<Guid> getCurrentToken()
		{
			Guid salida = Guid.Empty;

			SessionModel? sesion = await mvarIntStorage.GetSessionInfo();

			if (null != sesion)
				return sesion.Token;

			return Guid.Empty; //En caso de no encontrar sesión devolvemos un token vacío.
		}

		internal string composeUri(string command)
		{
			return string.Format("{0}/{1}", controllerId, command);
		}

		internal string composeCommand(string command, params requestParam[] arguments)
		{
			if (0 == arguments.Length)
			{
				return composeUri(command);
			}
			else
			{
				StringBuilder sb = new StringBuilder();
				bool primera = true;
				foreach (var arg in arguments)
				{
					if (primera)
						sb.Append("?");
					else
						sb.Append("&");
					primera = false;
					sb.Append(arg.key);
					sb.Append("=");
					sb.Append(arg.value);
				}
				return string.Format("{0}{1}",composeUri(command), sb.ToString());
			}
		}
		/// <summary>
		/// Get es para recibir un envío masivo de datos con pocos parámetros.
		/// </summary>
		/// <param name="request">Cadena de petición (comando+argumentos)</param>
		/// <returns>HttpResponse</returns>
		internal async Task<HttpResponseMessage> sendGetRequest(string request, bool checkSession = true)
		{
			if (checkSession)
				await EnsureActiveSession();

			HttpResponseMessage salida = await mvarClient.GetAsync(request);
			salida.EnsureSuccessStatusCode();
			return salida;
		}


		/// <summary>
		/// Put es para editar un elemento que ya existe en la base de datos
		/// </summary>
		/// <param name="commandId">Comando del servidor</param>
		/// <param name="jsonString">Objeto del modelo en formato json</param>
		/// <returns>HttpResponse</returns>
		internal async Task<HttpResponseMessage> sendPutRequest(string commandId, string jsonString, bool checkSession = true)
		{
			if (checkSession)
				await EnsureActiveSession();

			HttpContent paquete = new StringContent(jsonString, Encoding.UTF8, "application/json");
			string auxCommand = composeUri(commandId);
			HttpResponseMessage salida = await mvarClient.PutAsync(auxCommand, paquete);
			salida.EnsureSuccessStatusCode();
			return salida;
		}

		/// <summary>
		/// Post es para generar un nuevo elemento que no existía en la base de datos
		/// </summary>
		/// <param name="commandId">Comando del servidor</param>
		/// <param name="jsonString">Objeto del modelo en formato json</param>
		/// <returns></returns>
		internal async Task<HttpResponseMessage> sendPostRequest(string commandId, string jsonString, bool checkSession = true)
		{
			if(checkSession)
				await EnsureActiveSession();

			HttpContent paquete = new StringContent(jsonString, Encoding.UTF8, "application/json");
			string auxCommand = composeUri(commandId);
			HttpResponseMessage salida = await mvarClient.PostAsync(auxCommand, paquete);
			salida.EnsureSuccessStatusCode();
			return salida;
		}
		internal async Task<HttpResponseMessage> sendPostRequest(string commandId, MultipartFormDataContent content, bool checkSession = true)
		{
			if(checkSession)
				await EnsureActiveSession();	

			string auxCommand = composeUri(commandId);
            HttpResponseMessage salida = await mvarClient.PostAsync(auxCommand, content);
            salida.EnsureSuccessStatusCode();
            return salida;	
        }

		internal async Task EnsureActiveSession()
		{
			if (null == mvarIntStorage || null == mvarSessionService) return;

			SessionModel? session = await mvarIntStorage.GetSessionInfo();

			//Sin sesión tenemos un usuario anónimo o con la sesión sin iniciar. No es una caducidad.
			if(null==session || Guid.Empty == session.Token) return;

			if(!await mvarIntStorage.IsSessionExpiredLocally())
				return;

			// Reloj local vencido: renovar en el servidor en lugar de tumbar la consulta.
			// Si el ping falla (red del tren, etc.) se deja pasar la petición; el servidor decide.
			await TryRefreshSessionExpiry(session.Token);
		}

		/// <summary>
		/// Sliding expiry: el servidor alarga <c>Expiry</c> y devolvemos el nuevo instante al almacén local.
		/// </summary>
		internal async Task<bool> TryRefreshSessionExpiry(Guid token)
		{
			if (Guid.Empty.Equals(token) || null == mvarIntStorage)
				return false;

			try
			{
				BasicRequestModel request = new BasicRequestModel(token);
				string json = JsonSerializer.Serialize(request);
				HttpContent body = new StringContent(json, Encoding.UTF8, "application/json");
				HttpResponseMessage response = await mvarClient.PutAsync(
					"sapphireauthentication/sessionping",
					body);
				if (!response.IsSuccessStatusCode)
					return false;

				SessionPingResponse? ping = await response.Content.ReadFromJsonAsync<SessionPingResponse>();
				if (null == ping || !ping.IsValid)
					return false;

				await mvarIntStorage.UpdateSessionExpiry(ping.ExpiryUtc);
				return true;
			}
			catch
			{
				return false;
			}
		}


		public class requestParam
		{
			public requestParam(string key, string value)
			{
				this.key = key;
				this.value = value;
			}
			public string key { get; private set; }
			public string value { get; private set; }
		}
	}
}
