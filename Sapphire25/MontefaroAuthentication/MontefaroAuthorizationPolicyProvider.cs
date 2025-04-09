using Microsoft.AspNetCore.Authorization;

namespace Sapphire25.MontefaroAuthentication
{
	public class MontefaroAuthorizationPolicyProvider:IAuthorizationPolicyProvider
	{
		public Task<AuthorizationPolicy> GetDefaultPolicyAsync()
		{
			return Task.FromResult(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());
		}

		public Task<AuthorizationPolicy> GetFallbackPolicyAsync()
		{
			return Task.FromResult<AuthorizationPolicy>(null);
		}

		public Task<AuthorizationPolicy> GetPolicyAsync(string policyName)
		{
			return Task.FromResult(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());
		}
	}
}
