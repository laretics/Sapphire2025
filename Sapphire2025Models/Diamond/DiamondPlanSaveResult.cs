namespace Sapphire2025Models.Diamond
{
	public class DiamondPlanSaveResult
	{
		public bool Success { get; set; }

		public string Message { get; set; } = string.Empty;

		public bool AlreadyExists { get; set; }

		public DiamondPlanHeaderModel? Header { get; set; }
	}
}
