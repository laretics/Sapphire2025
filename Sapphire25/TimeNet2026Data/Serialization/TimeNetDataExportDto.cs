using TimeNet2026.DBStorage;
using TimeNet2026Data.DBStorage;

/// <summary>
/// Descriptor de información enviada en el paquete de serialización.
/// </summary>
[MessagePack.MessagePackObject]
public class TimeNetDataExportDto
{
	[MessagePack.Key(0)]
	public List<DBHeader> Headers { get; set; } = new();
	[MessagePack.Key(1)]
	public List<DBTopoStorage> TopoStorages { get; set; } = new();
	[MessagePack.Key(2)]
	public List<DBAxis> Axis { get; set; } = new();
	[MessagePack.Key(3)]
	public List<DBStation> Stations { get; set; } = new();
	[MessagePack.Key(4)]
	public List<DBRefPunctual> RefPunctuals { get; set; } = new();
	[MessagePack.Key(5)]
	public List<DBRauta> Rautatie { get; set; } = new();
	[MessagePack.Key(6)]
	public List<DBPlan> Plans { get; set; } = new();
	[MessagePack.Key(7)]
	public List<DBCirculationBlock> CirculationBlocks { get; set; } = new();
	[MessagePack.Key(8)]
	public List<DBCirculation> Circulations { get; set; } = new();
	[MessagePack.Key(9)]
	public List<DBSchedule> Schedules { get; set; } = new();
	[MessagePack.Key(10)]
	public List<DBScheduleUnit> ScheduleUnits { get; set; } = new();
	[MessagePack.Key(11)]
	public List<DBAsimilation> Asimilations { get; set; } = new();
	[MessagePack.Key(12)]
	public List<DBAsimilationStep> AsimilationSteps { get; set; } = new();
}