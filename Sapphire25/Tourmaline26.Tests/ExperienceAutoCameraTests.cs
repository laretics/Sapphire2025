using Tourmaline26.Services.TourmalineExperience;

namespace Tourmaline26.Tests;

public sealed class ExperienceAutoCameraTests
{
	[Fact]
	public void Pool_covers_cenital_drone_frikis_and_cab()
	{
		Assert.Contains(TourmalineCameraOrder.Cenital, ExperienceAutoCamera.Views);
		Assert.Contains(TourmalineCameraOrder.Drone, ExperienceAutoCamera.Views);
		Assert.Contains(TourmalineCameraOrder.TrackSide, ExperienceAutoCamera.Views);
		Assert.Contains(TourmalineCameraOrder.Brakeman, ExperienceAutoCamera.Views);
		Assert.Equal(4, ExperienceAutoCamera.Views.Length);
	}

	[Fact]
	public void Pick_does_not_repeat_previous_view()
	{
		var rng = new Random(7);
		foreach (TourmalineCameraOrder previous in ExperienceAutoCamera.Views)
		{
			int n = 0;
			while (n < 40)
			{
				(TourmalineCameraOrder order, _) = ExperienceAutoCamera.Pick(previous, rng);
				Assert.NotEqual(previous, order);
				n++;
			}
		}
	}

	[Fact]
	public void Pick_uses_both_sides()
	{
		var rng = new Random(3);
		bool sawLeft = false;
		bool sawRight = false;
		int n = 0;
		while (n < 80)
		{
			(_, bool side) = ExperienceAutoCamera.Pick(null, rng);
			if (side)
				sawRight = true;
			else
				sawLeft = true;
			n++;
		}
		Assert.True(sawLeft && sawRight);
	}

	[Fact]
	public void Pick_can_select_every_view()
	{
		var rng = new Random(11);
		var seen = new HashSet<TourmalineCameraOrder>();
		TourmalineCameraOrder? previous = null;
		int n = 0;
		while (n < 80)
		{
			(TourmalineCameraOrder order, _) = ExperienceAutoCamera.Pick(previous, rng);
			seen.Add(order);
			previous = order;
			n++;
		}
		Assert.Equal(ExperienceAutoCamera.Views.Length, seen.Count);
	}
}
