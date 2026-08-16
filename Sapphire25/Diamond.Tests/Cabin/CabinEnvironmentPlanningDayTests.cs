using Diamond.Cabin;
using Diamond.Project;

namespace Diamond.Tests.Cabin
{
	public class CabinEnvironmentPlanningDayTests
	{
		[Fact]
		public void DefaultsToCalendarDay()
		{
			CabinEnvironment cabin = new CabinEnvironment();
			cabin.ClockNow = new DateTime(2026, 8, 16, 10, 0, 0); // domingo
			Assert.Equal(DayOfWeek.Sunday, cabin.EffectivePlanningDay);
			Assert.False(cabin.HasPlanningDayOverride);
		}

		[Fact]
		public void SelectPlanningDay_LoadsThatDaysCirculations()
		{
			CabinEnvironment cabin = CabinWithPackage();
			cabin.ClockNow = new DateTime(2026, 8, 16, 10, 0, 0); // domingo
			cabin.Load(null, Guid.Empty, string.Empty, cabin.PublishedPackage, Guid.Empty, "test", null, null);

			Assert.Equal(DayOfWeek.Sunday, cabin.DayProject?.PlanningDay);
			Assert.Single(cabin.DayProject!.Circulations);
			Assert.Equal("sun-1", cabin.DayProject.Circulations[0].Id);

			cabin.SelectPlanningDay(DayOfWeek.Saturday);
			Assert.True(cabin.HasPlanningDayOverride);
			Assert.Equal(DayOfWeek.Saturday, cabin.EffectivePlanningDay);
			Assert.Equal(DayOfWeek.Saturday, cabin.DayProject?.PlanningDay);
			Assert.Equal("sat-1", cabin.DayProject!.Circulations[0].Id);
		}

		[Fact]
		public void SelectCalendarDay_ClearsOverride()
		{
			CabinEnvironment cabin = CabinWithPackage();
			cabin.ClockNow = new DateTime(2026, 8, 16, 10, 0, 0);
			cabin.Load(null, Guid.Empty, string.Empty, cabin.PublishedPackage, Guid.Empty, "test", null, null);
			cabin.SelectPlanningDay(DayOfWeek.Monday);
			Assert.True(cabin.HasPlanningDayOverride);

			cabin.SelectPlanningDay(DayOfWeek.Sunday);
			Assert.False(cabin.HasPlanningDayOverride);
			Assert.Equal(DayOfWeek.Sunday, cabin.EffectivePlanningDay);
		}

		[Fact]
		public void RefreshDayProject_KeepsOverrideWhenClockAdvances()
		{
			CabinEnvironment cabin = CabinWithPackage();
			cabin.ClockNow = new DateTime(2026, 8, 16, 10, 0, 0);
			cabin.Load(null, Guid.Empty, string.Empty, cabin.PublishedPackage, Guid.Empty, "test", null, null);
			cabin.SelectPlanningDay(DayOfWeek.Saturday);

			cabin.ClockNow = new DateTime(2026, 8, 17, 0, 5, 0); // lunes
			cabin.RefreshDayProject();

			Assert.Equal(DayOfWeek.Saturday, cabin.EffectivePlanningDay);
			Assert.Equal("sat-1", cabin.DayProject!.Circulations[0].Id);
		}

		[Fact]
		public void SwitchingDay_ClearsCirculationNotInNewDay()
		{
			CabinEnvironment cabin = CabinWithPackage();
			cabin.ClockNow = new DateTime(2026, 8, 16, 10, 0, 0);
			cabin.Load(null, Guid.Empty, string.Empty, cabin.PublishedPackage, Guid.Empty, "test", null, null);
			cabin.Circulation = cabin.DayProject!.Circulations[0];
			Assert.Equal("sun-1", cabin.Circulation.Id);

			cabin.SelectPlanningDay(DayOfWeek.Saturday);
			Assert.Null(cabin.Circulation);
		}

		private static CabinEnvironment CabinWithPackage()
		{
			PublishedProjectPackage package = new PublishedProjectPackage
			{
				Name = "test",
				Days =
				{
					DayWithOne("sat-1", "4801", DayOfWeek.Saturday),
					DayWithOne("sun-1", "4802", DayOfWeek.Sunday),
					DayWithOne("mon-1", "4701", DayOfWeek.Monday)
				}
			};
			return new CabinEnvironment { PublishedPackage = package };
		}

		private static PublishedDayDto DayWithOne(string id, string service, DayOfWeek day)
		{
			return new PublishedDayDto
			{
				Day = day,
				Name = day.ToString(),
				Circulations =
				{
					new PublishedCirculationDto
					{
						Id = id,
						TechnicalId = id,
						DemandId = "d",
						ServiceNumber = service,
						DepartureSeconds = 8 * 3600,
						AsimilationId = string.Empty
					}
				}
			};
		}
	}
}
