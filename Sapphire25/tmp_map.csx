using System;
using System.Linq;
using System.Collections.Generic;
using Diamond.Topo;
using Diamond.Timed;
using Diamond.Motion;
using Diamond.Tests;

var topo = TopoXmlSerializer.Load(SamplePaths.TopoSfm227);
SfmDemoInfrastructure.Apply(topo);
var t3 = topo.FindAxisById("T3")!;
var t2 = topo.FindAxisById("T2")!;
var palma = t3.Stations.First(s => s.Station.Avr=="PMI" || s.Station.Id=="01");
var enT3 = t3.Stations.First(s => s.Station.Name.Contains("Enlla", StringComparison.OrdinalIgnoreCase));
var enT2 = t2.Stations.First(s => s.Station.Name.Contains("Enlla", StringComparison.OrdinalIgnoreCase));
var spb = t2.Stations.First(s => s.Station.Avr=="SPB");
var man = t3.Stations.First(s => s.Station.Avr=="MAN" || s.Station.Name.Contains("Manacor", StringComparison.OrdinalIgnoreCase));
var inc = t3.Stations.First(s => s.Station.Avr=="INC");
Console.WriteLine($"PMI={palma.PK} INC={inc.PK} EnT3={enT3.PK} ({enT3.Station.Name}) MAN={man.PK}");
Console.WriteLine($"EnT2={enT2.PK} SPB={spb.PK}");

var ui = RouteView.Concat("T3+T2","x", new List<(Axis,long,long)>{(t3,palma.PK,enT3.PK),(t2,enT2.PK,spb.PK)});
var t3View = RouteView.FromAxis(t3);
Console.WriteLine("UI sig="+ui.PathSignature()+" end="+ui.PKEnd);
Console.WriteLine("T3 view PK "+t3View.PK+".."+t3View.PKEnd);

// Map key stations from T3 full to UI
foreach (var st in new[]{palma,inc,enT3,man}) {
  long dpk;
  bool ok = ui.TryMapRoutePkFrom(t3View, st.PK, out dpk);
  Console.WriteLine($"map T3 PK {st.PK} ({st.Station.Avr}) -> UI {(ok?dpk.ToString():"FAIL")}");
}

// Asimilation PMI-MAN
var plan = new Plan(topo);
plan.EnsureDefaultTrainSpecs();
plan.CompileDemand("require both ways every 60 min PMI -> MAN 06:00-10:00 as R\n  days lab\n  stops 30s\n");
var mesh = new MeshPlanner(plan).Solve(DayOfWeek.Monday);
var c = mesh.Circulations.First(x => x.Asimilation.Origin.Station.Avr=="PMI" || x.Asimilation.Destination.Station.Avr=="MAN");
Console.WriteLine("sample "+c.Id+" "+c.Asimilation.Origin.Station.Avr+"->"+c.Asimilation.Destination.Station.Avr+" view="+c.Asimilation.View.PathSignature());
long maxOk=-1; int failAfter=-1;
for (int s=0;s<=100;s++) {
  double u=s/100.0;
  long apk=c.Asimilation.PKByTime(TimeSpan.FromSeconds(u*c.Asimilation.TotalTime.TotalSeconds));
  long dpk; bool ok=ui.TryMapRoutePkFrom(c.Asimilation.View, apk, out dpk);
  if (ok) maxOk=dpk; else if (failAfter<0) failAfter=s;
}
Console.WriteLine("max mapped display PK="+maxOk+" first fail probe="+failAfter+" enllac route on ui should be ~"+ (enT3.PK-palma.PK));
// list last stations on UI
foreach (var s in ui.Stations.OrderBy(x=>x.PK).TakeLast(6))
  Console.WriteLine("  ui st "+s.PK+" "+s.Station.Avr+" "+s.Station.Name);
