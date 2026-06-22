using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sapphire2025Models.Aeneas
{
	public class StatusChangeRequestModel
	{
		public Guid trainId{ get; set; }
		public DateTime oldestRecord { get; set; }
		public StatusChangeRequestModel(Guid trainId,  DateTime oldestRecord)
		{
			  this.trainId = trainId;	
			  this.oldestRecord = oldestRecord;
		}
		public StatusChangeRequestModel() { }
	}
}
