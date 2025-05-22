using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Diagnostics;
using Sapphire2025Server.Models;
using Microsoft.EntityFrameworkCore.Metadata;
using MySql.Data.MySqlClient;

namespace Sapphire2025Server
{
	public class DataStorage:DbContext
	{
		private IConfiguration mvarConfig;

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

		public DbSet<TimeCache> TimeCache { get; set; }
		#region authentication
		public DbSet<ActiveSessionModel> ActiveSessions { get; set; }
		public DbSet<SessionEvent> SessionEvents { get; set; }
		public DbSet<User> Users { get; set; }
		public DbSet<ExtendedRole> ExtendedRoles { get; set; }
		public DbSet<UserAndRole> UserAndRoles { get; set; }
		public DbSet<RoleDictionary> RoleDictionary { get; set; }
		public DbSet<Register> Register { get; set; } //Diccionario general del sistema
		public DbSet<OwnerRegister> OwnerRegister { get; set; } //Diccionario de registro de los objetos del sistema.

		#endregion authentication
		#region Aeneas
		public DbSet<StatusChange> StatusChanges { get; set; }
		public DbSet<Train> Trains { get; set; }
		public DbSet<Note> Notes { get; set; }
		#endregion
	}
}
