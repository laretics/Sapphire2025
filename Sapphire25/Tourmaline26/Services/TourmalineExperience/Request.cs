namespace Tourmaline26.Services.TourmalineExperience
{
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Text.Json;

	public class TourmalineResponse
	{
		public bool success { get; set; }
		public string message { get; set; } = string.Empty;
		public string? response { get; set; }
	}
	public class LaunchRequest
	{
		/// <summary>
		/// Nombre de la carpeta de la ruta
		/// </summary>
		[Required]
		[DefaultValue("SFM")]
		public string Route { get; set; } = "SFM";

		/// <summary>
		/// Nombre de la actividad o subcarpeta dentro de la ruta
		/// </summary>
		[Required]
		[DefaultValue("T21")]
		public string RoutePath { get; set; } = "T21";

		/// <summary>
		/// Nombre del consist o locomotora
		/// </summary>
		[Required]
		[DefaultValue("Triple81")]
		public string Consist { get; set; } = "Triple81";

		/// <summary>
		/// Hora de inicio de la simulación
		/// </summary>
		[DefaultValue("12:00")]
		public string Now { get; set; } = "12:00";

		/// <summary>
		/// Estación del año
		/// </summary>
		[DefaultValue(0)]
		public int Season { get; set; } = 0;

		/// <summary>
		/// Meteorología
		/// </summary>
		[DefaultValue(0)]
		public int Climate { get; set; } = 0;

	}
}
