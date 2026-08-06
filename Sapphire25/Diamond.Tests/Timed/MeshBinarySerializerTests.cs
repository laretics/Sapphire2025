using Diamond.Timed;
using Diamond.Topo;

namespace Diamond.Tests.Timed
{
	public class MeshBinarySerializerTests
	{
		[Fact]
		public void RoundTrip_AllDays_AndValidityStart()
		{
			TopoLayout topo = TopoXmlSerializer.Load(SamplePaths.TopoSfm227);
			SfmDemoInfrastructure.Apply(topo);
			Plan plan = new Plan(topo);
			plan.EnsureDefaultTrainSpecs();
			Assert.True(plan.CompileDemand("""
				plan "Explotacion"
				require both ways every 60 min PMI -> MAN 06:00-10:00 as R-T3
				  days lab
				  stops 30s
				""").Success);

			DateOnly vigencia = new DateOnly(2026, 9, 1);
			ExploitationPlan original = ExploitationPlan.BuildFromPlan(plan, vigencia, "Explotacion", plan.DemandScript);
			Assert.True(original.Days.Count >= 5); // lab = lun-vie al menos
			Assert.Equal(vigencia, original.ValidityStart);

			using MemoryStream ms = new MemoryStream();
			MeshBinarySerializer.Save(original, ms, topo);
			ms.Position = 0;

			MeshBinarySerializer.LoadResult loaded = MeshBinarySerializer.Load(ms, topo);
			Assert.Equal(vigencia, loaded.ValidityStart);
			Assert.Equal("Explotacion", loaded.PlanName);
			Assert.True(loaded.Plan.Days.Count >= 5);

			// El día es un filtro: lunes y sábado pueden diferir (lab).
			Mesh? mon = loaded.Plan.GetMesh(DayOfWeek.Monday);
			Mesh? sun = loaded.Plan.GetMesh(DayOfWeek.Sunday);
			Assert.NotNull(mon);
			Assert.True(mon!.Circulations.Count >= 2);
			// Domingo no es lab → 0 circ. o menos que laborable
			if (sun is not null)
			{
				Assert.True(sun.Circulations.Count <= mon.Circulations.Count);
			}

			// Total multi-día > un solo día
			Assert.True(loaded.Plan.TotalCirculationCount >= mon.Circulations.Count);
		}

		[Fact]
		public void RoundTrip_PreservesServiceNumbers_OnFilteredDay()
		{
			TopoLayout topo = TopoXmlSerializer.Load(SamplePaths.TopoSfm227);
			SfmDemoInfrastructure.Apply(topo);
			Plan plan = new Plan(topo);
			plan.EnsureDefaultTrainSpecs();
			Assert.True(plan.CompileDemand("""
				plan "RoundTrip"
				require both ways every 60 min PMI -> MAN 06:00-10:00 as R-T3
				  days lab
				  stops 30s
				""").Success);

			ExploitationPlan exp = ExploitationPlan.BuildFromPlan(plan, new DateOnly(2026, 1, 15));
			using MemoryStream ms = new MemoryStream();
			MeshBinarySerializer.Save(exp, ms, topo);
			ms.Position = 0;
			Mesh mesh = MeshBinarySerializer.Load(ms, topo).Plan.GetMesh(DayOfWeek.Monday)!;

			HashSet<string> nums = new HashSet<string>(StringComparer.Ordinal);
			int i = 0;
			while (i < mesh.Circulations.Count)
			{
				if (mesh.Circulations[i].HasServiceNumber)
				{
					nums.Add(mesh.Circulations[i].ServiceNumber);
				}

				Assert.True(mesh.Circulations[i].Asimilation.TotalTime > TimeSpan.Zero);
				i++;
			}

			Assert.True(nums.Count >= 2);
		}

		[Fact]
		public void Load_WithWrongMagic_Throws()
		{
			TopoLayout topo = TopoXmlSerializer.Load(SamplePaths.TopoSfm227);
			using MemoryStream ms = new MemoryStream(new byte[] { 1, 2, 3, 4, 5 });
			Assert.ThrowsAny<Exception>(() => MeshBinarySerializer.Load(ms, topo));
		}

		[Fact]
		public void Load_TamperedPayload_Throws()
		{
			TopoLayout topo = TopoXmlSerializer.Load(SamplePaths.TopoSfm227);
			SfmDemoInfrastructure.Apply(topo);
			Plan plan = new Plan(topo);
			plan.EnsureDefaultTrainSpecs();
			Assert.True(plan.CompileDemand("""
				require both ways every 60 min PMI -> MAN 06:00-08:00 as R
				  days lab
				  stops 30s
				""").Success);
			ExploitationPlan exp = ExploitationPlan.BuildFromPlan(plan, new DateOnly(2026, 1, 1));

			using MemoryStream ms = new MemoryStream();
			MeshBinarySerializer.Save(exp, ms, topo);
			byte[] file = ms.ToArray();
			// Alterar un byte del medio del archivo (rompe la firma).
			int mid = file.Length / 2;
			file[mid] ^= 0xFF;

			using MemoryStream bad = new MemoryStream(file);
			Assert.ThrowsAny<Exception>(() => MeshBinarySerializer.Load(bad, topo));
		}

		[Fact]
		public void Load_UnsignedDmsh_Throws()
		{
			TopoLayout topo = TopoXmlSerializer.Load(SamplePaths.TopoSfm227);
			SfmDemoInfrastructure.Apply(topo);
			Plan plan = new Plan(topo);
			plan.EnsureDefaultTrainSpecs();
			Assert.True(plan.CompileDemand("""
				require both ways every 60 min PMI -> MAN 06:00-08:00 as R
				  days lab
				  stops 30s
				""").Success);
			ExploitationPlan exp = ExploitationPlan.BuildFromPlan(plan, null);

			using MemoryStream raw = new MemoryStream();
			MeshBinarySerializer.WriteUnsignedPayload(exp, raw, topo);
			raw.Position = 0;
			Assert.Throws<InvalidDataException>(() => MeshBinarySerializer.Load(raw, topo));
		}
	}
}
