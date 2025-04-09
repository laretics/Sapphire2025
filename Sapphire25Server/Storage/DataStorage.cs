using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Diagnostics;
using static Zafiro25.Models.Common;
using Zafiro25.Models.Model;
using Zafiro25.Models;
namespace Sapphire25Server.Storage
{
	public class DataStorage:DbContext
	{
		public DbSet<Train> Trains { get; set; }
		public DbSet<StatusChange> StatusChanges { get; set; }
		public DbSet<ActionRecord> ActionRecords { get; set; }
		public DbSet<ActionRecordOperation> ActionRecordOperations { get; set; }
		public DbSet<ActionRecordElement> ActionRecordElements { get; set; }
		public DbSet<SFMUser> Users { get; set; }
		public DbSet<Role> Roles { get; set; }
		public DbSet<UserRole> UserRoles { get; set; }

		public DataStorage(DbContextOptions<DataStorage> options)
		: base(options)
		{


		}

		protected override void OnModelCreating(ModelBuilder modelBuilder)
		{
			base.OnModelCreating(modelBuilder);
			modelBuilder.Entity<SFMUser>().ToTable("AspNetUsers");
			modelBuilder.Entity<Role>().ToTable("AspNetRoles");
			modelBuilder.Entity<UserRole>().ToTable("AspNetUserRoles");
		}

		private void seedTrains(ModelBuilder builder)
		{
			builder.Entity<Train>().HasData
				(
				new Train { Name = "7101-7102" },
				new Train { Name = "7103-7104" },
				new Train { Name = "7105-7106" },
				new Train { Name = "7107-7108" },
				new Train { Name = "7109-7110" },
				new Train { Name = "7111-7112" },
				new Train { Name = "8101-8102" },
				new Train { Name = "8103-8104" },
				new Train { Name = "8105-8106" },
				new Train { Name = "8107-8108" },
				new Train { Name = "8109-8110" },
				new Train { Name = "8111-8112" },
				new Train { Name = "8113-8114" },
				new Train { Name = "8115-8116" },
				new Train { Name = "8117-8118" },
				new Train { Name = "8119-8120" },
				new Train { Name = "8121-8122" },
				new Train { Name = "8123-8124" },
				new Train { Name = "8125-8126" },
				new Train { Name = "9101-9102" },
				new Train { Name = "9103-9104" },
				new Train { Name = "9105-9106" },
				new Train { Name = "9107-9108" },
				new Train { Name = "9109-9110" },
				new Train { Name = "9111-9112" },
				new Train { Name = "1101-1102" },
				new Train { Name = "1103-1104" },
				new Train { Name = "1105-1106" },
				new Train { Name = "1107-1108" },
				new Train { Name = "1109-1110" }
				);
		}

		public async Task InitialCommit()
		{
			//Esta función es un proceso de actualización automática de todos los trenes al estado
			//de disponible.
			//Sólo lo haré si el registro de cambios está vacío.
			if (await StatusChanges.CountAsync() > 0) return;

			SFMUser? auxRoot = await this.Users.Where(xx => xx.UserName == "Root").FirstOrDefaultAsync();

			Debug.Assert(auxRoot != null);
			foreach (Train train in await Trains.ToListAsync())
			{
				await CommitOperation(train.Guid, Common.OperationType.Activate, auxRoot, "[Initial Commit 0]");
				await CommitOperation(train.Guid, Common.OperationType.BeginCorrective, auxRoot, "[Initial Commit 1]");
				await CommitOperation(train.Guid, Common.OperationType.EndCorrective, auxRoot, "[Initial Commit 2]");
			}
		}

		public async Task<bool> CommitOperation(Guid trainId, Common.OperationType operation, SFMUser? user, string? comment)
		{
			if (Common.OperationType.Unknown == operation) return false;
			if (null == user) return false;
			if (Guid.Empty == trainId) return false;

			Train? auxTrain = await Trains.Where(xx => xx.Guid == trainId).FirstOrDefaultAsync();
			if (null == auxTrain) return false;

			//Generamos registro de cambio
			StatusChange auxCambio = new StatusChange();
			auxCambio.TrainId = trainId;
			auxCambio.TimeStamp = DateTime.Now;
			if (null == comment)
				auxCambio.Comment = string.Empty;
			else
				auxCambio.Comment = comment;
			auxCambio.Operation = operation;
			auxCambio.UserId = user.Id;
			await StatusChanges.AddAsync(auxCambio);

			//Actualizamos datos en la caché del tren
			auxTrain.lastChange = auxCambio.Guid;

			int cambios = await SaveChangesAsync();
			return cambios > 0;
		}

		public async Task<List<String>> RolesByUser(string userId)
		{
			List<String> salida = new List<String>();
			var auxRoles = await (from user in Users
								  where user.Id == userId
								  join userRole in UserRoles
								  on user.Id equals userRole.UserId
								  join role in Roles
								  on userRole.RoleId equals role.Id
								  select role.Name).ToListAsync();
			foreach (string auxName in auxRoles)
				salida.Add(auxName);
			return salida;
		}


	}
}
