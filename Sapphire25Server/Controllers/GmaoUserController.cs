using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sapphire25Server.Storage;
using Zafiro25.Models.Model;

namespace Sapphire25Server.Controllers
{
	[ApiController]
	[Route("[controller]")]
	public class GmaoUserController : ControllerBase
	{
		private IDbContextFactory<DataStorage> mvarFactory;
		public GmaoUserController(IDbContextFactory<DataStorage> factory)
		{
			mvarFactory = factory;
		}
		[HttpGet(Name = "GetUsers")]
		public async Task<IEnumerable<UserModel>> getCurrentUsers()
		{
			List<UserModel> salida = new List<UserModel>();
			DataStorage auxDb = mvarFactory.CreateDbContext();

			List<SFMUser> auxUsers = await auxDb.Users.ToListAsync();
			foreach (SFMUser auxUser in auxUsers)
			{
				UserModel auxUserModel = await (deserializeUser(auxUser.Id));
				salida.Add(auxUserModel);
			}
			return salida;
		}
		private async Task<UserModel> deserializeUser(string userId)
		{
			DataStorage auxUserDb = mvarFactory.CreateDbContext();
			SFMUser? auxUser = await auxUserDb.Users.Where
				(xx => xx.Id.Equals(userId)).
				FirstOrDefaultAsync();
			UserModel salida = new UserModel();
			salida.id = userId;
			if (null != auxUser)
			{
				salida.CF = auxUser.CF;
				salida.name = auxUser.UserName;
				salida.roles = await auxUserDb.RolesByUser(userId);
			}
			return salida;
		}
	}
}
