using Microsoft.JSInterop;
using Sapphire2025Models.Aeneas;
using Sapphire2025Models.Authentication;
using Sapphire2025Models.Expert;
using Sapphire2025Models.Expert.WorkshiftTemplates;
using System.Security.AccessControl;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks.Dataflow;
namespace Sapphire2025.Storage
{
	/// <summary>
	/// Clase de almacenaje en el propio cliente.
	/// Serán datos de sesión que usaré para memorizar la autenticación de usuarios
	/// y los parámetros de consulta.
	/// </summary>
	public class IntStorageService
	{
        private readonly IJSRuntime? mvarJsRuntime;
        private Dictionary<string, string> mcolSessionValues; //Este diccionario se usa desde el módulo Telegram
		
		internal const string LOCAL_STORAGE_ID = "localStorage";
		internal const string SESSION_STORAGE_ID = "sessionStorage";

        internal const string SESSION_INFO = "sessioninfo";

		public IntStorageService(IJSRuntime jsRuntime)
        {
            mvarJsRuntime = jsRuntime;
            mcolSessionValues = new Dictionary<string, string>();
        }
        public IntStorageService()
        {
            mvarJsRuntime = null;
			mcolSessionValues = new Dictionary<string, string>();
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
			//mvarInteractiveService.NotifyStateChanged();
			return true;
        }

		#endregion "Autenticación"

		#region "Caché de andenes"
        public async Task<PlatformModel[]>GetPlatformList()
        {
            string? auxCadena = await GetStringValue("cacheplatformlist", false);
            if(null!=auxCadena)
            {
                PlatformModel[]? auxLista = JsonSerializer.Deserialize<PlatformModel[]>(auxCadena);
                if (null != auxLista)
                    return auxLista;
            }
            return new PlatformModel[0]; //Array vacío si no tenemos nada.
        }
        public async Task<bool> SetPlatformList(PlatformModel[]? rhs)
        {
            if(null!=rhs)
            {
                string cadena = JsonSerializer.Serialize(rhs);
                await SetStringValue("cacheplatformlist", cadena, false);
                return true;
            }
            return false; 
        }
        public async Task<bool> ResetPlatformList()
        {
            await ResetValue("cacheplatformlist",false);
            return true;
        }

