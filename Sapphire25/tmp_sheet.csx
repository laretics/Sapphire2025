using Diamond.Controls.Rendering;
using Diamond.Timed;
using Diamond.Topo;
using Diamond.Tests;

var topo = TopoXmlSerializer.Load(SamplePaths.TopoSfm227);
SfmDemoInfrastructure.Apply(topo);
var plan = new Plan(topo);
plan.EnsureDefaultTrainSpecs();
var script = """
require both ways every 40 min PMI -> MAN 06:00-10:00 as R-T3
  days lab
  stops 30s
  skip RLL Enllaç "Sant Joan" PSJ
  dwell INC 1min
""";
var r = plan.CompileDemand(script);
Console.WriteLine("compile ok="+r.Success+" errs="+string.Join(";", r.Errors));
var mesh = new MeshPlanner(plan).Solve(DayOfWeek.Monday);
var c = mesh.Circulations.First(x => x.Asimilation.Origin.Station.Avr=="PMI" && x.Asimilation.Destination.Station.Avr=="MAN");
Console.WriteLine("train "+(c.HasServiceNumber?c.ServiceNumber:c.Id)+" view="+c.Asimilation.View.Id+" stations="+c.Asimilation.View.Stations.Count);
Console.WriteLine("origin pk="+c.Asimilation.Origin.PK+" dest="+c.Asimilation.Destination.PK);
var doc = CirculationSheetDocument.Build(c, mesh, 36);
Console.WriteLine("frontiers="+doc.Frontiers.Count+" pages="+doc.Pages.Count);
for (int p=0;p<doc.Pages.Count;p++) {
  var page = doc.Pages[p];
  Console.WriteLine(" page "+page.PageNumber+"/"+page.PageCount+" rows="+page.Frontiers.Count
    +" first="+page.Frontiers[0].StationKm+"/"+page.Frontiers[0].DependencyName
    +" last="+page.Frontiers[page.Frontiers.Count-1].StationKm+"/"+page.Frontiers[page.Frontiers.Count-1].DependencyName);
}
int i=0;
foreach (var f in doc.Frontiers) {
  if (i<5 || i>=doc.Frontiers.Count-5 || f.DependencyName.Contains("INCA",StringComparison.OrdinalIgnoreCase) || f.DependencyName.Contains("MANACOR",StringComparison.OrdinalIgnoreCase) || f.DependencyName.Contains("PALMA",StringComparison.OrdinalIgnoreCase))
    Console.WriteLine("  ["+i+"] km="+f.StationKm+" "+f.DependencyName+" pk="+f.RoutePk);
  i++;
}
