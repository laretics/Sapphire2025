using System.Text;
using Sapphire2026.Data.Models;

namespace Sapphire2026Telegram.Semantics.Concepts
{
	/// <summary>
	/// Esto es una solicitud de un parte de avería en la que involucramos un síntoma y una o varias UT.
	/// </summary>
	internal class TrainNoteConcept: TrainConcept
	{
		private string? mvarSympthoms { get; set; } //Síntomas de la avería.
		public string? Sympthoms { get => mvarSympthoms; set => mvarSympthoms = value; }
		public bool mvarConfirmed; //El usuario ha validado esta información.
		internal bool Incidence { get; private set; }
		public TrainNoteConcept(IConfiguration config,IServiceProvider provider, bool incidence):base(config,provider)
		{
			Incidence= incidence;
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
		internal async override Task AddText(string text)
		{
			if (mcolTrains.Count < 1)
				await base.AddText(text);
			else if (string.IsNullOrEmpty(mvarSympthoms))
				Sympthoms = text;
			else
			{
				mvarConfirmed = Sapphire2025Models.I18n.TelegramI18n.IsAffirmative(text);
				//Procesa el parte

			}
		}
	
		public TextResponse Confirmation(Sapphire2025Models.I18n.UiLocale locale)
		{
			if(mcolTrains.Count<1)
			{
				TextResponse auxPideUT = new TextResponse();
				auxPideUT.addCatalog("tg.ask.train.1", "tg.ask.train.2", "tg.ask.train.3", "tg.ask.train.4", "tg.ask.train.5", "tg.ask.train.6");
				return auxPideUT;
			}
			else if(null==Sympthoms)
			{
				TextResponse auxPideSintomas = new TextResponse();
				auxPideSintomas.addCatalog("tg.ask.sym.1", "tg.ask.sym.2", "tg.ask.sym.3");
				auxPideSintomas.addKey("utf", TrainVerbose(locale, false,true));
				auxPideSintomas.addKey("ut", TrainVerbose(locale, true, false));

				return auxPideSintomas;				
			}
			else if(!Validated)
			{
				TextResponse auxConfirmationMessage = new TextResponse();
				auxConfirmationMessage.addKey("ut", TrainVerbose(locale, false,true));
				auxConfirmationMessage.addKey("utf", TrainVerbose(locale, true, false));
				auxConfirmationMessage.addKey("sym", Sympthoms ?? string.Empty);
				if(Incidence)
					auxConfirmationMessage.addCatalog("tg.confirm.inc.1", "tg.confirm.inc.2", "tg.confirm.inc.3", "tg.confirm.inc.4");
				else
					auxConfirmationMessage.addCatalog("tg.confirm.note.1", "tg.confirm.note.2", "tg.confirm.note.3", "tg.confirm.note.4");
				return auxConfirmationMessage;
			}
			else if(Validated)
			{
				TextResponse auxConfirmationMessage2 = new TextResponse();
				auxConfirmationMessage2.addKey("utf", TrainVerbose(locale, true,true));
				auxConfirmationMessage2.addKey("ut", TrainVerbose(locale, false, false));
				auxConfirmationMessage2.addKey("sym", Sympthoms ?? string.Empty);
				auxConfirmationMessage2.addCatalog("tg.done.inc.1", "tg.done.inc.2", "tg.done.inc.3");
				return auxConfirmationMessage2;
			}
			TextResponse auxErrorMessage = new TextResponse();
			auxErrorMessage.addCatalog("tg.err.internal");
			return auxErrorMessage;
		}
		public bool HasAllTheInformation()
		{
			return mcolTrains.Count > 0 && null != Sympthoms;
		}
	}
}
