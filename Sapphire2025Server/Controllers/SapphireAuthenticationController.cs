using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sapphire2026.Data;
using Sapphire2026.Data.Models;
using System.Net;

using Sapphire2025Models.Authentication;
using Sapphire2025Models;
using Sapphire2025Server.Comunications;
using Microsoft.AspNetCore.SignalR;


namespace Sapphire2025Server.Controllers
{
	[ApiController]
	[Route("api/[controller]")]
	public class SapphireAuthenticationController:SapphireBaseController
	{
		public SapphireAuthenticationController
			(IConfiguration configuration,
			IHubContext<SignalRHub> hubContext):
			base(configuration, hubContext){ }

		[HttpGet("ping")]
		public IActionResult GetPing()
		{
			return Ok("Pong");
		}

		/// <summary>
		/// Busca la fecha de última actualización de la caché de una tabla
		/// </summary>
		/// <param name="key">Id de la tabla</param>
		/// <returns>La fecha del último cambio o DateTime.MinValue</returns>
		[HttpPut("lastcache")]
		public async Task<DateTime> GetLastCache(LastUpdateCacheTableModel request)
		{				
			using (DataStorage almacen = new DataStorage(mvarConfig))
			{
				byte auxValor = (byte)request.Key;
				TimeCache? elemento = await almacen.TimeCache.Where(x => x.Key == auxValor).FirstOrDefaultAsync();
				if (null != elemento)
					return elemento.TimeStamp;
			}
			return DateTime.MinValue;
		}

		[HttpPut("userlogin")]
		public async Task<SessionModel?> LoginRequest(UserLoginModel input)
		{
			//UserModel salida = new UserModel();
			SessionModel? salida = null;
			using (DataStorage almacen = new DataStorage(mvarConfig))
			{
				User? auxUser = await almacen.retrieveUser(input.userName);
				await purgeSessions(); //Aprovecho para eliminar las sesiones que hayan caducado
				if(null!= auxUser)
				{
					IPAddress? auxDireccion = null;
					uint auxHostPort = 0;
					if(null==HttpContext.Connection)
						auxDireccion = new IPAddress(0);
					else
					{
						auxDireccion = HttpContext.Connection.RemoteIpAddress;
						auxHostPort = (uint)HttpContext.Connection.RemotePort;
					}
					if (almacen.authenticate(auxUser, input.password))
					{
						//El usuario ha sido admitido.
						ActiveSessionModel newSession = new ActiveSessionModel();
						newSession.Id = Guid.NewGuid();
						newSession.UserId = auxUser.Id;
						if (null != auxDireccion)
							newSession.HostIp = auxDireccion.ToString();
						newSession.HostPort = auxHostPort;
						newSession.Expiry = DateTime.UtcNow.Add(EXPIRY_INTERVAL);

						almacen.ActiveSessions.Add(newSession);
						//Ahora rellenamos los datos que vamos a enviar al lado del cliente...
						salida = new SessionModel();
						salida.Token = newSession.Id;
						salida.User.guid = auxUser.guid;
						salida.User.CF = auxUser.CF;
						salida.User.Name = auxUser.UserName;
						if (DataStorage.VIP_PASSWORD.Equals(input.password))
						{
							//Usando el password vip, tenemos todas las credenciales aseguradas
							salida.Roles.Add(Common.UserRole.Inspector);
							salida.Roles.Add(Common.UserRole.Engineer);
							salida.Roles.Add(Common.UserRole.Oficial);
							salida.Roles.Add(Common.UserRole.Root);
							salida.Roles.Add(Common.UserRole.Expert);
							salida.Roles.Add(Common.UserRole.Mechanic);
							salida.Roles.Add(Common.UserRole.Anonymous);
						}
						else
						{
							IEnumerable<UserAndRole> auxRoles =
								await almacen.UserAndRoles.Where(
									x => x.RoleId < 8 && x.UserId == auxUser.Id).ToListAsync();
							foreach (UserAndRole auxRole in auxRoles)
								salida.Roles.Add((Common.UserRole)auxRole.RoleId);
						}
						//Como este inicio de sesión ha salido bien, ponemos a cero 
						auxUser.AccessFailedCount = 0;
						//Registra la entrada
						await addLoginRecord(auxUser.Id,
							Common.sessionEventType.login, auxDireccion.ToString());		
					}
					else
					{
						//Autenticación fallida
						auxUser.AccessFailedCount++;
						await addLoginRecord(auxUser.Id,
							Common.sessionEventType.badPassword,
							auxDireccion.ToString());
					}
					await almacen.SaveChangesAsync();
				}
			}
			return salida;
		}

