using Tourmaline26.Components;
using Sapphire2025.Storage;
using Microsoft.EntityFrameworkCore;
using Tourmaline26.Logic;
using Tourmaline26.Services;
using Tourmaline26.Services.TourmalineExperience;

var builder = WebApplication.CreateBuilder(args);

// Personalizar la carga de configuración:
builder.Configuration.Sources.Clear(); // Elimina todas las fuentes por defecto
builder.Configuration.AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: false, reloadOnChange: true);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddSingleton<TourmalineService>();
builder.Services.AddHostedService<TourmalineBackground>();
builder.Services.AddHttpContextAccessor();

string auxApiAddress = builder.Configuration["ApiBaseAddress"] ?? "https://material.trensfm.com:5031/tourmaline/";
Console.WriteLine($"API Address for SFM: {auxApiAddress}");

Uri apiBaseUri;
apiBaseUri = new Uri(auxApiAddress);
Console.WriteLine($"Final API URI: {apiBaseUri}");
builder.Services.AddDbContext<TourmalineContext>(options =>
options.UseSqlite("Data Source=tourmaline.db"));

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = apiBaseUri });

builder.Services.AddScoped<IntStorageService>();
builder.Services.AddScoped<AuthenticationClient>();
builder.Services.AddScoped<AeneasClient>();
builder.Services.AddScoped<ExpertClient>();
builder.Services.AddScoped<TimeNetClient>();
builder.Services.AddHostedService<MediaMTXService>();
builder.Services.AddSingleton<GPSService>();
builder.Services.AddHttpClient<LEDDisplayService>();
builder.Services.AddSingleton<MeteoService>();

// Configurar HttpClient para llamar a la API local
builder.Services.AddHttpClient<MediaMTXService>("CameraService", client =>
{
	client.Timeout = TimeSpan.FromSeconds(30);
});
builder.Services.AddHttpClient<MVBService>();
builder.Services.AddHttpClient<TourmalineExperienceService>();

var app = builder.Build();

var logger = app.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("API Address for SFM: {ApiAddress}", auxApiAddress);
logger.LogInformation("Final API URI: {ApiBaseUri}", apiBaseUri);


// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

//app.UseHttpsRedirection();


app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
