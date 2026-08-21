namespace Sapphire2025Models.Aeneas
{
	/// <summary>
	/// Petición para registrar un nuevo valor de odómetro en un tren.
	/// </summary>
	public class OdometrySetRequestModel : BasicRequestModel
	{
		public Guid TrainId { get; set; }
		public long Odometer { get; set; }

		/// <summary>Cliente de origen (p. ej. <c>tourmaline</c>). Vacío = Zafiro web.</summary>
		public string? Client { get; set; }

		public OdometrySetRequestModel() : base()
		{
			TrainId = Guid.Empty;
			Odometer = 0;
		}

		public OdometrySetRequestModel(Guid token, Guid trainId, long odometer, string? client = null) : base(token)
		{
			TrainId = trainId;
			Odometer = odometer;
			Client = client;
		}
	}
}