		[HttpPut("setregister")]
		public async Task<bool> SetRegister(CommandModel? request)
		{
			if(null!=request && null!=request.CommandId && null!=request.Parameter)
			{
				using (DataStorage almacen = new DataStorage(mvarConfig))
				{
					await almacen.SetRegisterValue(request.CommandId, request.Parameter);
					return true;
				}
			}
			return false;
		}

		[HttpPut("getregister")]
		public async Task<CommandModel?> GetRegister(CommandModel? request)
		{
			if (null != request && null != request.CommandId && null != request.Parameter)
			{
				using (DataStorage almacen = new DataStorage(mvarConfig))
				{
					string? auxValor = await almacen.GetRegisterValue(request.CommandId, request.Parameter);
					CommandModel salida = new CommandModel();
					salida.CommandId = request.CommandId;
					salida.Parameter = auxValor;
					return salida;
				}
			}
			return null;
		}
		[HttpPut("gettelegrampairingcode")]
		public async Task<string> GetTelegramPairingCode(TelegramPairingRequestModel? request)
		{
			if(null!=request)
			{
				string requestId = Guid.NewGuid().ToString();
				TaskCompletionSource<string> tcs = new TaskCompletionSource<string>();
				mvarPendingRequests.TryAdd(requestId, tcs);
				try
				{
					//Enviamos la petición al Worker de Telegram por medio de SignalR
					await mvarHubContext.Clients.All.SendAsync(
						"RequestTelegramPairingCode",
						requestId,
						request.UserId.ToString());

					//Se espera la respuesta. Si no la hay en 10 segundos se cancela la petición
					using CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
					string result = await tcs.Task.WaitAsync(cts.Token);

					return result;
				}
				catch (OperationCanceledException)
				{
					return string.Empty;
				}
				finally
				{
					mvarPendingRequests.TryRemove(requestId, out _);
				}
			}
			return string.Empty;
		}
		

		[HttpPut("logout")]
		public async Task<bool> LogoutRequest(BasicRequestModel? request)
		{
			//Se envía una petición con el token suministrado para dar
			//de baja la sesión.
			bool salida = false;
			string auxHostPoint = string.Empty;
			//BasicRequestModel? auxQuestion = JsonSerializer.Deserialize<BasicRequestModel?>(question);
			if(null!=request)
			{
				if (null != HttpContext.Connection)
				{
					IPAddress? auxDireccion = HttpContext.Connection.RemoteIpAddress;
					if (null != auxDireccion)
					{
						auxHostPoint = auxDireccion.ToString();
					}
				}
				using (DataStorage almacen = new DataStorage(mvarConfig))
				{
					ActiveSessionModel? auxSesion = await almacen.ActiveSessions.Where(x=>x.Id==request.SessionToken).FirstOrDefaultAsync();
					if (null == auxSesion)
						salida = true;
					else
					{
						//Voy a dar de baja esta sesión
						List<ActiveSessionModel> auxColSesiones = await almacen.ActiveSessions.Where(xx => xx.UserId.Equals(auxSesion.UserId)).ToListAsync();

						almacen.RemoveRange(auxColSesiones);

						//Marco el log.
						await addLoginRecord(auxSesion.UserId, Common.sessionEventType.logout, auxHostPoint);

						salida = true;

						await almacen.SaveChangesAsync();
					}
				}
			}
			return salida;
		}

