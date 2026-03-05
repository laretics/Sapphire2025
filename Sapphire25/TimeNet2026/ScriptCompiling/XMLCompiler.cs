using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using TimeNet2026.Auxiliar;
using TimeNet2026.Timed;
using TimeNet2026.Topo;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory.Database;

namespace TimeNet2026.ScriptCompiling
{
	/// <summary>
	/// Esta clase sirve para deserializar la estructura TimeNet sin ensuciar la información de la jerarquía con funciones raras.
	/// </summary>
	internal class XMLCompiler
	{
		public XMLCompiler() { Result = new XMLCompileResult(); }

		public XMLCompileResult Result { get; private set; }

		public TopoStorage? CompileTopoStorage(XNode root)
		{
			Result = new XMLCompileResult();
			Result.Success = true; //En principio la compilación será correcta.
			TopoStorage salida = new TopoStorage();
			if (root is XElement element)
			{
				foreach (XElement hijo in element.Elements())
				{
					switch (hijo.Name.LocalName)
					{
						case "info": //Cabecera de información.
							salida.Header = CompileHeader(hijo);
							break;
						case "topo": //Ejes
							if (!CompileAxisCollection(hijo, salida))
								return null;
							break;
						case "asimilation": //Asimilaciones
							if(!CompileAsimilationCollection(hijo,salida))
								return null;
							break;
					}
				}
				return salida;
			}
			return null;
		}


