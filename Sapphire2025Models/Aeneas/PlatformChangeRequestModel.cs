namespace Sapphire2025Models.Aeneas
{
	/// <summary>
	/// Petición para asignar o liberar la vía/andén de un tren.
	/// </summary>
	public class PlatformChangeRequestModel : BasicRequestModel
	{
		public Guid TrainId { get; set; }
		/// <summary>-1 = sin vía asignada.</summary>
		public int PlatformId { get; set; }

		public PlatformChangeRequestModel() : base()
		{
			TrainId = Guid.Empty;
			PlatformId = -1;
		}

		public PlatformChangeRequestModel(Guid token, Guid trainId, int platformId) : base(token)
		{
			TrainId = trainId;
			PlatformId = platformId;
		}
	}
}
