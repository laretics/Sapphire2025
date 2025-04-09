namespace Zafiro25.Models.Model
{
	public class TrainModel
	{
		public Guid id { get; set; }

		public string name { get; set; }
		public string? nameCloud { get; set; } //Colección de cadenas para poder buscar esta unidad.

		public List<StatusChangeModel> statusChanges { get; private set; }

		public TrainModel() 
		{
			statusChanges = new List<StatusChangeModel>();
			name = string.Empty;

		}
		public Common.TrainStatus lastStatus
		{
			get
			{
				if (statusChanges.Count>0)
				{
					StatusChangeModel auxLast = statusChanges.First();
					return auxLast.status;
				}
				return Common.TrainStatus.Unknown;
			}
		}
		public DateTime lastUpdateTime
		{
			get
			{
				if(statusChanges.Count>0)
				{
					StatusChangeModel auxLast = statusChanges.First();
					return auxLast.timeStamp;
				}
				return DateTime.Now;
			}
		}

		public string lastUserInfo
		{
			get
			{
				if(statusChanges.Count>0)
				{
					StatusChangeModel auxLast = statusChanges.First();
					if(null!= auxLast)
					{
						return auxLast.userId;
					}
				}
				return "-";
			}
		}
	}
}
