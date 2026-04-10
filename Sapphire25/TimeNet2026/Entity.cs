using System.Drawing;

namespace TimeNet2026
{
	public interface Entity
	{
		string name { get; set; } //Nombre de la entidad
		string comment { get; set; } //Un comentario sobre esta entidad
		string[] color { get; set; } //Color para representar gráficamente esta entidad
		public static string AtenuateColor(string rhs)
		{
			if (string.IsNullOrWhiteSpace(rhs) || !rhs.StartsWith("#") || rhs.Length != 7)
				return rhs; //No atenuamos

			int r = Convert.ToInt32(rhs.Substring(1, 2), 16) / 2;
			int g = Convert.ToInt32(rhs.Substring(3, 2), 16) / 2;
			int b = Convert.ToInt32(rhs.Substring(5, 2), 16) / 2;

			return $"#{r:X2}{g:X2}{b:X2}";
		}
	}
}
