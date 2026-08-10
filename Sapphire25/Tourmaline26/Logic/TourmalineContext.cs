using Microsoft.EntityFrameworkCore;
using Sapphire2026.Data.Models;
using Tourmaline26.Services.LocalDataModel;

namespace Tourmaline26.Logic
{
	public class TourmalineContext : DbContext
	{
		public TourmalineContext(DbContextOptions<TourmalineContext> options)
			: base(options)
		{
		}

		public DbSet<User> Users { get; set; }
		public DbSet<StatusChange> StatusChanges { get; set; }
		public DbSet<Train> Trains { get; set; }
		public DbSet<Note> Notes { get; set; }
		public DbSet<DBLocalSystem> LocalSystem { get; set; }
		public DbSet<DBDiamondTopoCache> DiamondTopos { get; set; }
		public DbSet<DBDiamondPublishedPlanCache> DiamondPublishedPlans { get; set; }
	}
}
