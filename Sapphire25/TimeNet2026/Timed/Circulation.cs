using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using TimeNet2026.Topo;

namespace TimeNet2026.Timed
{
	internal class Circulation : Entity, IComparable<Circulation>
	{
		private string mvarName;
		private string mvarComment;
		private string[] mvarColor = new string[2];
		internal Asimilation? asimilation { get; set; }
		internal TimeSpan departure { get; set; }
		internal TimeSpan arrival
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

		internal void deserialize(XmlNode root, OnyxStorage storage)
		{
			mvarName = root.Attributes["id"].Value;
			asimilation = storage.mcolAsimilations[root.Attributes["asm"].Value];
			departure = TimeSpan.Parse(root.Attributes["dep"].Value);
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
