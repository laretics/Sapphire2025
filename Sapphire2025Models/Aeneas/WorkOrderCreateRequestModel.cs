using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sapphire2025Models.Aeneas
{
	public class WorkOrderCreateRequestModel:BasicRequestModel
	{
		public Guid WorkType { get; set; }
		public Guid? DestinationObjectId { get; set; }
		public Guid? TrainId { get; set; }
	}
}
