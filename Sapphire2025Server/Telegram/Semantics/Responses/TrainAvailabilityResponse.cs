using Sapphire2025Models;

namespace Sapphire2025Server.Telegram.Semantics.Responses
{
	public class TrainAvailabilityResponse:Response
	{
		private Sapphire2025Models.Aeneas.TrainModel train;
		internal TrainAvailabilityResponse(Sapphire2025Models.Aeneas.TrainModel train)
		{
			this.train = train;
		}
		protected override byte maxResponses => 1;
		protected override string internalResponse(byte id)
		{
			switch (train.lastStatus)
			{
				case Sapphire2025Models.Common.TrainStatus.Available:
					return string.Format("El tren {0} está disponible y asignado a la circulación.", train.name);

				case Sapphire2025Models.Common.TrainStatus.DepotRequested:
					return string.Format("El tren {0} está disponible, pero lo pidió el taller para {1}", train.name, Common.timeStringTelegram(train.lastUpdateTime));
				case Common.TrainStatus.DepotAvailable:
					return string.Format("Este tren ({0} está a la espera de iniciar una intervención en taller desde {1} a disposición del personal de taller.",train.name, Common.timeStringTelegram(train.lastUpdateTime));
				case Common.TrainStatus.RequestToDiagnose:
					return string.Format("Aunque el tren {0} está circulando o a disposición de circulación, tiene un parte de avería abierto desde {1} y un experto debe diagnosticarlo.", train.name, Common.timeStringTelegram(train.lastUpdateTime));
				case Common.TrainStatus.RequestToRepair:
					return string.Format("{0} un usuario con nivel de experto ha decidido que el tren {1} no está para circular. Debe ser retirado de la circulación.",Common.timeStringTelegram(train.lastUpdateTime), train.name);
				case Common.TrainStatus.Repairing:
					return string.Format("El tren {0} no está disponible. Se encuentra en taller para reparación desde {1}.", train.name, Common.timeStringTelegram(train.lastUpdateTime));
				case Common.TrainStatus.Maintenance:
					return string.Format("El tren {0} no está disponible. Se encuentra apartado en taller por mantenimiento desde {1}.", train.name, Common.timeStringTelegram(train.lastUpdateTime));
				case Common.TrainStatus.StandStill:
					return string.Format("La unidad {0} está apartada de la circulación en régimen de Stand-Still.", train.name);
				case Common.TrainStatus.Disabled:
					return string.Format("El material con la matrícula {0} ha causado baja o está desguazado.", train.name);				
				default:
					return "Lo siento. No tengo información sobre este tren.";
			}
		}
	}
}
