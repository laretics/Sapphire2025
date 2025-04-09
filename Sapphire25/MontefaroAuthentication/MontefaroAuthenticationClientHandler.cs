using System.Net.Http.Headers;
using System.Text;

namespace Sapphire25.MontefaroAuthentication
{
	public class MontefaroAuthenticationClientHandler:HttpClientHandler
	{
		private readonly string _username;
		private readonly string _password;

		public MontefaroAuthenticationClientHandler(string username, string password)
		{
			_username = username;
			_password = password;
		}

		protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			var byteArray = Encoding.ASCII.GetBytes($"{_username}:{_password}");
			request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(byteArray));
			return await base.SendAsync(request, cancellationToken);
		}

	}
}