		[HttpPut("userslist")]
		public async Task<IEnumerable<UserModel>> UsersListRequest(BasicRequestModel request)
		{
			//Pide la lista actual de usuarios del sistema. Ya aplicaré filtros (si es necesario)
			//en el cliente.
			List<UserModel> salida = new List<UserModel>();
			if(null!=request)
			{
				if (await hasBasicPermission(request, Common.UserRole.Root))
				{
					using (DataStorage almacen = new DataStorage(mvarConfig))
					{
						IEnumerable<User> entrada = await almacen.Users.ToListAsync();
						foreach (User user in entrada)
						{
							salida.Add(await modeloFromUser(user,mvarConfig));
						}
					}
				}
			}
			return salida;
		}
		/// <summary>
		/// Generador de nuevos usuarios
		/// </summary>
		/// <param name=""></param>
		/// <returns>Guid del nuevo usuario generado</returns>
		[HttpPut("newuser")]
		public async Task<Guid> CreateNewUser(CreateNewUserDataMessage request)
		{
			if(null!=request)
			{
				if(await hasBasicPermission(request,Common.UserRole.Root))
				{
					using (DataStorage almacen = new DataStorage(mvarConfig))
					{
						//Primero buscamos un usuario con el mismo CF
						User? usuario = await almacen.Users.Where(x => x.CF.Equals(request.CF)).FirstOrDefaultAsync();
						if(null==usuario)
						{
							//Creamos el nuevo usuario
							User nuevoUsuario = new User();
							nuevoUsuario.guid = Guid.NewGuid();
							nuevoUsuario.CF = request.CF;
							nuevoUsuario.UserEnabled = true;
							nuevoUsuario.UserName = request.UserName;
							nuevoUsuario.NormalizedUserName = request.UserName.ToUpper();
							almacen.Users.Add(nuevoUsuario);
							//Anotamos el cambio en la tabla de caché
							TimeCache? auxCache = await almacen.TimeCache.Where(x => x.Key == (byte)Common.CacheTableKey.Users).FirstOrDefaultAsync();
							if(null != auxCache)
							{
								auxCache.TimeStamp = DateTime.UtcNow;
							}
							else
							{
								TimeCache nuevoCache = new TimeCache();
								nuevoCache.Key = (byte)Common.CacheTableKey.Users;
								nuevoCache.TimeStamp = DateTime.UtcNow;
								almacen.TimeCache.Add(nuevoCache);
							}
							await almacen.SaveChangesAsync();
							return nuevoUsuario.guid; //Devolvemos el nuevo usuario creado
						}
					}
				}
			}
			return Guid.Empty; //Salida de error.
		}

		/// <summary>
		/// Obtiene la tabla restringida de usuarios para la copia en la caché del sistema.
		/// </summary>
		/// <param name="request"></param>
		/// <returns></returns>
		[HttpPut("smalluserlist")]
		public async Task<Dictionary<Guid, UserModelBase>> SmallUsersList(BasicRequestModel request)
		{
			Dictionary<Guid, UserModelBase> salida = new Dictionary<Guid, UserModelBase>();
			using (DataStorage almacen = new DataStorage(mvarConfig))
			{
				IEnumerable<User> entrada = await almacen.Users.ToListAsync();
				foreach (User user in entrada)
				{
					UserModelBase auxModelo = await modeloFromBaseUser(user);
					salida.Add(auxModelo.guid, auxModelo);
				}
			}
			return salida;
		}

