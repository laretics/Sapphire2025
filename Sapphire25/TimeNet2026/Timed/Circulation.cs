using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using TimeNet2026.Auxiliar;
using TimeNet2026.Storage;
using TimeNet2026.Topo;

namespace TimeNet2026.Timed
{
	public class Circulation : Entity, IComparable<Circulation>
	{
		private string mvarName;
		private string mvarComment;
		private string[] mvarColor;
		public Circulation()
		{
			mvarColor = new string[2];
			mvarName = "??";
			mvarComment = string.Empty;
		}
		public Circulation(XmlNode root, TopoStorage storage):this()
		{
			deserialize(root, storage);
		}


		public Asimilation? asimilation { get; set; }
		public TimeSpan departure { get; set; }
		public TimeSpan arrival
		{
			get
			{
				if (null==asimilation || null == asimilation.duration)
					return departure;
				else
					return departure.Add((TimeSpan)asimilation.duration);
			}
		}
		internal TimeSpan calculateDelay(long currentPk, TimeSpan currentTime)
		{
			if (null == asimilation) return new TimeSpan(0);
			return asimilation.calculateDelay(currentPk, currentTime.Subtract(departure));
		}
		public int CompareTo(Circulation other) { return cacheDeparture.CompareTo(other.cacheDeparture); }
		internal bool contained(TimeSpan begin, TimeSpan end) { return !((departure > end) || (arrival < begin)); }
		internal TimeSpan departureFrom(Station station)
		{
			TimeSpan auxValue = asimilation.departureFrom(station);
			if (auxValue == TimeSpan.MaxValue) return auxValue;
			else
				return departure.Add(auxValue);
		}
		internal TimeSpan cacheDeparture { get; set; } //Valor usado para ordenar los trenes por hora de salida.
		public string name { get => mvarName; set => mvarName = value; }
		public string comment { get => mvarComment; set => mvarComment = value; }
		public string[] color { get => mvarColor; set => mvarColor=value; }

		internal void deserialize(XmlNode root, TopoStorage storage)
		{
			mvarName = XMLUtil.StringParam(root, "id");
			string auxAsimilaId = XMLUtil.StringParam(root, "asm");
			departure = XMLUtil.TimeSpanParam(root, "dep");
			if (storage.mcolAsimilations.ContainsKey(auxAsimilaId))
				asimilation = storage.mcolAsimilations[auxAsimilaId];
			color[0] = XMLUtil.StringParam(root, "col", "black");
            color[1] = XMLUtil.StringParam(root, "col", "white");
        }
		//internal View GetView(View destination, ViewGroup parent, bool isNight)
		//{
		//	//Mapeando controles
		//	LinearLayout auxContainer = destination.FindViewById<LinearLayout>(Resource.Id.lnCirculationRow);
		//	TextView auxTBId = destination.FindViewById<TextView>(Resource.Id.tbId);
		//	TextView auxTBDeparture = destination.FindViewById<TextView>(Resource.Id.tbDeparture);
		//	TextView auxTBPath = destination.FindViewById<TextView>(Resource.Id.tbPath);

		//	//Escribiendo información
		//	Android.Graphics.Color auxColor = asimilation.color[isNight ? 1 : 0];
		//	auxTBId.SetTextColor(auxColor);
		//	auxTBPath.SetTextColor(auxColor);

		//	auxTBId.Text = this.name;
		//	auxTBDeparture.Text = string.Format("{0:HH:mm}", DateTime.Today.Add(this.departure));
		//	auxTBPath.Text = this.asimilation.ToString();
		//	return destination;
		//}

	}
}
