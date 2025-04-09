namespace Sapphire25.MontefaroAuthentication
{
	public class MontefaroAuthService
	{
		private MontefaroAuthenticationClientHandler? mvarClientHandler;
		private readonly Uri mvarBaseAddress;

		public MontefaroAuthService(Uri BaseAddress)
		{
			mvarBaseAddress = BaseAddress;
		}

		public void SetCredentials(MontefaroAuthenticationClientHandler clientHandler)
		{
			mvarClientHandler = clientHandler;
		}

		public HttpClient GetHttpClient()
		{
			if (null==mvarClientHandler)
			{
				MontefaroAuthenticationClientHandler auxCliente = new MontefaroAuthenticationClientHandler("anonymous", string.Empty);
				return new HttpClient(auxCliente) { BaseAddress = mvarBaseAddress };
			}
			else
			{
				return new HttpClient(mvarClientHandler) { BaseAddress = mvarBaseAddress };
			}			
		}
	}
}
