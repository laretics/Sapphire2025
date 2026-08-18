using Diamond.Cabin;

namespace Diamond.Tests.Cabin
{
	public class LinearLocationTests
	{
		[Fact]
		public void SetOdometer_MarksSourceAndPk()
		{
			LinearLocation location = new LinearLocation();
			location.SetOdometer(12_345);

			Assert.Equal(12_345, location.PKRef);
			Assert.Equal(LinearLocationSource.Odometer, location.Source);
			Assert.True(location.LastOdometerUpdate > DateTime.MinValue);
		}
	}
}