		internal Header CompileHeader(XNode root)
		{
			Header salida = new Header();
			salida.Name = StringParam(root, "name");
			salida.Description = StringParam(root, "description");
			salida.Comment = StringParam(root, "comment");
			salida.License = StringParam(root, "license");
			salida.Author = StringParam(root, "author");
			salida.FirstDate = DateTimeParam(root, "firstdate");
			salida.LastDate = DateTimeParam(root, "lastdate");
			salida.Version = StringParam(root, "version");
			salida.Bitmap = StringParam(root, "bitmap");
			salida.ParentId = GuidParam(root, "parentId");
			if (salida.ParentId.Equals(Guid.Empty))
				salida.ParentId = GuidParam(root, "topoId");
			salida.Id = GuidParam(root, "id");
			return salida;
		}
		#region Topo
		internal Axis CompileAxis(XNode root)
		{
			Axis salida = new Axis();
			//Cabecera
			salida.id = StringParam(root, "id");
			salida.Name = StringParam(root, "name");
			salida.Comment = StringParam(root, "comment");
			salida.MaxSpeed = IntParam(root, "vmax");
			salida.mvarColor[0] = StringParam(root, "color");
			salida.mvarColor[1] = StringParam(root, "darkcolor");
			if (root is XElement element)
			{
				foreach (XElement child in element.Elements())
				{
					switch (child.Name.LocalName)
					{
						case "poly":
							CompileAxisPoly(child, salida);
							break;
						case "limit":
							CompileAxisLimits(child, salida);
							break;
						case "signal":
							CompileAxisSignals(child, salida);
							break;
					}
				}
			}
			return salida;
		}
		private void CompileAxisPoly (XElement root, Axis parent)
		{
			if(null==parent.Topology) parent.Topology = new TopoAxis();

			if (root is XElement element)
			{
				foreach (XElement child in element.Elements())
				{
					if ("point" == child.Name.LocalName)
					{
						GeoLocation auxLocation = GeoLocationParam(child);
						string auxId = StringParam(child, "id");
						if (string.Empty == auxId) //Punto vacío
						{
							RefPunctual auxPunto = new RefPunctual(child);
							parent.Topology.Points.Add(auxPunto);
						}
						else
						{
							Station auxStation = new Station(child, parent);
							parent.Topology.Points.Add(auxStation);
							parent.Stations.Add(auxStation);
						}
					}
				}
			}
			if (parent.Topology.Points.Count > 0)
				parent.Topology.recalculatePK(); //Asigno los PK de cada punto en función de las referencias
			parent.Topology.RecalculateLinearBounds();
		}
		private void CompileAxisLimits(XElement root, Axis parent)
		{
			if (null == parent.Topology) parent.Topology = new TopoAxis();
			if (root is XElement element)
			{
				foreach (XElement hijo in element.Elements())
				{
					if ("item" == hijo.Name)
					{
						SpeedLimit nuevo = new SpeedLimit(hijo);
						parent.Topology.SpeedLimits.Add(nuevo);
					}
				}
			}
		}
		private void CompileAxisSignals(XElement root, Axis parent)
		{
			if (null == parent.Topology) parent.Topology = new TopoAxis();
			if (root is XElement element)
			{
				foreach (XElement hijo in element.Elements())
				{
					if ("item" == hijo.Name)
					{
						Signal nueva = new Signal(hijo);
						parent.Topology.Signals.Add(nueva);
					}
				}
			}
		}
		internal bool CompileAxisCollection(XElement root,TopoStorage storage)
		{
			if (root is XElement element)
			{
				foreach (XElement hijo in element.Elements())
				{
					if (hijo.Name.LocalName.Equals("axis"))
					{
						Axis nuevo = CompileAxis(hijo);
						if (storage.mcolAxis.ContainsKey(nuevo.id))
						{
							Result.Success = false;
							Result.Warnings.Add(new XMLCompileWarning(string.Format("Topo Storage {0} already contains an axis named {1} with same id {2}", storage.Header.Name, nuevo.Name, nuevo.id), -1, XMLCompileWarning.SeverityEnum.Severe));
							return false;
						}
						else
							storage.mcolAxis.Add(nuevo.id, nuevo);							
					}
				}
			}
			return true;
		}
		internal Asimilation CompileAsimilation(XElement root, TopoStorage storage)
		{
			Station? currentStation = null;
			Axis? auxCurrentAxis = null;
			currentStation = storage.stationById(StringParam(root, "origin"));
			auxCurrentAxis = storage.axisByStation(currentStation);
			Asimilation currentAsimilation = new Asimilation(root, storage);
			currentAsimilation.origin = currentStation;
			if (root is XElement element)
			{
				foreach (XElement hijo in element.Elements())
				{
					if ("trip" == hijo.Name.LocalName)
					{
						currentStation = storage.stationById(StringParam(hijo, "dest"));
						auxCurrentAxis = storage.axisByStation(currentStation);
						if (null != currentStation && null != auxCurrentAxis)
						{
							AsimilationStep paso = new AsimilationStep(currentStation,
								auxCurrentAxis,
								TimeSpanParam(hijo, "time"),
								TimeSpanParam(hijo, "stop"));
							currentAsimilation.mcolSteps.Add(paso);
						}
					}
				}
			}
			return currentAsimilation;
		}
		internal bool CompileAsimilationCollection(XElement root, TopoStorage storage)
		{
			if (root is XElement element)
			{
				foreach (XElement hijo in element.Elements())
				{
					Asimilation nueva = CompileAsimilation(hijo, storage);
					if (storage.mcolAsimilations.ContainsKey(nueva.id))
					{
						Result.Success = false;
						Result.Warnings.Add(new XMLCompileWarning(string.Format("Topo Storage {0} already contains an asimilation named {1} with id {2}", storage.Header.Name, nueva.name, nueva.id), -1, XMLCompileWarning.SeverityEnum.Severe));
						return false;
					}
					storage.mcolAsimilations.Add(nueva.id, nueva);
				}
			}
			return true;
		}
		internal string DecompileHeader(Header rhs)
		{
			StringBuilder salida = new StringBuilder();
			salida.AppendFormat("<info id=\"{0}\"\n", rhs.Id);
			if (rhs.ParentId != Guid.Empty)
				salida.AppendFormat("topoId=\"{0}\"\n", rhs.ParentId);

			salida.AppendFormat("name=\"{0}\"\n description\"{1}\"\n comment\"{2}\"\n license\"{3}\"\n",
				rhs.Name,
				rhs.Description,
				rhs.Comment,
				rhs.License);
			salida.AppendFormat("author=\"{0}\"\n firstdate=\"{1}\"\n lastdate=\"{2}\"\n version=\"{3}\"\n bitmap=\"\"\n",
				rhs.Author,
				rhs.FirstDate,
				rhs.LastDate,
				rhs.Version,
				rhs.Bitmap
				);
			return salida.ToString();
		}
		internal string DecompileAxis(Axis rhs)
		{
			StringBuilder salida = new StringBuilder();
			salida.AppendFormat("\t<axis id=\"{0}\" name=\"{1}\" comment=\"{2}\" vmax=\"{3}\" color=\"{4}\" darkcolor=\"{5}\" >\n",
				rhs.id,
				rhs.Name,
				rhs.Comment,
				rhs.MaxSpeed,
				rhs.mvarColor[0],
				rhs.mvarColor[1]
				);
			salida.AppendLine("\t\t<poly>");
			if (null != rhs.Topology)
			{
				foreach (RefPunctual auxPunto in rhs.Topology.Points)
					salida.AppendLine("\t\t\t" + auxPunto.XNode());
			}
			salida.AppendLine("\t\t</poly>");
			salida.AppendLine("\t\t<limit>\n");
			if (null != rhs.Topology)
			{
				foreach (SpeedLimit auxLimit in rhs.Topology.SpeedLimits)
					salida.AppendLine("\t\t\t" + auxLimit.XNode());
			}
			salida.AppendLine("\t\t</limit>\n");
			salida.AppendLine("\t\t<signal>\n");
			if (null != rhs.Topology)
			{
				///Implementar aquí las señales que contiene el eje.
			}
			salida.AppendLine("\t\t</signal>\n");
			salida.AppendLine("\t</axis>");
			return salida.ToString();
		}
		#endregion Topo
		#region Rauta
		/// <summary>
		/// Obtiene el Guid del TopoStorage que es compatible con este rauta.
		/// </summary>
		/// <param name="root"></param>
		/// <returns></returns>
		public Guid TopoStorageIdByRauta(XNode root)
		{
			if (root is XElement element)
			{
				foreach (XElement hijo in element.Elements())
				{
					if (hijo.Name.LocalName == "info")
					{
						Header auxHeader = CompileHeader(hijo);
						return auxHeader.ParentId;
					}
				}
			}
			return Guid.Empty;
		}
		/// <summary>
		/// Extrae un archivo Rauta de XML e intenta asignárselo a un TopoStorage ya existente
		/// </summary>
		/// <param name="parent"></param>
		/// <param name="root"></param>
		/// <returns></returns>
		public bool CompileRauta(TopoStorage parent, XNode root)
		{
			Rauta salida = new Rauta(parent);

			if (root is XElement element)
			{
				foreach (XElement hijo in element.Elements())
				{
					switch (hijo.Name.LocalName)
					{
						case "info":
							Header cabecera = CompileHeader(hijo);
							if(cabecera.ParentId!=parent.Header.Id)
							{
								Result.Success = false;
								Result.Warnings.Add(new XMLCompileWarning(string.Format("Rauta named {0} has an incompatible signature with TopoStorage named {1}", cabecera.Name, parent.Header.Name), -1, XMLCompileWarning.SeverityEnum.Fatal));
								return false;
							}
							salida.Header = cabecera;
							break;
						case "plans":
							if(!CompilePlans(hijo,salida))
							{
								Result.Success = false;
								if(null==salida)
								{
									Result.Warnings.Add(new XMLCompileWarning("Current rauta is not complete and has registered errors trying to deserialize its plans", -1, XMLCompileWarning.SeverityEnum.Fatal));
								}
								else
								{
									Result.Warnings.Add(new XMLCompileWarning(string.Format("Rauta named {0} could not deserialize it's plans.", salida.Header.Name), -1, XMLCompileWarning.SeverityEnum.Warning));
								}
								return false;
							}
							break;
					}
				}
				//Si no ha devuelto "false", intentará instalar el rauta en parent.
				if (parent.ColRauta.ContainsKey(salida.Header.Id))
				{
					Result.Warnings.Add(new XMLCompileWarning(string.Format("Rauta named {0} already exists on topo collection {1}. New instance will overwrite old one.", salida.Header.Name, parent.Header.Name), -1, XMLCompileWarning.SeverityEnum.Warning));
					parent.ColRauta[salida.Header.Id] = salida;
				}
				else
					parent.ColRauta.Add(salida.Header.Id, salida);
			}
			return true; //Aquí llegamos si todo fue bien.
		}
		internal bool CompilePlans(XNode root, Rauta parent)
		{
			if (root is XElement element)
			{
				foreach (XElement hijo in element.Elements())
				{
					if (hijo.Name == "plan")
					{
						Plan? nuevo = CompilePlan(hijo, parent);
						if(null==nuevo)
							return false;
						else
							parent.Plans.Add(nuevo.mvarName, nuevo);
					}
				}
			}
			return true;
		}
		internal Plan? CompilePlan(XNode root, Rauta parent)
		{ 
		

		}

