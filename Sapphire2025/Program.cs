using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Sapphire2025;
using Sapphire2025.Storage;
using Sapphire2026Clients;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: false);
builder.Configuration.AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: false);


builder.Services.AddBlazorBootstrap();

string auxApiAddress = builder.Configuration["ApiBaseAddress"] ?? "http://localhost:5031/api/";
Console.WriteLine($"API Address for SFM: {auxApiAddress}");

Uri apiBaseUri;
if (auxApiAddress.StartsWith("http://") || auxApiAddress.StartsWith("https://"))
{
	apiBaseUri = new Uri(auxApiAddress);
}
else
{
	apiBaseUri = new Uri(new Uri(builder.HostEnvironment.BaseAddress), auxApiAddress);
}
Console.WriteLine($"Final API URI: {apiBaseUri}");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = apiBaseUri });

builder.Services.AddSingleton<InteractiveService>();
builder.Services.AddScoped<IntStorageService>();
builder.Services.AddScoped<AuthenticationClient>();
builder.Services.AddScoped<AeneasClient>();
builder.Services.AddScoped<ExpertClient>();
builder.Services.AddScoped<DiamondClient>();
builder.Services.AddScoped<SessionService>();

await builder.Build().RunAsync();