		#endregion "Caché de andenes"

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
        public async Task<bool> ResetTrainList()
        {
            await ResetValue("cachetrainlist", false);
            return true;
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

		#region "Página de Inspectores"
        public async Task DeleteInspectorReportValues()
            //Elimina todo lo que había en la caché sobre la tabla de Agentes.
        {
            await SetStringValue("inspectorreportdate", null, false);
            await SetStringValue("inspectorchiefstable", null, false);
			await SetStringValue("inspectorcommentsfield", null, false);
		}
        public async Task SetInspectorReportDate(DateTime rhs)
        {
            string cadena = JsonSerializer.Serialize(rhs);
            await SetStringValue("inspectorreportdate", cadena, false);
        }
        public async Task<DateTime> GetInspectorReportDate()
        {
            string? cadena = await GetStringValue("inspectorreportdate", false);
            DateTime salida = DateTime.Today;
            if(null != cadena)
				salida = JsonSerializer.Deserialize<DateTime>(cadena);

            if (DateTime.MinValue == salida)
                salida = DateTime.Today;

            return salida;
        }
        public async Task SetInspectorAgentsTable(List<Sapphire2025Models.Inspector.AgentsListRecordModel> agents)
        {
            string cadena = JsonSerializer.Serialize(agents);
            await SetStringValue("inspectoragentstable", cadena, false);
        }
        public async Task<List<Sapphire2025Models.Inspector.AgentsListRecordModel>> GetInspectorAgentsTable()
        {
            List<Sapphire2025Models.Inspector.AgentsListRecordModel>? salida = new List<Sapphire2025Models.Inspector.AgentsListRecordModel>();
            string? cadena = await GetStringValue("inspectoragentstable", false);
            if(null!=cadena)
            {
                salida = JsonSerializer.Deserialize<List<Sapphire2025Models.Inspector.AgentsListRecordModel>>(cadena);
                if (null == salida) return new List<Sapphire2025Models.Inspector.AgentsListRecordModel>();
            }
            return salida;
        }

        public async Task SetInspectorCommentsField(string? rhs)
        {
            string cadena = JsonSerializer.Serialize(rhs);
            await SetStringValue("inspectorcommentsfield", cadena, false);
        }
        public async Task<string?> GetInspectoCommentsField()
        {
            string? cadena = await GetStringValue("inspectorcommentsfield", false);
            if (null != cadena)
                return JsonSerializer.Deserialize<string?>(cadena);

            return null;
        }

        public async Task SetInspectorReportAssignations(List<AssignationContentModel>? rhs)
        {
            string cadena = JsonSerializer.Serialize(rhs);
            await SetStringValue("inspectorassignations", cadena, false);
        }
        public async Task<List<AssignationContentModel>?> GetInspectorReportAssignations()
        {
            string? cadena = await GetStringValue("inspectorassignations", false);
            if(null!=cadena)
            {
                List<AssignationContentModel>? salida = JsonSerializer.Deserialize<List<AssignationContentModel>>(cadena);
                return salida;
            }
            return null;
        }

        public async Task SetInspectorReportSortedTemplates(List<WorkShiftTemplateModel>? rhs)
        {
            string cadena = JsonSerializer.Serialize(rhs);
            await SetStringValue("inspectorreportsortedtemplates", cadena,false);
        }
        public async Task<List<WorkShiftTemplateModel>?> GetInspectorSortedTemplates()
        {
            string? cadena = await GetStringValue("inspectorreportsortedtemplates", false);
            if(null!=cadena)
            {
                List<WorkShiftTemplateModel>? salida = JsonSerializer.Deserialize<List<WorkShiftTemplateModel>>(cadena);
                return salida;
            }
            return null;
        }




		#endregion

		#region "Valores"
		/// <summary>
		/// Elimina un valor del almacenamiento interno
		/// </summary>
		/// <param name="key"></param>
		/// <returns></returns>
		public async Task ResetValue(string key, bool session)
        {
            string auxStorageId = internalRequestString(session, "removeItem");
            if (null == mvarJsRuntime)
                mcolSessionValues.Remove(key);
            else
                await mvarJsRuntime.InvokeVoidAsync(auxStorageId, key);
        }
		public async Task SetStringValue(string key, string? value, bool session)
        {            
            string auxStorageId = internalRequestString(session, "setItem");
            if(null==mvarJsRuntime)
            {
                if (!mcolSessionValues.ContainsKey(key))
                    mcolSessionValues.Add(key, "");
                System.Diagnostics.Debug.Assert(mcolSessionValues.ContainsKey(key));
                if (null == value)
                    mcolSessionValues.Remove(key);
                else
                    mcolSessionValues[key]= value;
            }
            else
    			await mvarJsRuntime.InvokeVoidAsync(auxStorageId, key, value);
        }
        public async Task<string?> GetStringValue(string key, bool session)
        {
			string auxStorageId = internalRequestString(session, "getItem");
            if(null==mvarJsRuntime)
            {
                if(mcolSessionValues.ContainsKey(key))
                    return mcolSessionValues[key];
                return null;
            }
            else
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
		public async Task SetSwitchesArray(string key, bool[] rhs, bool session)
        {
            StringBuilder cadena = new StringBuilder();
            for (int n = 0; n < rhs.Length; n++)
                cadena.Append(rhs[n] ? "1" : "0");
            await SetStringValue(key, cadena.ToString(), session);
        }
        public async Task<bool[]> GetSwitchesArray(string key, bool session)
        {
            string? cadena = await GetStringValue(key, session);
            if(null!=cadena)
            {
                bool[] salida = new bool[cadena.Length];
                for (int n = 0; n < cadena.Length; n++)
                    salida[n] = ('1' == cadena[n]);
                return salida;
            }
            return new bool[0];
        }
        #endregion "Valores"

		
	}
}