		#endregion Rauta
		#region Lectura de atributos
		private string StringParam(XNode node, string paramId, string defaultValue = "")
		{
			if (node is XElement element)
			{
				if (null == element.Attribute(paramId))
				{
					Result.Success = false;
					Result.Warnings.Add(
						new XMLCompileWarning(
							string.Format("Node {0} has no attribute named {1}.", element.Name, paramId),
							-1,
							XMLCompileWarning.SeverityEnum.Error));
				}
				else
				{
					if (null == element.Attribute(paramId)?.Value)
					{
						Result.Success = false;
						Result.Warnings.Add(
						new XMLCompileWarning(
						string.Format("Attribute {0} in node {1} has no value.", paramId, element.Name),
						-1,
						XMLCompileWarning.SeverityEnum.Warning));
					}
					else
					{
						return element.Attribute(paramId)?.Value ?? defaultValue;
					}
				}
			}
			else
			{
				Result.Success = false;
				Result.Warnings.Add(
					new XMLCompileWarning(
						string.Format("Node {0} is not an element.", node.BaseUri),
						-1,
						XMLCompileWarning.SeverityEnum.Error));
			}
			return defaultValue;
		}
		private bool BoolParam(XNode node, string paramId, bool defaultValue = false)
		{
			string salida = StringParam(node, paramId, "XX");
			if (salida != "XX")
			{
				if (salida.ToUpper().Contains("T"))
					return true;
				else if (salida.ToUpper().Contains("F"))
					return false;
				else
				{
					if (node is XElement element)
					{
						Result.Success = false;
						Result.Warnings.Add(
						new XMLCompileWarning(
						string.Format("Attribute {0} with value {1} in node {2} is not a boolean.", paramId, salida, element.Name),
						-1,
						XMLCompileWarning.SeverityEnum.Warning));
					}
				}
			}
			return defaultValue;
		}
		private byte ByteParam(XNode node, string paramId, byte defaultValue = 255)
		{
			if (node is XElement element)
			{
				if (element.Attribute(paramId)?.Value is string value)
				{
					if (byte.TryParse(value, out byte salida))
						return salida;
					else
					{
						Result.Success = false;
						Result.Warnings.Add(
							new XMLCompileWarning(
								string.Format("Attribute {0} with value {1} in node {2} is not a byte.", paramId, value, element.Name),
								-1,
								XMLCompileWarning.SeverityEnum.Warning));
					}
				}
				else
				{
					Result.Success = false;
					Result.Warnings.Add(
						new XMLCompileWarning(
							string.Format("Node {0} has no attribute named {1}.", element.Name, paramId),
							-1,
							XMLCompileWarning.SeverityEnum.Error));
				}
			}
			else
			{
				Result.Success = false;
				Result.Warnings.Add(
					new XMLCompileWarning(
						string.Format("Node {0} is not an element.", node.BaseUri),
						-1,
						XMLCompileWarning.SeverityEnum.Error));
			}
			return defaultValue;
		}
		private int IntParam(XNode node, string paramId, int defaultValue = -1)
		{
			if (node is XElement element)
			{
				if (element.Attribute(paramId)?.Value is string stringValue)
				{
					if (int.TryParse(stringValue, out int salida))
						return salida;
					else
					{
						Result.Success = false;
						Result.Warnings.Add(
							new XMLCompileWarning(
								string.Format("Attribute {0} with value {1} in node {2} is not an integer.", paramId, stringValue, element.Name),
								-1,
								XMLCompileWarning.SeverityEnum.Warning));
					}
				}
				else
				{
					Result.Success = false;
					Result.Warnings.Add(
						new XMLCompileWarning(
							string.Format("Node {0} has no attribute named {1}.", element.Name, paramId),
							-1,
							XMLCompileWarning.SeverityEnum.Error));
				}
			}
			else
			{
				Result.Success = false;
				Result.Warnings.Add(
					new XMLCompileWarning(
						string.Format("Node {0} is not an element.", node.BaseUri),
						-1,
						XMLCompileWarning.SeverityEnum.Error));
			}
			return defaultValue;
		}
		private long LongParam(XNode node, string paramId, long defaultValue = -1)
		{
			if (node is XElement element)
			{
				if (element.Attribute(paramId)?.Value is string stringValue)
				{
					if (long.TryParse(stringValue, out long salida))
						return salida;
					else
					{
						Result.Success = false;
						Result.Warnings.Add(
							new XMLCompileWarning(
								string.Format("Attribute {0} with value {1} in node {2} is not a long integer.", paramId, stringValue, element.Name),
								-1,
								XMLCompileWarning.SeverityEnum.Warning));
					}
				}
				else
				{
					Result.Success = false;
					Result.Warnings.Add(
						new XMLCompileWarning(
							string.Format("Node {0} has no attribute named {1}.", element.Name, paramId),
							-1,
							XMLCompileWarning.SeverityEnum.Error));
				}
			}
			else
			{
				Result.Success = false;
				Result.Warnings.Add(
					new XMLCompileWarning(
						string.Format("Node {0} is not an element.", node.BaseUri),
						-1,
						XMLCompileWarning.SeverityEnum.Error));
			}
			return defaultValue;
		}
		private double DoubleParam(XNode node, string paramId, double defaultValue = double.NaN)
		{
			if (node is XElement element)
			{
				if (element.Attribute(paramId)?.Value is string stringValue)
				{
					if (double.TryParse(stringValue, out double salida))
						return salida;
					else
					{
						Result.Success = false;
						Result.Warnings.Add(
							new XMLCompileWarning(
								string.Format("Attribute {0} with value {1} in node {2} is not a double.", paramId, stringValue, element.Name),
								-1,
								XMLCompileWarning.SeverityEnum.Warning));
					}
				}
				else
				{
					Result.Success = false;
					Result.Warnings.Add(
						new XMLCompileWarning(
							string.Format("Node {0} has no attribute named {1}.", element.Name, paramId),
							-1,
							XMLCompileWarning.SeverityEnum.Error));
				}
			}
			else
			{
				Result.Success = false;
				Result.Warnings.Add(
					new XMLCompileWarning(
						string.Format("Node {0} is not an element.", node.BaseUri),
						-1,
						XMLCompileWarning.SeverityEnum.Error));
			}
			return defaultValue;
		}
		private Guid GuidParam(XNode node, string paramId)
		{
			if (node is XElement element)
			{
				if (element.Attribute(paramId)?.Value is string stringValue)
				{
					if (Guid.TryParse(stringValue, out Guid salida))
						return salida;
					else
					{
						Result.Success = false;
						Result.Warnings.Add(
							new XMLCompileWarning(
								string.Format("Attribute {0} with value {1} in node {2} is not a Guid.", paramId, stringValue, element.Name),
								-1,
								XMLCompileWarning.SeverityEnum.Warning));
					}
				}
				else
				{
					Result.Success = false;
					Result.Warnings.Add(
						new XMLCompileWarning(
							string.Format("Node {0} has no attribute named {1}.", element.Name, paramId),
							-1,
							XMLCompileWarning.SeverityEnum.Error));
				}
			}
			else
			{
				Result.Success = false;
				Result.Warnings.Add(
					new XMLCompileWarning(
						string.Format("Node {0} is not an element.", node.BaseUri),
						-1,
						XMLCompileWarning.SeverityEnum.Error));
			}
			return Guid.Empty;
		}
		private DateTime DateTimeParam(XNode node, string paramId)
		{
			if (node is XElement element)
			{
				if (element.Attribute(paramId)?.Value is string stringValue)
				{
					if (DateTime.TryParse(stringValue, out DateTime salida))
						return salida;
					else
					{
						Result.Success = false;
						Result.Warnings.Add(
							new XMLCompileWarning(
								string.Format("Attribute {0} with value {1} in node {2} is not a date or a time.", paramId, stringValue, element.Name),
								-1,
								XMLCompileWarning.SeverityEnum.Warning));
					}
				}
				else
				{
					Result.Success = false;
					Result.Warnings.Add(
						new XMLCompileWarning(
							string.Format("Node {0} has no attribute named {1}.", element.Name, paramId),
							-1,
							XMLCompileWarning.SeverityEnum.Error));
				}
			}
			else
			{
				Result.Success = false;
				Result.Warnings.Add(
					new XMLCompileWarning(
						string.Format("Node {0} is not an element.", node.BaseUri),
						-1,
						XMLCompileWarning.SeverityEnum.Error));
			}
			return DateTime.MinValue;
		}
		private TimeSpan TimeSpanParam(XNode node, string paramId)
		{
			if (node is XElement element)
			{
				if (element.Attribute(paramId)?.Value is string stringValue)
				{
					if (TimeSpan.TryParse(stringValue, out TimeSpan salida))
						return salida;
					else
					{
						Result.Success = false;
						Result.Warnings.Add(
							new XMLCompileWarning(
								string.Format("Attribute {0} with value {1} in node {2} is not a time span.", paramId, stringValue, element.Name),
								-1,
								XMLCompileWarning.SeverityEnum.Warning));
					}
				}
				else
				{
					Result.Success = false;
					Result.Warnings.Add(
						new XMLCompileWarning(
							string.Format("Node {0} has no attribute named {1}.", element.Name, paramId),
							-1,
							XMLCompileWarning.SeverityEnum.Error));
				}
			}
			else
			{
				Result.Success = false;
				Result.Warnings.Add(
					new XMLCompileWarning(
						string.Format("Node {0} is not an element.", node.BaseUri),
						-1,
						XMLCompileWarning.SeverityEnum.Error));
			}
			return new TimeSpan(0);
		}
		private GeoLocation GeoLocationParam(XNode node)
		{
			return new GeoLocation(DoubleParam(node, "x", 0), DoubleParam(node, "y", 0));
		}
		#endregion Lectura de atributos
	}
}
