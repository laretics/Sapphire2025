using Sapphire2025Models;
using Sapphire2025Models.Aeneas;
using Sapphire2025Models.Authentication;
using Sapphire2025Models.GMao;
using Sapphire2026Clients;
using System.Net.Http.Json;
using System.Reflection.Metadata.Ecma335;
using System.Text.Json;

namespace Sapphire2025.Storage
{
	public partial class AeneasClient:HttpClientBase
	{
		public AeneasClient(HttpClient httpClient, IntStorageService intStorage, SessionService session) : base(httpClient, intStorage,session, "sapphireaeneas") { }

		//IMPORTANTE: La parte de WorkCatalog / WorkOrder está en WorkOrderClient.cs
	
		public async Task<bool> openFailReport(TrainModel? train)
		{

			if(null!=train)
			{
				//La apertura de un parte de averías envía el tren al estado de
				//solicitud de diagnóstico sólo si el tren está activo o bien
				//en estado de solicitud de diagnóstico.
				Common.TrainStatus currentStatus = train.lastStatus;
				if (currentStatus == Common.TrainStatus.Available ||
					currentStatus == Common.TrainStatus.RequestToDiagnose)
				{
					return await commitTrainStatus(train.id, Common.OperationType.CorrectiveRequest);
				}
				else
					return true; //En cualquier otro estado la salida es correcta y no se hace nada.
			}
			return false;
		}
		public async Task<IEnumerable<TrainModel>> trainsList()
		{
			string request = composeCommand("trains");
			try
			{
                HttpResponseMessage respuesta = await sendGetRequest(request);
                IEnumerable<TrainModel>? auxLista = await respuesta.Content.ReadFromJsonAsync<IEnumerable<TrainModel>>();
				if (null != auxLista) return auxLista;
            }
			catch (Exception ex)
			{
				Console.WriteLine(ex.Message);
			}
            return new List<TrainModel>();
        }
		public async Task<IEnumerable<PlatformModel>> platformsList()
		{
			string request = composeCommand("platforms");
			HttpResponseMessage respuesta = await sendGetRequest(request);
			IEnumerable<PlatformModel>? auxLista = await
				respuesta.Content.ReadFromJsonAsync<IEnumerable<PlatformModel>>();
			if(null==auxLista) return new List<PlatformModel>();
			return auxLista;
		}
		public async Task<TrainModel?> train(string trainId)
		{
			string request = composeCommand(
				"traininfo",
				new requestParam("trainid", trainId));
			HttpResponseMessage respuesta = await sendGetRequest(request);
			return await respuesta.Content.ReadFromJsonAsync<TrainModel?>();
		}
		public async Task<Dictionary<Guid,UserModel>?>  usersTrainList()
		{
			string request = composeCommand("userstrains");
			HttpResponseMessage respuesta = await sendGetRequest(request);
			return await respuesta.Content.ReadFromJsonAsync<Dictionary<Guid,UserModel>>();
		}
		public async Task<IEnumerable<StatusChangeModel>> recentChangeList(DateTime timeStamp)
		{
			DateTime auxUtc = timeStamp.Kind == DateTimeKind.Utc
		? timeStamp
		: DateTime.SpecifyKind(timeStamp, DateTimeKind.Utc);
			string request = composeCommand(
				"rcchngs",
				new requestParam("timestamp", auxUtc.ToString("o")));
			HttpResponseMessage respuesta = await sendGetRequest(request);
			IEnumerable<StatusChangeModel>? auxLista = await respuesta.Content.ReadFromJsonAsync<IEnumerable<StatusChangeModel>>();
			if(null==auxLista) return new List<StatusChangeModel>() ;
			return auxLista;
		}

		public async Task<Dictionary<Guid,UserModel>?> usersChangesList(string trainId)
		{
			string request = composeCommand(
				"usersstchngs",
				new requestParam("trainid",trainId));
			HttpResponseMessage respuesta = await sendGetRequest(request);
			return await respuesta.Content.ReadFromJsonAsync<Dictionary<Guid ,UserModel>>();
		}

		public async Task<IEnumerable<StatusChangeModel>> trainChangesList(Guid trainId, DateTime oldest)
		{
			IEnumerable<StatusChangeModel>? salida = null;
			StatusChangeRequestModel request = new StatusChangeRequestModel(trainId, oldest);
			string jsonData = System.Text.Json.JsonSerializer.Serialize(request);
			HttpResponseMessage respuesta = await sendPostRequest("stchngs",jsonData);
			if (respuesta.IsSuccessStatusCode)
				salida =  await respuesta.Content.ReadFromJsonAsync<IEnumerable<StatusChangeModel>>();
			
				if (null == salida) return new List<StatusChangeModel>();
			return salida;
		}

		public async Task<bool> addNote(NoteModel note)
		{
			string jsonData = System.Text.Json.JsonSerializer.Serialize(note);
			HttpResponseMessage respuesta = await sendPostRequest("addnote", jsonData);
			if (respuesta.IsSuccessStatusCode)
				return await respuesta.Content.ReadFromJsonAsync<bool>();
			return false;
		}

