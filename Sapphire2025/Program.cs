using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using System.Text.Json;
using Sapphire2025;
using Sapphire2025.Storage;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddBlazorBootstrap();

//Lectura de la configuración desde appsettings.json
HttpClient http = new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) };
string configJson = await http.GetStringAsync("appsettings.json");
JsonDocument auxDoc = JsonDocument.Parse(configJson);
string auxApiAddress = auxDoc.RootElement.GetProperty("ApiBaseAddress").GetString() ?? builder.HostEnvironment.BaseAddress;
Console.WriteLine($"API Address for SFM: {auxApiAddress}");
builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(auxApiAddress) });

builder.Services.AddSingleton<InteractiveService>(); //Necesito este objeto sólo para que el sistema se sincronice con las operaciones de login y logout.

builder.Services.AddScoped<IntStorageService>(); //Acceso a los datos de sesión.
builder.Services.AddScoped<AuthenticationClient>(); //Cliente http autenticación
builder.Services.AddScoped<AeneasClient>(); //Cliente http Aeneas
builder.Services.AddScoped<ExpertClient>(); //Cliente para peticiones de gráficos de Maquinistas

await builder.Build().RunAsync();
