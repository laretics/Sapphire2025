using System;
using System.Globalization;
using System.IO;
using System.Xml.Linq;

namespace Diamond.Rauta
{
	/// <summary>
	/// Carga el formato rautatie (p. ej. rautasfm227.xml).
	/// No serializa schedules de agentes (solo circulations/blocks).
	/// </summary>
	public static class RautaXmlSerializer
	{
		public static RautaDocument Load(string path)
		{
			if (path is null)
			{
				throw new ArgumentNullException(nameof(path));
			}

			using (FileStream stream = File.OpenRead(path))
			{
				return Load(stream);
			}
		}

		public static RautaDocument Load(Stream stream)
		{
			if (stream is null)
			{
				throw new ArgumentNullException(nameof(stream));
			}

			XDocument document = XDocument.Load(stream);
			XElement? root = document.Root;
			if (root is null || root.Name.LocalName != "rautatie")
			{
				throw new InvalidDataException("El XML de rauta debe tener raíz <rautatie>.");
			}

			RautaDocument doc = new RautaDocument();
			XElement? info = root.Element("info");
			if (info is not null)
			{
				doc.Info.Id = Attr(info, "id");
				doc.Info.TopoId = Attr(info, "topoId");
				doc.Info.Name = Attr(info, "name");
				doc.Info.Description = Attr(info, "description");
				doc.Info.Comment = Attr(info, "comment");
				doc.Info.Version = Attr(info, "version");
				doc.Info.Author = Attr(info, "author");
			}

			XElement? plans = root.Element("plans");
			if (plans is not null)
			{
				foreach (XElement planEl in plans.Elements("plan"))
				{
					doc.AddPlan(ReadPlan(planEl));
				}
			}

			return doc;
		}

		private static RautaPlan ReadPlan(XElement planEl)
		{
			RautaPlan plan = new RautaPlan();
			plan.Id = Attr(planEl, "id");
			plan.Name = Attr(planEl, "name");
			plan.Comment = Attr(planEl, "comment");

			XElement? circulations = planEl.Element("circulations");
			if (circulations is null)
			{
				return plan;
			}

			foreach (XElement blockEl in circulations.Elements("block"))
			{
				RautaBlock block = new RautaBlock();
				block.AsimilationId = Attr(blockEl, "asm");
				block.Freq = Attr(blockEl, "freq");
				block.Pattern = Attr(blockEl, "pattern");

				foreach (XElement cirEl in blockEl.Elements("cir"))
				{
					RautaCirculation cir = ReadCir(cirEl, block.AsimilationId, block.Freq);
					block.AddCirculation(cir);
				}

				plan.AddBlock(block);
			}

			// Circulaciones sueltas bajo circulations (si las hubiera)
			foreach (XElement cirEl in circulations.Elements("cir"))
			{
				RautaBlock orphan = new RautaBlock();
				RautaCirculation cir = ReadCir(cirEl, string.Empty, string.Empty);
				orphan.AsimilationId = cir.AsimilationId ?? string.Empty;
				orphan.Freq = cir.Freq ?? string.Empty;
				orphan.AddCirculation(cir);
				plan.AddBlock(orphan);
			}

			return plan;
		}

		private static RautaCirculation ReadCir(XElement cirEl, string defaultAsm, string defaultFreq)
		{
			RautaCirculation cir = new RautaCirculation();
			cir.Id = Attr(cirEl, "id");
			string dep = Attr(cirEl, "dep");
			TimeSpan ts;
			if (TimeSpan.TryParseExact(dep, new[] { @"hh\:mm\:ss", @"h\:mm\:ss", @"hh\:mm", @"h\:mm" }, CultureInfo.InvariantCulture, out ts)
				|| TimeSpan.TryParse(dep, CultureInfo.InvariantCulture, out ts))
			{
				cir.Departure = ts;
			}

			string asm = Attr(cirEl, "asm");
			cir.AsimilationId = asm.Length > 0 ? asm : (defaultAsm.Length > 0 ? defaultAsm : null);
			string freq = Attr(cirEl, "freq");
			cir.Freq = freq.Length > 0 ? freq : (defaultFreq.Length > 0 ? defaultFreq : null);
			return cir;
		}

		private static string Attr(XElement el, string name)
		{
			XAttribute? a = el.Attribute(name);
			return a is null ? string.Empty : a.Value;
		}
	}
}
