using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Sapphire25.MontefaroAuthentication;
// Cliente

var builder = WebAssemblyHostBuilder.CreateDefault(args);
initBuilder(builder);

await builder.Build().RunAsync();

void initBuilder(WebAssemblyHostBuilder builder)
{
	builder.RootComponents.Add<App>("#app");
	builder.RootComponents.Add<HeadOutlet>("head::after");
	Uri baseAddress = new Uri(builder.HostEnvironment.BaseAddress);
	ConfigureServices(builder.Services, baseAddress);
}


//Separación de la configuración de los servicios
void ConfigureServices(IServiceCollection ss, Uri baseAddress)
{
	//ss.AddAuthorizationCore();
	ss.AddScoped(sp => new MontefaroAuthService(baseAddress));
	ss.AddScoped<MontefaroAuthenticationStateProvider,
	MontefaroAuthenticationStateProvider>();
	ss.AddScoped<AuthenticationStateProvider>(p => p.GetService<MontefaroAuthenticationStateProvider>());
	ss.AddScoped(sp => new HttpClient { BaseAddress = baseAddress });
	//ss.AddAuthentication("BasicAuthentication")
	//	.AddScheme<AuthenticationSchemeOptions, BasicAuthenticationHandler>("BasicAuthentication", null);
	ss.AddAuthorizationCore(options =>
	{
		options.AddPolicy("BasicPolicy", policy =>
		{
			policy.RequireAuthenticatedUser();
		});
	});
}

//Microsoft.AspNetCore.Components.Authorization