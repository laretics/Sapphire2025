using ZafiroGmao.Data;
using ZafiroGmao.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;

namespace ZafiroGmao.Telegram.Transactions
{
	public class Register:Transaction
	{
		string mvarUserId=string.Empty; //Identificador del usuario
										//private UserManager<SFMUser> mvarUserManager; //Para la validación de contraseñas
		IServiceScopeFactory mvarScopeFactory; //Lo necesito para crear el administrador de passwords
		public Register(long chatId, IServiceScopeFactory scopeFactory):base(chatId) { mvarScopeFactory = scopeFactory; }
		public override async Task<string> initialMessage()
		{
			using (ApplicationDbContext auxDb = new ApplicationDbContext())
			{
				SFMUser? auxUsuario = await auxDb.Users.Where(f => f.TelegramId== chatId).FirstOrDefaultAsync();
				if (null == auxUsuario)
				{
					return "Por favor, introduzca su código de usuario o su número de carnet ferroviario (CF)";					
				}
				else
				{
					mvarIsEnded = true; //Termino el diálogo
					return string.Format("Su identificador de usuario es {0} (Carnet ferroviario {1}). Eso significa que ya está registrado en este bot.", auxUsuario.Id, auxUsuario.CF);					
				}
			}
		}
		public override async Task<string> processMessage(string rhs)
		{
			string origin = await base.processMessage(rhs);
			using (ApplicationDbContext auxDb = new ApplicationDbContext())
			{
				if (string.Empty==mvarUserId)
				{
					//Estoy esperando un CF o un código válido de usuario
					SFMUser? auxUser = await auxDb.Users.Where(f => f.NormalizedUserName==origin).FirstOrDefaultAsync();
					if (null == auxUser)
					{
						auxUser = await auxDb.Users.Where(f => f.CF == origin).FirstOrDefaultAsync();

					}
					if (null == auxUser)
					{
						mvarIsEnded = true;
						return "Lo siento. No se encuentra ningún usuario con estos datos para registrar.";
					}
					else
					{
						mvarUserId = auxUser.Id;
						return "Introduzca ahora su contraseña de usuario en el sistema.";
					}
				}
				else
				{
					//Estoy esperando el password del usuario

					//Necesitaré la referencia al administrador de usuarios para comprobar los passwords.
					UserManager<SFMUser> auxUserManager;
					using (var auxScope = mvarScopeFactory.CreateScope())
					{
						auxUserManager = auxScope.ServiceProvider.GetRequiredService<UserManager<SFMUser>>();
						SFMUser? auxUser = await auxDb.Users.Where(f => f.Id == mvarUserId).FirstOrDefaultAsync();
						System.Diagnostics.Debug.Assert(null != auxUser);
						if (await auxUserManager.CheckPasswordAsync(auxUser, rhs))
						{
							auxUser.TelegramId = chatId;
							await auxDb.SaveChangesAsync();
							mvarIsEnded = true;
							return string.Format("El usuario {0} con CF {1} está ahora dado de alta en el sistema.", auxUser.UserName, auxUser.CF);
						}
						else
						{
							mvarIsEnded = true;
							return "El password o el código de usuario son incorrectos. Pruebe de nuevo.";
						}
					}
				}
			}
		}

		public override string ToString() //Descripción de este diálogo
		{
			return "Registro de nuevo usuario.";
		}
	}
}
