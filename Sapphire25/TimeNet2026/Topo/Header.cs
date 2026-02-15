using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Xml.Linq;
using TimeNet2026.Auxiliar;

namespace TimeNet2026.Topo
{
	/// <summary>
	/// Esto es una cabecera con información sobre un topo o sobre un rauta.
	/// </summary>
	public class Header
	{
		public string Name { get; set; }
		public string Description { get; set; }
		public string Comment { get; set; }
		public string License { get; set; }
		public string Author { get; set; }
		public DateTime FirstDate { get; set; }
		public DateTime LastDate { get; set; } //Esta será la fecha de entrada en vigor de este conjunto de planes.
		public string Version { get; set; }
		public string Bitmap { get; set; }
		public Guid Id { get; set; }
		public Guid ParentId { get; set; } //De momento sólo lo usan los rautatie

		public Header()
		{
			this.Name = string.Empty;
			this.Description = string.Empty;
			this.Comment = string.Empty;
			this.License = string.Empty;
			this.Author = string.Empty;
			this.Bitmap = string.Empty;
			this.Version = string.Empty;
		}
		internal void deserialize(XNode root)
		{
			this.Name = XUtil.StringParam(root, "name");
			this.Description = XUtil.StringParam(root, "description");
			this.Comment = XUtil.StringParam(root, "comment");
			this.License = XUtil.StringParam(root, "license");
			this.Author = XUtil.StringParam(root, "author");
			this.FirstDate = XUtil.DateTimeParam(root, "firstdate");
			this.LastDate = XUtil.DateTimeParam(root, "lastdate");
			this.Version = XUtil.StringParam(root, "version");
			this.Bitmap = XUtil.StringParam(root, "bitmap");
			this.ParentId = XUtil.GuidParam(root, "parentId");
			if (this.ParentId.Equals(Guid.Empty))
				this.ParentId = XUtil.GuidParam(root, "topoId");
			this.Id = XUtil.GuidParam(root, "id");
		}
		internal string XNode()
		{
			StringBuilder salida = new StringBuilder();
			salida.AppendFormat("<info id=\"{0}\"\n", this.Id);
			if (ParentId != Guid.Empty)
				salida.AppendFormat("topoId=\"{0}\"\n", ParentId);

			salida.AppendFormat("name=\"{0}\"\n description\"{1}\"\n comment\"{2}\"\n license\"{3}\"\n",
				Name,
				Description,
				Comment,
				License);
			salida.AppendFormat("author=\"{0}\"\n firstdate=\"{1}\"\n lastdate=\"{2}\"\n version=\"{3}\"\n bitmap=\"\"\n",
				Author,
				FirstDate,
				LastDate,
				Version,
				Bitmap
				);
			return salida.ToString();
		}
	}
}
