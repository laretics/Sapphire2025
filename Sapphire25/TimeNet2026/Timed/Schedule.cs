using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace TimeNet2026.Timed
{
	public class Schedule:Entity
	{
		private string mvarName;
		internal string name 
		{ 
			get => mvarName;
			set
			{
				if (null!=value && value.Length>0)
				{
					string[] palabras = value.Split(',');
					if(palabras.Length>0)
						nameCloud = palabras.Select(p => p.Trim()).ToArray();
					mvarName = palabras[0].Trim();
				}					
			}
		}
		internal string[] nameCloud { get; set; } //Conjunto de nombres alternativos que puede tener este mismo turno.
		internal string nameCloudString => string.Join(", ", nameCloud);

		internal int[] coordinates { get; set; } //Coordenadas X,Y en la presentación gráfica.
		internal string comment { get; set; }
		internal byte weekdayMask { get; set; } //Días de la semana en que está operativo este horario.
		internal string[] color { get; set; }
		string Entity.name { get => name; set => name = value; }
		string Entity.comment { get => comment; set => comment = value; }
		string[] Entity.color { get => this.color; set => this.color = value; }
		internal List<ScheduleItem> mcolItems;
		internal Schedule()
		{
			this.name = string.Empty;
			this.nameCloud = new string[0];
			this.comment = string.Empty;
			this.color = new string[2];
			this.coordinates = new int[2];
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
		private byte parseWeekDays(string? rhs)
		{
			if (null == rhs)
				return 0xff; //Cualquier día de la semana.
			else
			{
				string cadenaWeek = rhs.Trim().ToUpper();
				if (cadenaWeek.Equals("FFF"))
					return 1 | 64 | 128; //Sábados domingos y festivos.
				else if (cadenaWeek.Equals("LAB"))
					return 2 | 4 | 8 | 16 | 32; //Laborables.
				else
				{
					byte salida = 0;
					if (cadenaWeek.Contains('L')) salida |= 2;
					if (cadenaWeek.Contains('M')) salida |= 4;
					if (cadenaWeek.Contains('X')) salida |= 8;
					if (cadenaWeek.Contains('J')) salida |= 16;
					if (cadenaWeek.Contains('V')) salida |= 32;
					if (cadenaWeek.Contains('S')) salida |= 64;
					if (cadenaWeek.Contains('D')) salida |= 1;
					if (cadenaWeek.Contains('F')) salida |= 128;
					return salida;
				}
			}
		}

		internal void deserialize(XmlNode root, Plan parent)
		{
			if(null!=root.Attributes)
			{
				string? auxName = root.Attributes["name"]?.Value;
				string? auxComment = root.Attributes["comment"]?.Value;
				string? auxColor0 = root.Attributes["stcol"]?.Value;
				string? auxColor1 = root.Attributes["bgcol"]?.Value;
				string? auxWeek=root.Attributes["week"]?.Value;
				string? auxCoordinates=root.Attributes["ord"]?.Value;
				if(null!=auxName)
				{
					this.name = auxName;
					this.comment = auxComment ?? string.Empty;
					this.color = new string[2];
					this.color[0] = auxColor0 ?? "#000000";
					this.color[1] = auxColor1 ?? "#FFFFFF";
					this.weekdayMask = parseWeekDays(auxWeek);

					if(null!=auxCoordinates)
					{
						string[] coords = auxCoordinates.Split(',');
						if(coords.Length==2)
						{
							this.coordinates = new int[2];
							this.coordinates[0] = int.Parse(coords[0]);
							this.coordinates[1] = int.Parse(coords[1]);
						}
					}
					foreach (XmlNode node in root.ChildNodes)
					{
						ScheduleItem? nuevoItem = null;
						switch(node.Name)
						{
							case "depot":
								nuevoItem = parseDepot(node);
								break;
							case "train":
								nuevoItem = parseCirculation(node, parent);
								break;
							default:
								nuevoItem = null;
								break;
						}
						if(null!=nuevoItem)
							mcolItems.Add((ScheduleItem)nuevoItem);
					}
				}				
			}
		}
		private ScheduleItem? parseDepot(XmlNode node)
		{
			if (null != node.Attributes)
			{
				string? auxStart = node.Attributes["start"]?.Value;
				string? auxEnd = node.Attributes["end"]?.Value;
				string? auxActive = node.Attributes["active"]?.Value;
				return new ScheduleItem(new TimeLapse(auxStart, auxEnd), true);
			}
			return null;
		}
		private ScheduleItem? parseCirculation(XmlNode node, Plan parent)
		{
			if(null!=node.Attributes)
			{
				string? auxId = node.Attributes["id"]?.Value;
				string? auxActive = node.Attributes["active"]?.Value;
				if(null!=auxId && parent.mcolCirculations.ContainsKey(auxId))
				{
					Circulation circulation = parent.mcolCirculations[auxId];
					bool active = true;
					if(null!=auxActive)
						active = bool.Parse(auxActive);
					return new ScheduleItem(circulation, active);
				}
			}
			return null;
		}
	}
}
