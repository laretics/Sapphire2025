using System.Xml;

namespace TimeNetWebServer.Classes
{
	internal class XMLImporter
	{
		internal async Task<XmlElement?> ImportXML(Stream rhs)
		{
			XmlDocument documento = new XmlDocument();
			try
			{
				using StreamReader lector = new StreamReader(rhs);
				string contenido = await lector.ReadToEndAsync();
				documento.LoadXml(contenido);
			}
			catch (Exception ex){}
			
			if (null != documento.DocumentElement)
				return documento.DocumentElement;

			return null;
		}

	}
}
