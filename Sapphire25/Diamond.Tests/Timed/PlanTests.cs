using Diamond.Motion;
using Diamond.Timed;

namespace Diamond.Tests.Timed
{
	public class PlanTests
	{
		[Fact]
		public void TrainSpecsCatalog_AddAndFindById()
		{
			Plan plan = new Plan();
			TrainSpecs model = plan.EnsureDefaultTrainSpecs();

			Assert.Single(plan.Fleet);
			Assert.Same(model, plan.FindTrainSpecsById("default"));
			Assert.Equal(0.9, model.Acceleration, 5);
			Assert.Equal(0.8, model.ServiceBrake, 5);

			TrainSpecs metro = new TrainSpecs("metro", "UT Metro", 1.0, 0.9, 100.0);
			plan.AddTrainSpecs(metro);

			Assert.Equal(2, plan.Fleet.Count);
			Assert.Same(metro, plan.FindTrainSpecsById("metro"));
		}

		[Fact]
		public void TrainSpecsCatalog_DuplicateId_Throws()
		{
			Plan plan = new Plan();
			plan.EnsureDefaultTrainSpecs();

			Assert.Throws<InvalidOperationException>(() =>
				plan.AddTrainSpecs(new TrainSpecs("default", "Otro", 1.0, 1.0, 120.0)));
		}

		[Fact]
		public void EnsureDefaultTrainSpecs_IsIdempotent()
		{
			Plan plan = new Plan();
			TrainSpecs a = plan.EnsureDefaultTrainSpecs();
			TrainSpecs b = plan.EnsureDefaultTrainSpecs();
			Assert.Same(a, b);
			Assert.Single(plan.Fleet);
		}
	}
}