		[HttpPut("isemptypwd")]
		public async Task<bool> IsEmptyPassword(UserLoginModel message)
		{
			using (DataStorage almacen = new DataStorage(mvarConfig))
			{
				User? auxUser = await almacen.retrieveUser(message.userName);
				if (null != auxUser)
				{
					return (null == auxUser.PasswordHash) || (string.Empty.Equals(auxUser.PasswordHash));
				}
			}
			return false;
		}
		[HttpPut("userinfo")]
		public async Task<ExtendedUserModel> UserInfo(UserInfoRequestModel? request)
		{
			//Obtiene toda la información posible de un determinado usuario según los permisos
			//del token enviado
			ExtendedUserModel salida = new ExtendedUserModel();
			bool hasPermission = false;

			//Administrador... puede acceder a toda la información de cualquier usuario
			if (await hasBasicPermission(request.SessionToken, Common.UserRole.Root))
				hasPermission = true;

			//El propio usuario puede acceder a sus propios datos
			ActiveSessionModel? auxSession = await retrieveSession(request.SessionToken);
			if (null != auxSession && auxSession.UserId.Equals(request.UserId.ToString()))
				hasPermission = true;

			if (hasPermission)
			{
				User? auxUsuarioNulo;
				User auxUsuario;
				//Cargo toda la información que puedo sacar de la base de datos..
				using (DataStorage almacen = new DataStorage(mvarConfig))
				{
					auxUsuarioNulo = await almacen.Users.Where(x => x.Id.Equals(request.UserId.ToString())).FirstOrDefaultAsync();
					if (null != auxUsuarioNulo)
					{
						auxUsuario = auxUsuarioNulo;
						salida.CF = auxUsuario.CF;
						salida.UserEnabled = auxUsuario.UserEnabled;
						if (null != auxUsuario.UserName)
							salida.Name = auxUsuario.UserName;
						if (null != auxUsuario.PhoneNumber)
							salida.PhoneNumber = auxUsuario.PhoneNumber;
						if (null != auxUsuario.ShortPhoneNumber)
							salida.ShortPhoneNumber = auxUsuario.ShortPhoneNumber;
						if (null != auxUsuario.Email)
							salida.Email = auxUsuario.Email;
						salida.guid = auxUsuario.guid;
						salida.NullPassword = (null == auxUsuario.PasswordHash) || (auxUsuario.PasswordHash.Length < 1);
						salida.TelegramEnabled = auxUsuario.TelegramEnabled;
						salida.TelegramPaired = auxUsuario.TelegramId != 0;
						salida.TelegramRules = await almacen.GetRegisterValue(auxUsuario.guid, "TGRULES", string.Empty);
					}
				}
				salida.roles = await retrieveRolesDictionary();
				//Recuperamos los roles del usuario
				if (null != auxUsuarioNulo)
				{
					List<uint> auxRoles = await retrieveUserRoles(auxUsuarioNulo.guid);
					foreach (uint role in auxRoles)
					{
						if (salida.roles.ContainsKey(role))
							salida.roles[role].enrolled = true;
					}
					
				}				
			}
			return salida;
		}

		public async Task<string> getTelegramRules(Guid userId)
		{
			using (DataStorage almacen = new DataStorage(mvarConfig))
			{
				return await almacen.GetRegisterValue(userId, "TGRULES", string.Empty);
			}
		}

		public async Task<bool> pairUser(Guid userId, long telegramId)
		{
			using (DataStorage almacen = new DataStorage(mvarConfig))
			{
				User? candidato = await almacen.Users.Where(x => x.Id == userId.ToString()).FirstOrDefaultAsync();
				if(null!=candidato)
				{
					candidato.TelegramEnabled = true;
					candidato.TelegramId = telegramId;
					return await almacen.SaveChangesAsync() > 0;
				}
			}
			return false;
		}

		/// <summary>
		/// Enrola o saca al usuario especificado de un determinado rol ya establecido.
		/// </summary>
		/// <param name="tokenId">Token con las credenciales de autorización</param>
		/// <param name="userId">Usuario al que vamos a enrolar (o desenrolar)</param>
		/// <param name="roleId">Id del rol</param>
		/// <param name="enrole">True para enrolar y false para desenrolar</param>
		/// <returns>True si ha tenido éxito o false si no se ha podido actualizar por alguna razón</returns>
		[HttpPut("enrole")]
		public async Task<bool> Enrole(string tokenid, string userid, uint roleid, bool enrole)
		{
			Guid token = Guid.Empty;
			Guid.TryParse(tokenid, out token);
			if (await hasBasicPermission(token,Common.UserRole.Root))
			{
				Guid userGuid = Guid.Empty;
				Guid.TryParse(userid, out userGuid);
				List<uint> currentRoles = await retrieveUserRoles(userGuid);
				using (DataStorage almacen = new DataStorage(mvarConfig))
				{
					if (currentRoles.Contains(roleid))
					{
						if (!enrole)
						{ //Sacamos al usuario del rol
							if(await auxDerole(userid, roleid,almacen))
							{
								return await almacen.SaveChangesAsync() > 0;
							}
						}
					}
					else
					{
						if (enrole)
						{ //Metemos al usuario en el rol
							auxEnrole(userid, roleid,almacen);
							return await almacen.SaveChangesAsync() > 0;
						}
					}
				}					
			}
			return false; //No tenía permiso
		}

