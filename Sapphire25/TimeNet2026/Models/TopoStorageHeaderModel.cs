using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TimeNet2026.Topo;

namespace TimeNet2026.Models
{
	public class TopoStorageHeaderModel
	{
		public Header header { get; set; } = new Header(); //Cabecera del TopoStorage
		public IEnumerable<Header> rautatie { get; set;  } = Enumerable.Empty<Header>(); //Colección de cabeceras de los rauta que contiene.
	}
}
