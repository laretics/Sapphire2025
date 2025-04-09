using Microsoft.AspNetCore.Identity;
using Zafiro25.Data;

namespace Zafiro25.Components.Account
{
    internal sealed class IdentityUserAccessor(UserManager<SFMUser> userManager, IdentityRedirectManager redirectManager)
    {
        public async Task<SFMUser> GetRequiredUserAsync(HttpContext context)
        {
            var user = await userManager.GetUserAsync(context.User);

            if (user is null)
            {
                redirectManager.RedirectToWithStatus("Account/InvalidUser", $"Error: Unable to load user with ID '{userManager.GetUserId(context.User)}'.", context);
            }

            return user;
        }
    }
}
