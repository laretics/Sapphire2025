using Sapphire2026Telegram;
using Microsoft.AspNetCore.SignalR.Client;

// Configuración del cliente SignalR como singleton

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddSingleton<HubConnection>(sp =>
{
	var configuration = sp.GetRequiredService<IConfiguration>();
	var hubUrl = configuration.GetValue<string>("SignalR:HubUrl") ?? "http://localhost:5000/signalrhub";

	return new HubConnectionBuilder()
	.WithUrl(hubUrl)
	.WithAutomaticReconnect()
	.Build();
});

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
