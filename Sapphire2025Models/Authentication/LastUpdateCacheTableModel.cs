using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sapphire2025Models.Authentication
{
	public class LastUpdateCacheTableModel:BasicRequestModel
	{
		public Common.CacheTableKey Key { get; set; }

		public LastUpdateCacheTableModel()
		{
			Key = Common.CacheTableKey.None;
		}
	}
}