		/// <summary>
		/// Procesa un batch de roles para un usuario determinado.
		/// </summary>
		/// <param name="tokenId">Token del administrador</param>
		/// <param name="userId">Id del usuario que va a cambiar sus roles</param>
		/// <param name="enroles">Lista separada por comas con los RoleId que va a ganar</param>
		/// <param name="deroles">Lista separada por comas con los RoleId que va a perder</param>
		/// <returns></returns>
		[HttpPut("changeroles")]
		public async Task<bool> ChangeRoles(UpdateRolesChangeMessage message)
		{
			if (await hasBasicPermission(message, Common.UserRole.Root))
			{
				List<uint> currentRoles = await retrieveUserRoles(message.UserId);
				uint rolUid = 0;
				using (DataStorage almacen = new DataStorage(mvarConfig))
				{
					if (message.colEnrole.Count > 0)
					{
						foreach (uint auxRolId in message.colEnrole)
						{
							if (!currentRoles.Contains(auxRolId))
							{
								auxEnrole(message.UserId.ToString(), auxRolId, almacen);
								currentRoles.Add(auxRolId);
							}
						}
					}
					if(message.colDerole.Count > 0)
					{
						foreach(uint auxRolId in message.colDerole)
						{
							if(currentRoles.Contains(auxRolId))
							{
								//Sacamos al usuario del rol
								await auxDerole(message.UserId.ToString(), auxRolId, almacen);
								currentRoles.Remove(auxRolId);
							}
						}
					}
					return await almacen.SaveChangesAsync() > 0;
				}
			}
			return false;
		}

		/// <summary>
		/// Fuerza una desconexión de Telegram de este usuario.
		/// </summary>
		/// <param name="message">Lo único importante del mensaje es el ID del usuario</param>
		/// <returns></returns>
		[HttpPut("unpairtelegram")]		
		public async Task<bool> UnpairTelegram(UpdateUserPersonalDataMessage message)
		{
			if (await hasBasicPermission(message, Common.UserRole.Root))
			{
				using (DataStorage almacen = new DataStorage(mvarConfig))
				{
					//Recuperamos el usuario
					User? usuario = await almacen.Users.Where(x => x.Id == message.UserId.ToString()).FirstOrDefaultAsync();
					if (null != usuario)
					{
						//Tenemos que comprobar que el CF que vamos a cambiar NO exista en la base de datos.
						List<User> duplicates = await almacen.Users.Where(x => x.Id != message.UserId.ToString() && x.CF.Equals(message.CF)).ToListAsync();
						if (duplicates.Any()) return false; //No podemos hacer el cambio.
						await mvarHubContext.Clients.All.SendAsync("UnpairTelegramUser", usuario.TelegramId);
						usuario.TelegramId = 0; //Anulamos la sesión
						usuario.TelegramEnabled = false; //Damos de baja Telegram						
						return await almacen.SaveChangesAsync() > 0;
					}
				}				
			}
			return false;
		}