		/// <summary>
		/// Etiqueta una nota (IsSymptom + SystemAffected) y marca IsValid = true.
		/// </summary>
		public async Task<bool> labelNote(Guid noteId, bool isSymptom, byte systemAffected)
		{
			Guid auxToken = await getCurrentToken();
			NoteLabelRequestModel request = new NoteLabelRequestModel(auxToken, noteId, isSymptom, systemAffected);
			string jsonData = JsonSerializer.Serialize(request);
			HttpResponseMessage respuesta = await sendPostRequest("labelnote", jsonData);
			if (respuesta.IsSuccessStatusCode)
				return await respuesta.Content.ReadFromJsonAsync<bool>();
			return false;
		}
		//Fuerza la modificación del andén actual del tren
		public async Task<bool> changePlatform(TrainModel train)
		{
			if (null == train)
				return false;
			Guid auxToken = await getCurrentToken();
			PlatformChangeRequestModel request = new PlatformChangeRequestModel(auxToken, train.id, train.PlatformId);
			string jsonData = System.Text.Json.JsonSerializer.Serialize(request);
			HttpResponseMessage respuesta = await sendPostRequest("changeplatform", jsonData);
			if (respuesta.IsSuccessStatusCode)
				return await respuesta.Content.ReadFromJsonAsync<bool>();
			return false;
		}

		/// <summary>
		/// Registra un nuevo valor de odómetro para el tren (histórico + valor actual).
		/// </summary>
		public async Task<bool> setOdometer(Guid trainId, long odometer)
		{
			Guid auxToken = await getCurrentToken();
			OdometrySetRequestModel request = new OdometrySetRequestModel(auxToken, trainId, odometer);
			string jsonData = System.Text.Json.JsonSerializer.Serialize(request);
			HttpResponseMessage respuesta = await sendPostRequest("setodometer", jsonData);
			if (respuesta.IsSuccessStatusCode)
				return await respuesta.Content.ReadFromJsonAsync<bool>();
			return false;
		}



		//Obtiene las últimas notas de un determinado tren.
		//Si el parámetro max es cero, devuelve todas las notas.
		//Si tiene otro valor, devuelve el número de notas requerido.
		public async Task<List<NoteModel>> retrieveNotes(Guid parentId,string type, int max)
		{
			Guid auxToken = await getCurrentToken();
			NoteChatRequestModel requestModel = new NoteChatRequestModel(auxToken,parentId,type,max);
			string jsonData = System.Text.Json.JsonSerializer.Serialize(requestModel);
			HttpResponseMessage response = await sendPostRequest("getnotes", jsonData);
			if(response.IsSuccessStatusCode)
			{
				List<NoteModel>? auxLista = await response.Content.ReadFromJsonAsync<List<NoteModel>>();
				if (null != auxLista)
					return auxLista;
			}
			return new List<NoteModel>(); //No se han encontrado notas.
		}

		public async Task<bool> commitTrainStatus(Guid trainId, Common.OperationType operation)
		{
			Guid auxToken = await getCurrentToken();
			TrainStatusCommitModel commit = new TrainStatusCommitModel(auxToken,trainId,operation);
			string jsonData = System.Text.Json.JsonSerializer.Serialize(commit);
			HttpResponseMessage response = await sendPostRequest("cmtstatus", jsonData);
			if(response.IsSuccessStatusCode)
				return await response.Content.ReadFromJsonAsync<bool>();
			return false;
		}

		public async Task<bool> TelegramBroadcast(string message, bool priority = false, params Common.UserRole[] roles)
		{
			TelegramBroadcastRequestModel request = new TelegramBroadcastRequestModel
			{
				Message = message,
				Priority = priority,
				Roles = roles
			};

			string jsonString = JsonSerializer.Serialize(request);

			HttpResponseMessage respuesta = await sendPostRequest("telegrambroadcast", jsonString);
			if (!respuesta.IsSuccessStatusCode) return false;

			string contenido = await respuesta.Content.ReadAsStringAsync();
			if (string.IsNullOrWhiteSpace(contenido)) return false;

			JsonSerializerOptions opciones = new(JsonSerializerDefaults.Web);
			return JsonSerializer.Deserialize<bool>(contenido, opciones);
		}

		/// <summary>
		/// Búsqueda de notas con filtros (fecha, usuario, tren, tipo, etiquetas, keywords).
		/// </summary>
		public async Task<IEnumerable<NoteModel>> searchNotes(NoteSearchRequestModel request)
		{
			if (null == request)
				return Array.Empty<NoteModel>();
			if (Guid.Empty.Equals(request.SessionToken))
				request.SessionToken = await getCurrentToken();

			string jsonData = JsonSerializer.Serialize(request);
			HttpResponseMessage respuesta = await sendPostRequest("searchnotes", jsonData);
			if (respuesta.IsSuccessStatusCode)
			{
				IEnumerable<NoteModel>? lista = await respuesta.Content.ReadFromJsonAsync<IEnumerable<NoteModel>>();
				if (null != lista)
					return lista;
			}
			return Array.Empty<NoteModel>();
		}

		/// <summary>
		/// Consulta compleja unificada de notas e incidencias (cambios de estado).
		/// El servidor registra el uso en el log de actividad.
		/// </summary>
		public async Task<IncidenceQueryResponse?> incidenceQuery(IncidenceQueryRequest request)
		{
			if (null == request)
				return null;
			if (Guid.Empty.Equals(request.SessionToken))
				request.SessionToken = await getCurrentToken();

			string jsonData = JsonSerializer.Serialize(request);
			HttpResponseMessage respuesta = await sendPostRequest("incidencequery", jsonData);
			if (respuesta.IsSuccessStatusCode)
				return await respuesta.Content.ReadFromJsonAsync<IncidenceQueryResponse>();
			return null;
		}
	}
}
