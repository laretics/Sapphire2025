namespace Sapphire2025Server.Comunications
{
	public class ArmanditoScrap
	{
	}
}


/*
 * 	  using SocketIOClient;
using System;
using System.Text.Json;
using System.Threading.Tasks;

class Program
{
    static async Task Main(string[] args)
    {
        var estacion = 1; // Cambia por la estación que quieras

        var socket = new SocketIO("https://info.trensfm.com", new SocketIOOptions
        {
            Reconnection = true,
            Transport = SocketIOClient.Transport.TransportProtocol.WebSocket
        });

        socket.OnConnected += async (sender, e) =>
        {
            Console.WriteLine("✅ Conectado al servidor Socket.IO");
            await socket.EmitAsync("tipo", "panel", new { estacion = estacion, clase = "LCD" });
        };

        socket.On("base", response =>
        {
            Console.WriteLine("📡 Datos base recibidos");
            var data = response.GetValue<JsonElement>();
            Console.WriteLine(data);
            // Aquí tienes líneas, ubicaciones, etc.
        });

        socket.On("panel", response =>
        {
            Console.WriteLine("🚆 Datos del panel recibidos (horarios)");
            try
            {
                var data = response.GetValue<JsonElement>();
                Console.WriteLine(JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true }));

                // Ejemplo de extracción útil:
                // var registros = data.GetProperty("info"); // ajusta según la estructura
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error parseando panel: " + ex.Message);
            }
        });

        socket.OnDisconnected += (sender, e) =>
        {
            Console.WriteLine("❌ Desconectado: " + e);
        };

        await socket.ConnectAsync();

        Console.WriteLine("Presiona ENTER para salir...");
        Console.ReadLine();

        await socket.DisconnectAsync();
    }
}
 * */