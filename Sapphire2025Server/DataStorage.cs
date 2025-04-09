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

		#region authentication
		public DbSet<ActiveSessionModel> ActiveSessions { get; set; }
		public DbSet<SessionEvent> SessionEvents { get; set; }
		public DbSet<User> Users { get; set; }
		public DbSet<ExtendedRole> ExtendedRoles { get; set; }
		public DbSet<UserAndRole> UserAndRoles { get; set; }
		public DbSet<RoleDictionary> RoleDictionary { get; set; }

		#endregion authentication
	}
}
