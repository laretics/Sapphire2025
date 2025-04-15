using Microsoft.JSInterop;
using Sapphire2025Models.Aeneas;
using Sapphire2025Models.Authentication;
using System.Text.Json;
using System.Text.RegularExpressions;
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
		public event Action OnChange;
		internal const string LOCAL_STORAGE_ID = "localStorage";
		internal const string SESSION_STORAGE_ID = "sessionStorage";

		public IntStorageService(IJSRuntime jsRuntime)
        {
            mvarJsRuntime = jsRuntime;
        }

        public async Task<string?> getToken()
        {
            return await GetStringValue("sessionToken",false);
        }

        internal string internalRequestString(bool session, string command)
        {
            return string.Format("{0}.{1}",session? SESSION_STORAGE_ID: LOCAL_STORAGE_ID, command);
        }
		#region "Autenticación"
        public async Task<SessionModel?> GetSessionInfo()
        {
            string? auxCadena = await GetStringValue("sessioninfo", false);
			if (null!=auxCadena)
			{
                return JsonSerializer.Deserialize<SessionModel?>(auxCadena);				
			}
            return null;
		}
        public async Task<bool> SetSessionInfo(SessionModel? session)
        {
            if (null == session)
            {
                await ResetValue("sessioninfo", false);
                return true;
            }
            else
            {
                string cadena = JsonSerializer.Serialize(session);
                await SetStringValue("sessioninfo", cadena, false);
                return true;
            }
        }

		#endregion "Autenticación"

		#region "Caché de trenes"
		public async Task<IEnumerable<TrainModel>?>GetTrainList()
        {
            string? auxCadena = await GetStringValue("cachetrainlist",false);
            if(null!=auxCadena)
            {
                return JsonSerializer.Deserialize<IEnumerable<TrainModel>>(auxCadena);
            }
            return null; //No tenemos ninguna caché almacenada en la memoria
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
        public async Task<Dictionary<string,UserModel>?>GetTrainUsersDictionary()
        {
            string? auxCadena = await GetStringValue("cacheuserslist", false);
            if(null!=auxCadena)
            {
                return JsonSerializer.Deserialize<Dictionary<string,UserModel>>(auxCadena);
            }
            return null;
        }
        public async Task<bool>SetTrainUsersDictionary(Dictionary<string,UserModel>? rhs)
        {
            if(null!=rhs)
            {
                string cadena = JsonSerializer.Serialize(rhs);
                await SetStringValue("cacheuserslist", cadena, false);
                return true;
            }
            return false;
        }

		#endregion "Caché de trenes"

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
            NotifyStateChanged();
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

		private void NotifyStateChanged() => OnChange?.Invoke();
	}
}

