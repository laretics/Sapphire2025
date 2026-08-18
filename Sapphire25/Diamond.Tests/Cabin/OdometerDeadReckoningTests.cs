using Diamond.Cabin;

namespace Diamond.Tests.Cabin
{
	public class OdometerDeadReckoningTests
	{
		[Fact]
		public void Project_IncreasingPk_AddsTraveledMeters()
		{
			OdometerDeadReckoning reckoning = new OdometerDeadReckoning();
			reckoning.Arm(1000, 50_000);

			Assert.Equal(50_400, reckoning.Project(1400, pkIncreasing: true));
		}

		[Fact]
		public void Project_DecreasingPk_SubtractsTraveledMeters()
		{
			OdometerDeadReckoning reckoning = new OdometerDeadReckoning();
			reckoning.Arm(1000, 50_000);

			Assert.Equal(49_600, reckoning.Project(1400, pkIncreasing: false));
		}

		[Fact]
		public void Project_OdometerGoesBackwards_DoesNotReverse()
		{
			OdometerDeadReckoning reckoning = new OdometerDeadReckoning();
			reckoning.Arm(1000, 50_000);

			Assert.Equal(50_000, reckoning.Project(800, pkIncreasing: true));
		}

		[Fact]
		public void Resync_ResetsOrigin()
		{
			OdometerDeadReckoning reckoning = new OdometerDeadReckoning();
			reckoning.Arm(1000, 50_000);
			reckoning.Resync(1800, 52_000);

			Assert.True(reckoning.Armed);
			Assert.Equal(1800, reckoning.OriginOdometer);
			Assert.Equal(52_000, reckoning.OriginPk);
			Assert.Equal(52_200, reckoning.Project(2000, pkIncreasing: true));
		}

		[Fact]
		public void Disarm_StopsUntilArmedAgain()
		{
			OdometerDeadReckoning reckoning = new OdometerDeadReckoning();
			reckoning.Arm(1000, 50_000);
			reckoning.Disarm();

			Assert.False(reckoning.Armed);
		}
	}
}
