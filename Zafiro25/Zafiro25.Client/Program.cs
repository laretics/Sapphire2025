using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Zafiro25.Client;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
Console.WriteLine("Program.cs del cliente se está ejecutando.");
builder.Services.AddAuthorizationCore();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddSingleton<AuthenticationStateProvider, PersistentAuthenticationStateProvider>();
builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
await builder.Build().RunAsync();
Console.WriteLine("Program.cs del cliente ha terminado de ejecutarse.");