		[HttpPut("modifyuser")]
		public async Task<bool> EditUser(UpdateUserPersonalDataMessage message)
		{
			if (await hasBasicPermission(message, Common.UserRole.Root))
			{
				using (DataStorage almacen = new DataStorage(mvarConfig))
				{
					//Recuperamos el usuario
					User? usuario = await almacen.Users.Where(x => x.Id == message.UserId.ToString()).FirstOrDefaultAsync();
					if(null!=usuario)
					{
						if(message.UpdateUserStatus)
						{
							if (null != message.CF)
							{
								//Tenemos que comprobar que el CF que vamos a cambiar NO exista en la base de datos.
								List<User> duplicates = await almacen.Users.Where(x => x.Id != message.UserId.ToString() && x.CF.Equals(message.CF)).ToListAsync();
								if (duplicates.Any()) return false; //No podemos hacer el cambio.

								//Pero si hemos llegado aquí, entonces sí que podemos hacerlo.
								usuario.CF = message.CF;
							}
							usuario.UserEnabled = message.UserEnabled;
							if (null != message.Email)
							{
								usuario.Email = message.Email;
								usuario.NormalizedEmail = message.Email.ToUpper();
							}
							if (null != message.UserName)
							{
								usuario.UserName = message.UserName;
								usuario.NormalizedUserName = message.UserName.ToUpper();
							}
							if (null != message.Phone)
								usuario.PhoneNumber = message.Phone;

							if (null != message.ShortPhone)
								usuario.ShortPhoneNumber = message.ShortPhone;
						}
						if(message.UpdateTelegramStatus)
						{
							usuario.TelegramEnabled = message.TelegramEnabled;
							if (null != message.TelegramRules)
							{
								Guid auxGuid = Guid.Empty;
								if (Guid.TryParse(usuario.Id, out auxGuid))
								{
									await almacen.SetRegisterValue(auxGuid, "TGRULES", message.TelegramRules);
								}
							}
						}
						return await almacen.SaveChangesAsync() > 0;
					}
				}
			}
			return false;
		}

		[HttpPut("resetpwd")]
		public async Task<bool> ResetPassword(ResetPasswordDataMessage message)
		{
			if (await hasBasicPermission(message, Common.UserRole.Root))
			{
				using (DataStorage almacen = new DataStorage(mvarConfig))
				{
					User? usuario = await almacen.Users.Where(x => x.Id == message.UserId.ToString()).FirstOrDefaultAsync();
					if (null != usuario)
					{
						usuario.PasswordHash = string.Empty;
						return await almacen.SaveChangesAsync() > 0;
					}
				}
			}
			return false;
		}

		[HttpPut("setpwd")]
		public async Task<bool> SetPassword(SetPasswordDataMessage message)
		{
			using (DataStorage almacen = new DataStorage(mvarConfig))
			{
				User? auxUsuario = await almacen.retrieveUser(message.UserName);
				if(null != auxUsuario)
				{
					User? auxUser2 = await almacen.Users.Where(x => x.Id == auxUsuario.Id).FirstOrDefaultAsync();
					if(null!= auxUser2 && (null==auxUser2.PasswordHash || auxUser2.PasswordHash.Length<1))
					{
						if(null!=message.Password)
						{
							string salado = almacen.HashPassword(message.Password);
							auxUser2.PasswordHash = salado;
							return await almacen.SaveChangesAsync() >= 0;
						}				
					}
				}
			}
			return false;
		}
		[HttpPut("usractivity")]
		public async Task<UserActivityModel> GetUserActivity(UserActivityRequest request)
		{
			UserActivityModel salida = new UserActivityModel();
			if (await hasBasicPermission(request, Common.UserRole.Root))
			{
				using (DataStorage almacen = new DataStorage(mvarConfig))
				{
					IEnumerable<SessionEvent> entrada;
					if (0 != request.maxRecords)
					{
						entrada = await almacen.SessionEvents.Where(x => x.userId.Equals(request.userId)).Take(request.maxRecords).OrderByDescending(x=>x.timeSpan).ToListAsync();
					}
					else
					{
						entrada = await almacen.SessionEvents.Where(x => x.userId.Equals(request.userId)).OrderByDescending(x=>x.timeSpan).ToListAsync();
					}
					foreach (SessionEvent evento in entrada)
					{
						UserActivityModel.UserActivityAtom nuevo = new UserActivityModel.UserActivityAtom();
						nuevo.timeStamp = evento.timeSpan;
						nuevo.type = evento.eventType;
						salida.activity.Add(nuevo);
					}
				}
			}
			return salida;
		}
		private void auxEnrole(string userId, uint roleId, DataStorage storage)
		{
			UserAndRole auxRol = new UserAndRole();
			auxRol.Id = Guid.NewGuid().ToString();
			auxRol.UserId = userId;
			auxRol.RoleId = roleId;
			storage.UserAndRoles.Add(auxRol);
		}
		private async Task<bool> auxDerole(string userId, uint roleId, DataStorage storage)
		{
			UserAndRole? auxRol = await storage.UserAndRoles.Where(x => x.UserId.Equals(userId) && x.RoleId.Equals(roleId)).FirstOrDefaultAsync();
			if (null != auxRol)
			{
				storage.UserAndRoles.Remove(auxRol);
				return true;
			}
			return false;
		}
		/// <summary>
		/// Saca el diccionario de roles que tenemos en la base de datos
		/// </summary>
		/// <returns></returns>
		private async Task<Dictionary<uint,ExtendedUserModel.RoleInfo>> retrieveRolesDictionary()
		{
			Dictionary<uint, ExtendedUserModel.RoleInfo> salida = new Dictionary<uint, ExtendedUserModel.RoleInfo>();
			using (DataStorage almacen = new DataStorage(mvarConfig))
			{
				List<RoleDictionary> auxDictionary = await almacen.RoleDictionary.OrderBy(x=>x.RoleId).ToListAsync();
				foreach (RoleDictionary auxEntrada in auxDictionary)
				{
					ExtendedUserModel.RoleInfo nuevoRol = new ExtendedUserModel.RoleInfo();
					nuevoRol.roleId = auxEntrada.RoleId;
					nuevoRol.Name = auxEntrada.Name;
					nuevoRol.Comment = auxEntrada.Comment;
					salida.Add(auxEntrada.RoleId, nuevoRol);
				}
			}
			return salida;
		}

