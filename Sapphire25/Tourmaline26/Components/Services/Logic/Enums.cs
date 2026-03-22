namespace Tourmaline26.Components.Services.Logic
{
	public static class Enums
	{
		public enum TeslaMode //Afecta a la visualización de las ventanas y establece el modo de trabajo.
		{
			Iddle, //Navegador con todo apagado. Modo inicial.
			DestinationSelect, //Selección de itinerario para la ruta.
			Navigation, //Modo navegación. En la conducción normal del tren.
			RightFrame //Panel derecho desplegado. Es el modo en el que se introducen datos.
		}
	}
}
