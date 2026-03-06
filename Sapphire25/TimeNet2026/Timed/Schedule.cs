using Microsoft.EntityFrameworkCore.Storage.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using TimeNet2026.Auxiliar;

namespace TimeNet2026.Timed
{
	public class Schedule:Entity
	{
		private string mvarName;
		internal string Name 
		{ 
			get => mvarName;
			set
			{
				if (null!=value && value.Length>0)
				{
					string[] palabras = value.Split(',');
					if(palabras.Length>0)
						NameCloud = palabras.Select(p => p.Trim()).ToArray();
					mvarName = palabras[0].Trim();
				}					
			}
		}
		internal string[] NameCloud { get; set; } //Conjunto de nombres alternativos que puede tener este mismo turno.
		internal string NameCloudString => string.Join(", ", NameCloud);

		internal int[] Coordinates { get; set; } //Coordenadas X,Y en la presentación gráfica.
		internal string Comment { get; set; }
		internal byte weekdayMask { get; set; } //Días de la semana en que está operativo este horario.
		internal string[] Color { get; set; }
		string Entity.name { get => Name; set => Name = value; }
		string Entity.comment { get => Comment; set => Comment = value; }
		string[] Entity.color { get => this.Color; set => this.Color = value; }
		internal List<ScheduleItem> mcolItems;
		internal Schedule()
		{
			Name = string.Empty;
			NameCloud = new string[0];
			Comment = string.Empty;
			Color = new string[2];
			Coordinates = new int[2];
			mcolItems = new List<ScheduleItem>();
		}
		internal bool containsCirculation(Circulation rhs)
		{
			foreach (ScheduleItem item in mcolItems)
			{
				if (item.circulation == rhs) return true;
			}
			return false;
		}
	}
}
