using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TimeNet2026Data.DBStorage;
using Microsoft.EntityFrameworkCore;
using TimeNet2026.DBStorage;

namespace TimeNet2026Data
{
	public interface ITimeNetContextStorage
	{
       DbSet<DBHeader> Headers { get; }
       DbSet<DBRauta> Rautatie { get; }
       DbSet<DBPlan> Plans { get; }
       DbSet<DBCirculationBlock> CirculationBlocks { get; }
       DbSet<DBTopoStorage> TopoStorages { get; }
       DbSet<DBAxis> Axis { get; }
       DbSet<DBStation> Stations { get; }
       DbSet<DBRefPunctual> RefPunctuals { get; }
       DbSet<DBAsimilationStep> AsimilationSteps { get; }
       DbSet<DBAsimilation> Asimilations { get; }
       DbSet<DBSchedule> Schedules { get; }
       DbSet<DBScheduleUnit> ScheduleUnits { get; }
       DbSet<DBCirculation> Circulations { get; }
       Task<int> SaveChangesAsync();
	}
}
