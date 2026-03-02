using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using TimeNet2026.Auxiliar;
using TimeNet2026.Topo;

namespace TimeNet2026.ScriptCompiling
{
	/// <summary>
	/// Esta clase sirve para deserializar la estructura TimeNet sin ensuciar la información de la jerarquía con funciones raras.
	/// </summary>
	internal class XMLCompiler
	{
		public XMLCompiler() { }

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
							importAxis(hijo, salida);
							break;
						case "asimilation": //Asimilaciones
							deserializeAsimilations(hijo, this);
							break;
					}
				}
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