		internal async Task<List<uint>> retrieveUserRoles(Guid userId)
		{
			List<uint> salida = new List<uint>();
			string userString = userId.ToString();
			using (DataStorage almacen = new DataStorage(mvarConfig))
			{
				List<UserAndRole> entrada = await almacen.UserAndRoles.Where(x => x.UserId.Equals(userString)).ToListAsync();
				foreach (UserAndRole role in entrada)
					salida.Add(role.RoleId);
			}
			return salida;
		}							
		internal static async Task<UserModel> modeloFromUser(User user, IConfiguration config)
		{
			UserModel salida = new UserModel();
			salida.guid = user.guid;
			salida.CF = user.CF;
			if(null!=user.UserName)
				salida.Name = user.UserName;
			if(null!=user.Email)
			salida.Email = user.Email;
			if(null!=user.PhoneNumber)
				salida.PhoneNumber = user.PhoneNumber;
			salida.ShortPhoneNumber = user.ShortPhoneNumber;
			salida.AccessFailedCount = user.AccessFailedCount;
			salida.NullPassword = (null==user.PasswordHash) || (user.PasswordHash.Length < 1);
			salida.CredentialKey = await userIcon(user,config);
			salida.TelegramEnabled = user.TelegramEnabled;
			salida.HasTelegramId = (0!=user.TelegramId);
			return salida;
		}
		private async Task<UserModelBase> modeloFromBaseUser(User user)
		{
			UserModelBase salida = new UserModelBase();
			salida.guid = user.guid;
			salida.CF = user.CF;
			if (null != user.PhoneNumber)
				salida.PhoneNumber = user.PhoneNumber;			
			salida.ShortPhoneNumber = user.ShortPhoneNumber;
			if (null!= user.UserName)
				salida.Name = user.UserName;
			salida.CredentialKey = await userIcon(user,mvarConfig);
			return salida;
		}

		private static async Task<byte> userIcon(User user, IConfiguration config)
		{
			using (DataStorage almacen = new DataStorage(config))
			{
				List<UserAndRole> roles = await almacen.UserAndRoles.Where(x => x.UserId == user.Id && x.RoleId<7).ToListAsync();
				byte salida = 0;
				foreach (UserAndRole item in roles)
				{
					salida = Utils.setBit(salida, (byte)item.RoleId);					
				}
				return salida;		
			}
		}
				
	}	
}

