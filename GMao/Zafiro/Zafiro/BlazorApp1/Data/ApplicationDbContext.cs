using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using Pomelo.EntityFrameworkCore.MySql.Infrastructure;
using ZafiroGmao.Data.Models;
using ZafiroGmao.Data.Seeding;

namespace ZafiroGmao.Data
{
    public class ApplicationDbContext : IdentityDbContext<SFMUser>
    {
        public DbSet<Train> Trains { get; set; }
        public DbSet<StatusChange> StatusChanges { get; set; }
        public DbSet<ActionRecord> ActionRecords { get; set; }
        public DbSet<ActionRecordOperation> ActionRecordOperations { get; set; }
        public DbSet<ActionRecordElement> ActionRecordElements { get; set; }

        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
            Database.EnsureCreated();
        }
        public ApplicationDbContext() : base() 
        {
            Database.EnsureCreated();
        }
		protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
		{
            optionsBuilder.UseMySql("Server=localhost;Database=zafiro;User=ZafiroAdmin;Password=Zafiro55;",
                new MySqlServerVersion(new Version(8,0,21)));
		}

		protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);
            seedTrains(builder);
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

            Debug.Assert( auxRoot != null );
            foreach (Train train in await Trains.ToListAsync())
            {
                await CommitOperation(train.Guid, Common.OperationType.Activate, auxRoot, "[Initial Commit 0]");
                await CommitOperation(train.Guid, Common.OperationType.BeginCorrective, auxRoot, "[Initial Commit 1]");
                await CommitOperation(train.Guid, Common.OperationType.EndCorrective, auxRoot, "[Initial Commit 2]");
            }         
        }

        public async Task<bool> CommitOperation(Guid trainId,ZafiroGmao.Data.Models.Common.OperationType operation,SFMUser? user, string? comment)
        {
            if (ZafiroGmao.Data.Models.Common.OperationType.Unknown == operation) return false;
            if(null==user) return false;
            if(Guid.Empty==trainId) return false;

            Train? auxTrain = await Trains.Where(xx => xx.Guid == trainId).FirstOrDefaultAsync();
            if(null==auxTrain) return false;

            //Generamos registro de cambio
            ZafiroGmao.Data.Models.StatusChange auxCambio = new StatusChange();
            auxCambio.TrainId = trainId;
            auxCambio.TimeStamp = DateTime.Now;
            if (null == comment)
                auxCambio.Comment = string.Empty;
            else
                auxCambio.Comment = comment;
            auxCambio.Operation = operation;
            auxCambio.UserId = user.Id;
            await StatusChanges.AddAsync( auxCambio );

            //Actualizamos datos en la caché del tren
            auxTrain.lastChange = auxCambio.Guid;

            int cambios = await SaveChangesAsync();
            return cambios>0;
        }

    }
}