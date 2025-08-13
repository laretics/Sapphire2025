using Microsoft.JSInterop;
using Sapphire2025Models.Aeneas;
using Sapphire2025Models.Authentication;
using System.Text.Json;
namespace Sapphire2025.Storage
{
	/// <summary>
	/// Clase de almacenaje en el propio cliente.
	/// Serán datos de sesión que usaré para memorizar la autenticación de usuarios
	/// y los parámetros de consulta.
	/// </summary>
	public class IntStorageService
	{
        private readonly IJSRuntime mvarJsRuntime;
        private readonly InteractiveService mvarInteractiveService;
		
		internal const string LOCAL_STORAGE_ID = "localStorage";
		internal const string SESSION_STORAGE_ID = "sessionStorage";

        internal const string SESSION_INFO = "sessioninfo";

		public IntStorageService(IJSRuntime jsRuntime, InteractiveService interactive)
        {
            mvarJsRuntime = jsRuntime;
            mvarInteractiveService = interactive;
        }

        internal string internalRequestString(bool session, string command)
        {
            return string.Format("{0}.{1}",session? SESSION_STORAGE_ID: LOCAL_STORAGE_ID, command);
        }
		#region "Autenticación"
		/// <summary>
		/// Esta fución es una ayuda. Se podría obtener el usuario actual a partir de
        /// GetSessionInfo.
		/// </summary>
		/// <returns>Obtiene el usuario actual (o null, si no hay una sesión activa</returns>
		public async Task<UserModelBase?> GetCurrentUser()
        {
            SessionModel? auxSesion = await GetSessionInfo();
			if (null != auxSesion)
			{
				if (null != auxSesion.User)
					return auxSesion.User;
			}
            return null;
		}
        public async Task<SessionModel?> GetSessionInfo()
        {
            string? auxCadena = await GetStringValue(SESSION_INFO, false);
			if (null!=auxCadena)
			{
                return JsonSerializer.Deserialize<SessionModel?>(auxCadena);				
			}
            return null;
		}
        public async Task<Guid> getToken()
        {
            SessionModel? sesion = await GetSessionInfo();
            if (null != sesion)
                return sesion.Token;

            return Guid.Empty; //En caso de no encontrar sesión devolvemos un token vacío.
        }

		public async Task<bool> SetSessionInfo(SessionModel? session)
        {
            if (null == session)
            {
                await ResetValue(SESSION_INFO, false);
            }
            else
            {
                string cadena = JsonSerializer.Serialize(session);
                await SetStringValue(SESSION_INFO, cadena, false);
            }
			mvarInteractiveService.NotifyStateChanged();
			return true;
        }

		#endregion "Autenticación"

		#region "Caché de trenes"
		public async Task<IEnumerable<TrainModel>>GetTrainList()
        {
            string? auxCadena = await GetStringValue("cachetrainlist",false);
            if(null!=auxCadena)
            {
                IEnumerable<TrainModel>? auxLista = JsonSerializer.Deserialize<IEnumerable<TrainModel>>(auxCadena);
				if (null != auxLista)
					return auxLista;
			}
			return new List<TrainModel>();
		}
        public async Task<bool>SetTrainList(IEnumerable<TrainModel>? rhs)
        {
            if(null!=rhs)
            {
                string cadena = JsonSerializer.Serialize(rhs);
                await SetStringValue("cachetrainlist",cadena, false);
                return true;
            }
            return false;
        }
		public async Task<bool> SetTrainUsersDictionary(Dictionary<Guid, UserModel>? rhs)
		{
			if (null != rhs)
			{
				string cadena = JsonSerializer.Serialize(rhs);
				await SetStringValue("cacheuserslist", cadena, false);
				return true;
			}
			return false;
		}
		#endregion "Caché de trenes"
		#region "Caché de usuarios"

		/// <summary>
		/// Asigna la última hora en que actualizó la caché de la tabla de usuarios
		/// </summary>
		/// <param name="time"></param>
		/// <returns></returns>
		public async Task SetUsersCacheTime(DateTime time)
		{
			string cadena = JsonSerializer.Serialize(time);
			await SetStringValue("userscachetime", cadena, false);
		}
        public async Task<DateTime> GetUsersCacheTime()
        {
            string? auxCadena = await GetStringValue("userscachetime", false);
            if (null != auxCadena)
            {
                DateTime? salida = JsonSerializer.Deserialize<DateTime?>(auxCadena);
                if(null != salida) 
					return salida.Value;
            }
            return DateTime.MinValue;
        }

		public async Task<Dictionary<Guid, UserModelBase>?> GetUsersCache()
        {
            string? auxCadena = await GetStringValue("userscachetable", false);
            if (null != auxCadena)
            {
                return JsonSerializer.Deserialize<Dictionary<Guid, UserModelBase>>(auxCadena);
            }
            return null;
        }
        public async Task SetUsersCache(Dictionary<Guid, UserModelBase>? rhs)
		{
			if (null != rhs)
			{
				string cadena = JsonSerializer.Serialize(rhs);
				await SetStringValue("userscachetable", cadena, false);
			}
		}

		#endregion "Caché de usuarios"






		#region "Valores"
        /// <summary>
        /// Elimina un valor del almacenamiento interno
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public async Task ResetValue(string key, bool session)
        {
            string auxStorageId = internalRequestString(session, "removeItem");
            await mvarJsRuntime.InvokeVoidAsync(auxStorageId, key);
        }
		public async Task SetStringValue(string key, string value, bool session)
        {            
            string auxStorageId = internalRequestString(session, "setItem");
			await mvarJsRuntime.InvokeVoidAsync(auxStorageId, key, value);
        }
        public async Task<string?> GetStringValue(string key, bool session)
        {
			string auxStorageId = internalRequestString(session, "getItem");
			return await mvarJsRuntime.InvokeAsync<string>(auxStorageId, key);
        }
        public async Task SetIntValue(string key, int value, bool session)
        {
            await SetStringValue(key, string.Format("{0}", value),session);
        }
        public async Task<int> GetIntValue(string key, bool session)
        {
            string? entrada = await GetStringValue(key,session);
            int salida = int.MinValue;
            int.TryParse(entrada, out salida);
            return salida;
        }
        public async Task SetGuidValue(string key, Guid value, bool session)
        {
            await SetStringValue(key,string.Format("{0}", value),session);
        }
        public async Task<Guid> GetGuidValue(string key, bool session)
        {
            string? entrada = await GetStringValue(key,session);
            Guid salida = Guid.Empty;
            Guid.TryParse(entrada, out salida);
            return salida;
        }
		#endregion "Valores"

		
	}
}

