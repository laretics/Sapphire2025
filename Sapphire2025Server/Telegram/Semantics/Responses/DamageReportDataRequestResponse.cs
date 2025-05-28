using Sapphire2025Models.Aeneas;

namespace Sapphire2025Server.Telegram.Semantics.Responses
{
	public class DamageReportDataRequestResponse : Response
	{
		public DamageReportDataRequestResponse(Concepts.TrainConcept train)
		{
			this.train = train;
		}
		public Concepts.TrainConcept train { get; private set; }
		protected override byte maxResponses => 3;
		protected override string internalResponse(byte id)
		{
			switch(id)
			{
				case 0:
					return string.Format ("Por favor, introduce la información sobre la avería o incidencia que has detectado en la unidad {0}",train.name);
				case 1:
					return string.Format("Acabas de abrir un parte de averías a la unidad {0}. Por favor, indica los síntomas de la incidencia",train.name);
				default:
					return string.Format("Indica lo que le ocurre a la unidad {0}.", train.name);
			}
		}
	}
}
