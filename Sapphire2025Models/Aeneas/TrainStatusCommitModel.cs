using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sapphire2025Models.Aeneas
{
	public class TrainStatusCommitModel:BasicRequestModel
	{
		public TrainStatusCommitModel(Guid token, Guid trainId, Common.OperationType operation):base(token)
		{
			this.trainId = trainId;
			this.operation = operation;
		}
		public TrainStatusCommitModel():base()
		{
			this.trainId = Guid.Empty;
			this.operation = Common.OperationType.Unknown;
		}
		public Guid trainId { get; set; } //Referencia al tren sobre el que se ejecuta esta transacción
		public Common.OperationType operation { get; set; } //Nuevo estado del tren

	}
}
