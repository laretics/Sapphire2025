using System;
using System.Linq;
using System.Collections.Generic;
using Diamond.Topo;
using Diamond.Timed;
using Diamond.Tests;

var topo = TopoXmlSerializer.Load(SamplePaths.TopoSfm227);
SfmDemoInfrastructure.Apply(topo);
var t3 = topo.FindAxisById("T3")!;
var t2 = topo.FindAxisById("T2")!;
foreach (var s in t3.Stations.OrderBy(x=>x.PK)) {
  if ((s.Station.Name??"").Contains("Enlla", StringComparison.OrdinalIgnoreCase)
   || (s.Station.Avr??"")=="PMI" || (s.Station.Avr??"")=="RLL" || (s.Station.Avr??"")=="MAN"
   || (s.Station.Avr??"")=="INC" || (s.Station.Name??"").Contains("Rull", StringComparison.OrdinalIgnoreCase))
    Console.WriteLine($"T3 {s.PK,8} {s.Station.Avr,-4} {s.Station.Name}");
}
foreach (var s in t2.Stations.OrderBy(x=>x.PK)) {
  if ((s.Station.Name??"").Contains("Enlla", StringComparison.OrdinalIgnoreCase)
   || (s.Station.Avr??"")=="SPB" || (s.Station.Avr??"")=="LLB")
    Console.WriteLine($"T2 {s.PK,8} {s.Station.Avr,-4} {s.Station.Name}");
}
var palma = t3.Stations.First(s => s.Station.Avr=="PMI" || s.Station.Id=="01");
var enT3 = t3.Stations.First(s => s.Station.Name.Contains("Enlla", StringComparison.OrdinalIgnoreCase));
var enT2 = t2.Stations.First(s => s.Station.Name.Contains("Enlla", StringComparison.OrdinalIgnoreCase));
var spb = t2.Stations.First(s => s.Station.Avr=="SPB");
Console.WriteLine($"seg T3 {palma.PK}->{enT3.PK} T2 {enT2.PK}->{spb.PK}");
var ui = RouteView.Concat("T3+T2","x", new List<(Axis,long,long)>{(t3,palma.PK,enT3.PK),(t2,enT2.PK,spb.PK)});
Console.WriteLine("stations on view:");
foreach(var s in ui.Stations) Console.WriteLine($"  {s.PK,8} {s.Station.Avr,-4} {s.Station.Name}");
