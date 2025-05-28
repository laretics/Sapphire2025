using Sapphire2025Models;
using System.Text;

namespace Sapphire2025Server.Telegram.Semantics.Responses
{
	/// <summary>
	/// Respuesta para el usuario sobre los trenes disponibles que hay en la flota.
	/// </summary>
	internal class TrainsAvailableResponse:Response
	{
		private List<Sapphire2025Models.Aeneas.TrainModel> trains;
		internal TrainsAvailableResponse(List<Sapphire2025Models.Aeneas.TrainModel> trains)
		{
			this.trains = trains;
		}
		protected override byte maxResponses => 3;
		protected override string internalResponse(byte id)
		{
			StringBuilder sb = new StringBuilder();
			switch(id)
			{
				case 0:
					sb.AppendFormat("Estos son los {0} trenes disponibles en la flota:\n",trains.Count());
					break;
				case 1:
					sb.AppendFormat("Actualmente se encuentran disponibles los siguientes {0} trenes:\n",trains.Count());
					break;
				default:
					sb.AppendFormat("La lista de los {0} trenes disponibles es la siguiente:\n",trains.Count());
					break;
			}
			foreach (var train in trains)
			{
				sb.Append("-");
				sb.Append(train.name);
				sb.Append(" ");
				switch(train.lastStatus)
				{
					case Common.TrainStatus.Available:
						sb.Append("en circulación");
						break;
					case Common.TrainStatus.DepotRequested: //Pedido para mantenimiento
						sb.AppendFormat("pedido para preventivo desde {0}",Common.timeStringTelegram(train.lastUpdateTime));
						break;
					case  Common.TrainStatus.RequestToDiagnose: //Pendiente de diagnóstico
						sb.AppendFormat("pendiente de diagnóstico desde {0}",Common.timeStringTelegram(train.lastUpdateTime));
						break;
					case Common.TrainStatus.RequestToRepair:
						sb.AppendFormat("a retirar por avería desde {0}",Common.timeStringTelegram(train.lastUpdateTime));
						break;
					default:
						sb.Append("No disponible por error");
						break;
				}
				sb.Append("\n");
			}
			return sb.ToString();
		}
	}
}
