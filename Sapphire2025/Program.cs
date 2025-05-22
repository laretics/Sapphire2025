using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.JSInterop;
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


//builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri("https://localhost:7153")});
//builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri("http://localhost:5000") });
//builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri("http://88.99.33.109:5031") });

builder.Services.AddScoped<IntStorageService>(); //Acceso a los datos de sesión.

builder.Services.AddScoped<AuthenticationClient>(); //Cliente http autenticación
builder.Services.AddScoped<AeneasClient>(); //Cliente http Aeneas

builder.Services.AddSingleton<InteractiveService>(); //Servicio para refresco de datos.


await builder.Build().RunAsync();
