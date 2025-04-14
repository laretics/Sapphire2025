using Sapphire2025Models.Aeneas;
using Sapphire2025Models.Authentication;
using System.Net.Http.Json;

namespace Sapphire2025.Storage
{
	public class AeneasClient:HttpClientBase
	{
		public AeneasClient(HttpClient httpClient) : base(httpClient, "sapphireaeneas") { }

		public async Task<IEnumerable<TrainModel>?> trainsList()
		{
			string request = composeCommand("trains");
			HttpResponseMessage respuesta = await sendGetRequest(request);
			return await respuesta.Content.ReadFromJsonAsync<IEnumerable<TrainModel>>();
		}
		public async Task<Dictionary<string,UserModel>?>  usersTrainList()
		{
			string request = composeCommand("userstrains");
			HttpResponseMessage respuesta = await sendGetRequest(request);
			return await respuesta.Content.ReadFromJsonAsync<Dictionary<string,UserModel>>();
		}
	}
}
