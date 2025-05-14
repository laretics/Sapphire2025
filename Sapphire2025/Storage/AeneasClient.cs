using Sapphire2025Models;
using Sapphire2025Models.Aeneas;
using Sapphire2025Models.Authentication;
using System.Net.Http.Json;

namespace Sapphire2025.Storage
{
	public class AeneasClient:HttpClientBase
	{
		public AeneasClient(HttpClient httpClient, IntStorageService intStorage) : base(httpClient, intStorage, "sapphireaeneas") { }


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
			HttpResponseMessage respuesta = await sendGetRequest(request);
			IEnumerable<TrainModel>? auxLista = await respuesta.Content.ReadFromJsonAsync<IEnumerable<TrainModel>>();
			if(null == auxLista) return new List<TrainModel>();
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
		public async Task<IEnumerable<StatusChangeModel>> trainChangesList(string trainId)
		{
			string request = composeCommand(
				"stchngs",
				new requestParam("trainid", trainId));
			HttpResponseMessage respuesta = await sendGetRequest(request);
			IEnumerable<StatusChangeModel>? auxLista = await respuesta.Content.ReadFromJsonAsync<IEnumerable<StatusChangeModel>>();
			if(null==auxLista) return new List<StatusChangeModel>();
			return auxLista;
		}

		public async Task<IEnumerable<StatusChangeModel>> recentChangeList(DateTime timeStamp)
		{
			string request = composeCommand(
				"rcchngs",
				new requestParam("timestamp", timeStamp.ToString()));
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

		public async Task<bool> addNote(NoteModel note)
		{
			string jsonData = System.Text.Json.JsonSerializer.Serialize(note);
			HttpResponseMessage respuesta = await sendPostRequest("addnote", jsonData);
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
	}
}
