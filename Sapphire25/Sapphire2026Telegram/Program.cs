using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Hosting;
using Sapphire2025.Storage;
using Sapphire2026Telegram;

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

	builder.Services.AddSingleton<HubConnection>(hubConnection);
}
else
{
	builder.Services.AddSingleton<HubConnection?>(sp => null);
}

builder.Services.AddHostedService<Worker>();

string auxApiAddress = builder.Configuration["ApiBaseAddress"] ?? "http://localhost:5000/api/";
Console.WriteLine($"API Address for SFM: {auxApiAddress}");
Uri apiBaseUri = new Uri(auxApiAddress);
Console.WriteLine($"Final API URI: {apiBaseUri}");
builder.Services.AddScoped(sp => new HttpClient { BaseAddress = apiBaseUri });
builder.Services.AddScoped<IntStorageService>();
builder.Services.AddScoped<AuthenticationClient>();
builder.Services.AddScoped<AeneasClient>();
builder.Services.AddScoped<ExpertClient>();
builder.Services.AddScoped<TimeNetClient>();
builder.Services.AddSystemd();

var host = builder.Build();
await host.RunAsync();