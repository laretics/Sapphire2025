using Diamond.Timed;
var script = @"days lab
  req PMI -> MAN 06:00
    stops 30s
";
var r = DemandScriptParser.Parse(script);
Console.WriteLine("Success=" + r.Success);
foreach (var e in r.Errors) Console.WriteLine("ERR: " + e);
Console.WriteLine("Reqs=" + r.Requirements.Count);
if (r.Requirements.Count > 0) {
  var req = r.Requirements[0];
  Console.WriteLine("Id=" + req.Id + " From=" + req.From.Text + " To=" + req.To.Text);
  Console.WriteLine("DefaultDwell=" + req.Stops.DefaultDwell);
}
