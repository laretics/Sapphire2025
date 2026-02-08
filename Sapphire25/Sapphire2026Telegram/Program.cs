using Sapphire2026Telegram;
using Microsoft.AspNetCore.SignalR.Client;

var builder = Host.CreateApplicationBuilder(args);

// Configurar HubConnection desde la configuración
var signalREnabled = builder.Configuration.GetValue<bool>("SignalR:Enabled", false);
var hubUrl = builder.Configuration.GetValue<string>("SignalR:HubUrl");

if (signalREnabled && !string.IsNullOrEmpty(hubUrl))
{
	var hubConnection = new HubConnectionBuilder()
		.WithUrl(hubUrl)
		.WithAutomaticReconnect()
		.Build();

	builder.Services.AddSingleton<HubConnection?>(hubConnection);
}
else
{
	builder.Services.AddSingleton<HubConnection?>(sp => null);
}

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();