using Microsoft.EntityFrameworkCore;
using TimeNet2026.DBStorage;
using TimeNet2026Data.DBStorage;

namespace TimeNet2026Data
{
	public class TimeNetContext:DbContext , ITimeNetContextStorage
	{
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
		protected override void OnModelCreating(ModelBuilder modelBuilder)
		{
			modelBuilder.Entity<DBAsimilationStep>().ToTable("AsimilationSteps");
			modelBuilder.Entity<DBAsimilation>().ToTable("Asimilations");
			modelBuilder.Entity<DBAxis>().ToTable("Axis");
			modelBuilder.Entity<DBCirculationBlock>().ToTable("CirculationBlocks");
			modelBuilder.Entity<DBCirculation>().ToTable("Circulations");
			modelBuilder.Entity<DBHeader>().ToTable("Headers");
			modelBuilder.Entity<DBPlan>().ToTable("Plans");
			modelBuilder.Entity<DBRauta>().ToTable("Rautatie");
			modelBuilder.Entity<DBRefPunctual>().ToTable("RefPunctuals");
			modelBuilder.Entity<DBRefPunctual>()
				.HasKey(e => new { e.AxisId, e.Pk }); // Clave primaria compuesta
			modelBuilder.Entity<DBScheduleUnit>().ToTable("ScheduleUnits");
			modelBuilder.Entity<DBSchedule>().ToTable("Schedules");
			modelBuilder.Entity<DBStation>().ToTable("Stations");
			modelBuilder.Entity<DBTopoStorage>().ToTable("TopoStorages");		
		}
		public TimeNetContext(DbContextOptions<TimeNetContext> opciones) : base(opciones) { }
		async Task<int> ITimeNetContextStorage.SaveChangesAsync()
		{
			return await base.SaveChangesAsync();
		}
		#region Header

		#endregion Header

	}
}
