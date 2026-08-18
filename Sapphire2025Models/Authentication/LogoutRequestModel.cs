namespace Sapphire2025Models.Authentication
{
	/// <summary>Cierre de sesión; campos extra para origen Tourmaline.</summary>
	public class LogoutRequestModel : BasicRequestModel
	{
		public LogoutRequestModel()
		{
		}

		public LogoutRequestModel(Guid token) : base(token)
		{
		}

		public string? Client { get; set; }

		public string? TrainId { get; set; }

		public string? TrainName { get; set; }
	}
}
