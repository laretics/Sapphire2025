using System.Text;
using ZafiroGmao.Data.Models;

namespace ZafiroGmao.Telegram
{
    /// <summary>
    /// Helper para gestionar todas las comunicaciones posibles entre GMao y Telegram
    /// </summary>
    public class GMaoTelegram
    {
        public GMaoTelegram(BotSoul service)
        {
            this.Service = service;
        }
        public BotSoul Service { get; private set; }


        public async Task AnnounceTrainOperation(
                                        SFMUser? user,
                                        Train? train,
                                        string? comment,            
                                        Common.OperationType rhs)
        {
            if (null == user) return; //No vamos a poner mensajes por acciones de usuarios nulos.
            if (null == train) return; //De la misma forma, el tren debe existir.
            StringBuilder auxMessage = new StringBuilder();
            switch (rhs)
            {
                case Data.Models.Common.OperationType.EndMaintenance:
                case Data.Models.Common.OperationType.EndCorrective:
                    auxMessage.AppendFormat("{0} retorna la UT {1} que estaba en talleres a la circulación.", user.UserName, train.Name);
                    break;
                case Data.Models.Common.OperationType.BeginCorrective:
					auxMessage.AppendFormat("{0} retira la UT {1} a talleres por correctivo.", user.UserName, train.Name);				
                    break;
                case Data.Models.Common.OperationType.BeginMaintenance:
					auxMessage.AppendFormat("{0} retira la UT {1} a talleres para mantenimiento.", user.UserName, train.Name);
                    break;
                case Data.Models.Common.OperationType.DiagnoseToFault:
					auxMessage.AppendFormat("{0} decide que la UT {1} debe ser retirada por avería.", user.UserName, train.Name);
                    break;
				case Common.OperationType.DiagnoseToAvailable:
					auxMessage.AppendFormat("{0} decide que la UT {1} puede seguir prestando servicio.", user.UserName, train.Name);
					break;
				case Common.OperationType.DepotRequest:
					auxMessage.AppendFormat("{0} solicita la UT {1} para mantenimiento.", user.UserName, train.Name);
					break;
                case Common.OperationType.DepotRequestAccept:
					auxMessage.AppendFormat("{0} retira la UT {1} para mantenimiento.", user.UserName, train.Name);
					break;
                case Common.OperationType.MaintenanceRescue:
					auxMessage.AppendFormat("{0} rescata la UT {1} retirada para mantenimiento volviéndola a poner en servicio.", user.UserName, train.Name);
					break;
                case Common.OperationType.DiferMaintenance:
					auxMessage.AppendFormat("{0} saca la UT {1} de taller sin haber concluído las operaciones mantenimiento.", user.UserName, train.Name);
					break;
				case Common.OperationType.CorrectiveRequest:
					auxMessage.AppendFormat("{0} solicita la UT {1} para recibir mantenimiento.", user.UserName, train.Name);
					break;
			}

            if(auxMessage.Length > 0)
            {
				if (null != comment && comment.Length > 0)
				{
                    auxMessage.AppendFormat(" Notas: \"{0}\"", comment);
				}
				await Service.sendToSubscriptors(auxMessage.ToString());
            }
        }

    }
}
