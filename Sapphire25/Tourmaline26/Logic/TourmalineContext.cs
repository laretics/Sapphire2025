using Microsoft.EntityFrameworkCore;
using Sapphire2026.Data;
using Sapphire2026.Data.Models;
using System.Security.Cryptography;
using System.Text;
using TimeNet2026.DBStorage;
using TimeNet2026Data;
using TimeNet2026Data.DBStorage;
using Tourmaline26.Services.LocalDataModel;

namespace Tourmaline26.Logic
{
    public class TourmalineContext:DbContext, ITimeNetContextStorage
    {
        public TourmalineContext(DbContextOptions<TourmalineContext> options) : base(options)
        { }
        public DbSet<User> Users { get; set; }
        public DbSet<StatusChange> StatusChanges { get; set; }
        public DbSet<Train> Trains { get; set; }
        public DbSet<Note> Notes { get; set; }
        public DbSet<DBLocalSystem> LocalSystem { get; set; } //Info local sobre el tren.

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
    }
}
