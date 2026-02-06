using System.Text;
using Sapphire2026.Data.Models;

namespace Sapphire2025Server.Telegram.Semantics.Concepts
{
	/// <summary>
	/// Esto es una solicitud de un parte de avería en la que involucramos un síntoma y una o varias UT.
	/// </summary>
	internal class TrainIncidenceConcept: TrainConcept
	{
		private string? mvarSympthoms { get; set; } //Síntomas de la avería.
		public string? Sympthoms { get => mvarSympthoms; set => mvarSympthoms = value; }
		public bool mvarConfirmed; //El usuario ha validado esta información.
		public TrainIncidenceConcept(IConfiguration config):base("Train_Incidence_Report", "averia,incidencia,incidence,parte,hoja,aviso",config)
		{
			
		}
		public bool Validated
		{
			get => mvarConfirmed;
			set
			{
				if (HasAllTheInformation())
					mvarConfirmed = value;
			}
		}
		internal string VehicleDescriptor()
		{
			if (mcolTrains.Count > 0)
			{
				if (mcolTrains.Count > 1)
				{
					StringBuilder auxSalida = new StringBuilder();
					auxSalida.Append("Los trenes ");
					foreach (Train tren in mcolTrains)
					{
						if (tren != mcolTrains.First())
						{
							if (tren == mcolTrains.Last())
								auxSalida.Append(" y ");
							else
								auxSalida.Append(", ");
						}
						auxSalida.Append(tren.Name);
					}
					return auxSalida.ToString();
				}
				else
					return string.Format("El tren o vehículo {0}", mcolTrains[0].Name);
			}
			else
				return "el material móvil";
		}
		public TextResponse Confirmation()
		{
			if(mcolTrains.Count<1)
			{
				TextResponse auxPideUT = new TextResponse();
				auxPideUT.addText("¿De qué material móvil estamos hablando?");
				auxPideUT.addText("Necesito saber qué tren o vehículo está implicado");
				auxPideUT.addText("Me hacen falta datos; ¿A qué unidad tren o coche te estás refiriendo?");
				auxPideUT.addText("¿Qué tren es?");
				auxPideUT.addText("¿Qué material móvil se ha visto afectado?");
				auxPideUT.addText("Me falta saber qué tren es.");
				return auxPideUT;
			}
			else if(null==Sympthoms)
			{
				TextResponse auxPideSintomas = new TextResponse();
				auxPideSintomas.addText("¿Qué le ocurre a #ut ?");
				auxPideSintomas.addText("¿Qué síntomas tiene #ut ?");
				auxPideSintomas.addText("Por favor, describe la incidencia de #ut .");
				auxPideSintomas.addKey("ut", VehicleDescriptor());
				return auxPideSintomas;				
			}
			else if(!Validated)
			{
				TextResponse auxConfirmationMessage = new TextResponse();
				auxConfirmationMessage.addKey("ut", VehicleDescriptor());
				auxConfirmationMessage.addKey("sym", Sympthoms);
				auxConfirmationMessage.addText("Se está abriendo un parte de incidencia a #ut con la siguiente descripción: \"#sym\". ¿Es correcto?");
				auxConfirmationMessage.addText("#ut está a punto de abrir un parte de incidencia por #sym. ¿Procedo?");
				auxConfirmationMessage.addText("Si acepta en #ut, se abrirá un parte de avería con esta descripción: \"#sym\" ¿De acuerdo?");
				auxConfirmationMessage.addText("#ut va a acumular un parte de avería con esta descripción: \"#sym\" ¿Es correcto?");
				return auxConfirmationMessage;
			}
			else if(Validated)
			{
				TextResponse auxConfirmationMessage2 = new TextResponse();
				auxConfirmationMessage2.addKey("ut", VehicleDescriptor());
				auxConfirmationMessage2.addKey("sym", Sympthoms);
				auxConfirmationMessage2.addText("Abierto un parte de incidencia a #ut con la siguiente descripción: \"#sym\".");
				auxConfirmationMessage2.addText("#ut tiene abierto un parte de incidencia por #sym.");
				auxConfirmationMessage2.addText("#ut acumula un nuevo parte de incidencia con esta descripción: \"#sym\".");
				return auxConfirmationMessage2;
			}
			TextResponse auxErrorMessage = new TextResponse();
			auxErrorMessage.addText("Error interno en TrainIncidenceConcept");
			return auxErrorMessage;
		}
		public bool HasAllTheInformation()
		{
			return mcolTrains.Count > 0 && null != Sympthoms;
		}
	}
}
