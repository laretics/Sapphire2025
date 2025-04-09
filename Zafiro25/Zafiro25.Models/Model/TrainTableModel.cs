using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Zafiro25.Models.Model
{
	public class TrainTableModel
	{
		public List<TrainModel> trains { get; private set; }
		public Dictionary<string,UserModel> users { get; private set; }
		public TrainTableModel()
		{
			trains = new List<TrainModel>();
			users = new Dictionary<string, UserModel>();
		}


	}
}
