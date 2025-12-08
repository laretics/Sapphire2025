using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TimeNet2026.Storage
{
	internal class OnyxField
	{
		internal OnyxField(string name, string type)
		{
			this.Name = name;
			this.Type = type.ToUpper();
			this.NotNull = true;
			this.IsKey = false;
			this.AutoCount = false;
		}
		internal OnyxField(string name, string type, bool notNull):this(name,type)
		{
			this.NotNull = notNull;
		}
		internal OnyxField(string name, string type, bool notNull, bool key, bool autoCount)
			:this(name,type,notNull)
		{
			this.IsKey = key;
			this.AutoCount = autoCount;
		}
		internal string Name { get; set; }
		internal string Type { get; set; }
		internal bool NotNull { get; set; }
		internal bool IsKey { get; set; }
		internal bool AutoCount { get; set; }
		internal string Descriptor
		{
			get
			{
				return string.Format("{0} {1}{2}{3}{4}",
					Name,
					Type,
					IsKey ? " PRIMARY KEY" : "",
					(IsKey&&AutoCount) ? " AUTOINCREMENT":"",
					NotNull ? " NOT NULL" : "");
			}
		}
	}
}
