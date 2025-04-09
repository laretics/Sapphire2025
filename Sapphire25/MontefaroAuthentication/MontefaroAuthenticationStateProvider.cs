using Microsoft.AspNetCore.Components.Authorization;
using System.Security.Claims;

namespace Sapphire25.MontefaroAuthentication
{
	public class MontefaroAuthenticationStateProvider:AuthenticationStateProvider
	{
		private ClaimsPrincipal mvarAnonymous = new ClaimsPrincipal(new ClaimsIdentity());
		private ClaimsPrincipal mvarUser;

		public override Task<AuthenticationState> GetAuthenticationStateAsync()
		{
			ClaimsPrincipal user = mvarUser ?? mvarAnonymous;
			return Task.FromResult(new AuthenticationState(user));
		}

		public void MarkUserAsAuthenticated(string username)
		{
			var identity = new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, username) }, "MontefaroAuthenticator");
			mvarUser = new ClaimsPrincipal(identity);
			NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(mvarUser)));
		}

		public void MarkUserAsLoggedOut()
		{
			mvarUser = mvarAnonymous;
			NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(mvarAnonymous)));
		}
	}
}
