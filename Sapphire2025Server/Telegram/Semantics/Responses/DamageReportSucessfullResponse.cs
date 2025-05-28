using Sapphire2025Server.Models;

namespace Sapphire2025Server.Telegram.Semantics.Responses
{
	public class DamageReportSucessfullResponse:Response
	{
		internal Train Tren {  get; private set; }
		public DamageReportSucessfullResponse(Train Tren)
		{
			this.Tren = Tren;
		}
		protected override byte maxResponses => 4;
		protected override string internalResponse(byte id)
		{
			switch(id)
			{
				case 0:
					return string.Format("El parte de avería para la UT {0} se ha enviado correctamente.", Tren.Name);
				case 1:
					return string.Format("Acabas de registrar un parte de avería para el tren {0}.", Tren.Name);
				case 2:
					return string.Format("El sistema acaba de registrar un nuevo parte de avería para la UT {0}", Tren.Name);
				default:
					return string.Format("El tren {0} tiene ahora un nuevo parte de avería registrado.", Tren.Name);
			}			
		}
	}
}
