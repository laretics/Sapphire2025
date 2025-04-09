using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Zafiro25.Models;
using Zafiro25.Models.GMao;

namespace Zafiro25.Models.Model
{
	/// <summary>
	/// Representa el contenido actual de trenes en cada estado
	/// </summary>
	public class FlowModel
	{
		public Dictionary<Common.TrainStatus,List<TrainModel>> colTrains;
		public List<TrainModel> listTrains;
		public Common.TrainStatus currentStatus { get; set; } = Common.TrainStatus.NoneSelected;

		public int cardinal(Common.TrainStatus status)
		{
			if(colTrains.ContainsKey(status)) return colTrains[status].Count;
			return 0;
		}

		public FlowModel()
		{
			colTrains = new Dictionary<Common.TrainStatus, List<TrainModel>>();
			listTrains = new List<TrainModel>();
		}

		public void add(TrainModel rhs)
		{
			if(!colTrains.ContainsKey(rhs.lastStatus))
			{
				colTrains.Add(rhs.lastStatus, new List<TrainModel>());
			}
			colTrains[rhs.lastStatus].Add(rhs);
			listTrains.Add(rhs);
		}
		
	}
}
