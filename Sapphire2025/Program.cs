using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.JSInterop;
using Sapphire2025;
using Sapphire2025.Storage;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

//builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

builder.Services.AddBlazorBootstrap();

//builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri("https://localhost:7153")});
builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri("http://localhost:5031") });

builder.Services.AddScoped<IntStorageService>(); //Acceso a los datos de sesión.

builder.Services.AddScoped<AuthenticationClient>(); //Cliente http autenticación
builder.Services.AddScoped<AeneasClient>(); //Cliente http Aeneas

builder.Services.AddSingleton<InteractiveService>(); //Servicio para refresco de datos.

await builder.Build().RunAsync();
