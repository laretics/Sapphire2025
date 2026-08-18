using Sapphire2025Models.Diamond;

namespace Diamond.Tests.Controls
{
	public class ConsignaGenerationNumberingTests
	{
		[Fact]
		public void Format_YearAndSequence_YySlashXxx()
		{
			Assert.Equal("26/001", ConsignaGenerationNumbering.Format(2026, 1));
			Assert.Equal("26/015", ConsignaGenerationNumbering.Format(2026, 15));
			Assert.Equal("00/001", ConsignaGenerationNumbering.Format(2000, 1));
		}

		[Fact]
		public void ComputeNext_FirstOfYear_Is001()
		{
			int year;
			int seq;
			ConsignaGenerationNumbering.ComputeNext(0, 0, new DateTime(2026, 8, 17), out year, out seq);
			Assert.Equal(2026, year);
			Assert.Equal(1, seq);
			Assert.Equal("26/001", ConsignaGenerationNumbering.Format(year, seq));
		}

		[Fact]
		public void ComputeNext_SameYear_Increments()
		{
			int year;
			int seq;
			ConsignaGenerationNumbering.ComputeNext(2026, 3, new DateTime(2026, 12, 1), out year, out seq);
			Assert.Equal(2026, year);
			Assert.Equal(4, seq);
			Assert.Equal("26/004", ConsignaGenerationNumbering.Format(year, seq));
		}

		[Fact]
		public void ComputeNext_NewYear_ResetsTo001()
		{
			int year;
			int seq;
			ConsignaGenerationNumbering.ComputeNext(2026, 215, new DateTime(2027, 1, 1), out year, out seq);
			Assert.Equal(2027, year);
			Assert.Equal(1, seq);
			Assert.Equal("27/001", ConsignaGenerationNumbering.Format(year, seq));
			Assert.Equal(
				"Deroga Consigna Serie B nº 26/215 y anteriores",
				ConsignaGenerationNumbering.RepealText("26/215"));
		}

		[Fact]
		public void RepealText_EmptyWhenNoPrevious()
		{
			Assert.Equal(string.Empty, ConsignaGenerationNumbering.RepealText(null));
			Assert.Equal(string.Empty, ConsignaGenerationNumbering.RepealText(""));
		}

		[Fact]
		public void RepealText_MentionsPrevious()
		{
			Assert.Equal(
				"Deroga Consigna Serie B nº 26/001 y anteriores",
				ConsignaGenerationNumbering.RepealText("26/001"));
		}

		[Fact]
		public void Status_OpenUsesNextAndLastAsRepeal()
		{
			DiamondConsignaGenerationStatus status = new DiamondConsignaGenerationStatus
			{
				IsOpen = true,
				LastNumber = "26/001",
				PreviousNumber = "25/012",
				NextNumber = "26/002"
			};
			Assert.Equal("26/002", status.NumberForDocument);
			Assert.Equal("26/001", status.RepealNumber);
		}

		[Fact]
		public void Status_ClosedUsesLastAndPreviousAsRepeal()
		{
			DiamondConsignaGenerationStatus status = new DiamondConsignaGenerationStatus
			{
				IsOpen = false,
				LastNumber = "26/002",
				PreviousNumber = "26/001",
				NextNumber = "26/003"
			};
			Assert.Equal("26/002", status.NumberForDocument);
			Assert.Equal("26/001", status.RepealNumber);
		}
	}
}
