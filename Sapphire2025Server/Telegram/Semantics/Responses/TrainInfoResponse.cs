using Microsoft.OpenApi.Any;
using Sapphire2025Models;
using Sapphire2025Models.Aeneas;
using Sapphire2025Models.Authentication;

namespace Sapphire2025Server.Telegram.Semantics.Responses
{
	public class TrainInfoResponse:Response
	{
		public TrainModel Model { get; set; }
		public UserModel? User { get; set; }
		public Guid ownerId { get; set; } //Referencia al usuario para poder dirigirse a él de tú.
		public TrainInfoResponse(TrainModel model, UserModel? user, Guid ownerId)
		{
			this.Model = model;
			this.User = user;
			this.ownerId = ownerId;
		}
		protected override string internalResponse(byte id)
		{
			if(null==User)
			{
				switch (id)
				{
					case 0:
						return string.Format("El tren {0} {1} desde {2}",
							Model.name,
							Common.TrainStatusTelegramString[(byte)Model.lastStatus],
							Common.timeStringTelegram(Model.lastUpdateTime)							
							);
					default:
						return string.Format("La UT {0} {1} El último cambio de estado se produjo {2}",
							Model.name,
							Common.TrainStatusTelegramString[(byte)Model.lastStatus],
							Common.timeStringTelegram(Model.lastUpdateTime)
							);
				}
			}
			else
			{
				if(User.guid==ownerId)
				{
					switch (id)
					{
						case 0:
							return string.Format("Tú cambiaste {2} el tren {0} {1} desde {2}",
								Model.name,
								Common.TrainStatusTelegramString[(byte)Model.lastStatus],
								Common.timeStringTelegram(Model.lastUpdateTime)
								);
						default:
							return string.Format("La UT {0} {1} desde que tú lo modificases {2}",
								Model.name,
								Common.TrainStatusTelegramString[(byte)Model.lastStatus],
								Common.timeStringTelegram(Model.lastUpdateTime)
								);
					}
				}
				else
				{
					switch (id)
					{
						case 0:
							return string.Format("{3} cambió {2} el tren {0} {1} desde {2}",
								Model.name,
								Common.TrainStatusTelegramString[(byte)Model.lastStatus],
								Common.timeStringTelegram(Model.lastUpdateTime),
								User.Name
								);
						default:
							return string.Format("La UT {0} {1} desde que {3}} lo modificó {2}",
								Model.name,
								Common.TrainStatusTelegramString[(byte)Model.lastStatus],
								Common.timeStringTelegram(Model.lastUpdateTime),
								User.Name
								);
					}
				}
			}
		}		
		protected override byte maxResponses => 2;	
	}
}
