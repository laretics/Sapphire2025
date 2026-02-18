using System.Xml.Linq;

namespace TimeNetWebServer.Classes
{
	internal class XMLImporter
	{
		internal async Task<XElement?> ImportXML(Stream rhs)
		{
			XDocument documento;
			try
			{
				using StreamReader lector = new StreamReader(rhs);
				string contenido = await lector.ReadToEndAsync();
				documento = XDocument.Parse(contenido);
				return documento.Root;
			}
			catch (Exception ex){ Console.WriteLine(ex.Message); }				
			return null;
		}

	}
}
