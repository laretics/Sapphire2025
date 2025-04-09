using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace Sapphire25.MontefaroAuthentication
{
	public class MontefaroAuthorizationService : IAuthorizationService
	{
		public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object resource, IEnumerable<IAuthorizationRequirement> requirements)
		{
			var result = requirements.All(requirement => requirement is DenyAnonymousAuthorizationRequirement && user.Identity.IsAuthenticated)
				? AuthorizationResult.Success()
				: AuthorizationResult.Failed();

			return Task.FromResult(result);
		}

		public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object resource, string policyName)
		{
			var result = user.Identity.IsAuthenticated
				? AuthorizationResult.Success()
				: AuthorizationResult.Failed();

			return Task.FromResult(result);
		}
	}
}
