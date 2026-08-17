using Diamond.Controls.Rendering;
using Microsoft.EntityFrameworkCore;
using Sapphire2025.Storage;
using Sapphire2026Clients;
using Tourmaline26.Components;
using Tourmaline26.Logic;
using Tourmaline26.Services;
using Tourmaline26.Services.Armandito;
using Tourmaline26.Services.Cameras;
using Tourmaline26.Services.Logging;
using Tourmaline26.Services.SfmInfo;
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

// Misma base que Sapphire2025 / Sapphire2026Clients (controladores en /api/sapphireaeneas, etc.).
// Ojo: no usar .../tourmaline/ — los clientes componen rutas relativas tipo "sapphireaeneas/getnotes".
CirculationDocumentBranding.ApplyFromConfiguration(
	builder.Configuration["Diamond:Documents:CompanyLogo"],
	builder.Environment.WebRootPath);

string auxApiAddress = builder.Configuration["ApiBaseAddress"] ?? "https://material.trensfm.com:5031/api/";
if (!auxApiAddress.EndsWith('/'))
    auxApiAddress += "/";
Console.WriteLine($"API Address for SFM: {auxApiAddress}");

Uri apiBaseUri;
apiBaseUri = new Uri(auxApiAddress);
Console.WriteLine($"Final API URI: {apiBaseUri}");
builder.Services.AddDbContext<TourmalineContext>(options =>
options.UseSqlite("Data Source=tourmaline.db"));

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = apiBaseUri });

builder.Services.AddScoped<IntStorageService>();
builder.Services.AddScoped<SessionService>();
builder.Services.AddScoped<AuthenticationClient>();
builder.Services.AddScoped<AeneasClient>();
builder.Services.AddScoped<ExpertClient>();
builder.Services.AddScoped<DiamondClient>();
builder.Services.AddScoped<UserPreferencesService>();
builder.Services.AddSingleton<Tourmaline26.Services.CabinCache.DiamondLocalCache>();
builder.Services.AddHostedService<MediaMTXService>();
// Proxy RTSP → MJPEG nativo (sin MediaMTX en el camino de visualización HMI).
builder.Services.AddSingleton<CameraStreamService>();
builder.Services.AddSingleton<GPSService>();
builder.Services.AddHttpClient<LedPanelController>();
builder.Services.AddSingleton<LEDDisplayService>();
builder.Services.AddSingleton<MeteoService>();

// Configurar HttpClient para llamar a la API local
builder.Services.AddHttpClient<MediaMTXService>("CameraService", client =>
{
	client.Timeout = TimeSpan.FromSeconds(30);
});
// MVBService es BackgroundService: debe ser Singleton + HostedService.
// AddHttpClient<MVBService>() solo registra el typed client (Transient) y NUNCA
// arranca ExecuteAsync → CurrentData quedaba siempre null.
builder.Services.AddHttpClient("MVB", client =>
{
    client.Timeout = TimeSpan.FromSeconds(2);
});
builder.Services.AddSingleton<MVBService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<MVBService>());
builder.Services.AddHttpClient<TourmalineExperienceService>();
builder.Services.AddHttpClient<ArmanditoService>(client =>
{
    string baseUrl = builder.Configuration["SystemConfiguration:SfmInfoUrl"] ?? "https://info.trensfm.com:8084/";
    client.BaseAddress = new Uri(baseUrl.EndsWith("/") ? baseUrl : baseUrl + "/");
    client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
        "Bearer",
        builder.Configuration["SystemConfiguration:SfmInfoToken"] ?? "SFM2026");
    client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
});

// Panel de salidas SFM (info.trensfm.com): REST catálogo + Socket.IO long-poll.
builder.Services.AddHttpClient(SfmDeparturesService.HttpClientName, client =>
{
    string panelUrl = builder.Configuration["SystemConfiguration:SfmPanelUrl"] ?? "https://info.trensfm.com";
    client.BaseAddress = new Uri(panelUrl.EndsWith("/") ? panelUrl : panelUrl + "/");
    // Long-poll de Engine.IO puede quedar abierto ~20–25 s.
    client.Timeout = TimeSpan.FromSeconds(60);
    client.DefaultRequestHeaders.Accept.Add(
        new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
});
builder.Services.AddSingleton<SfmDeparturesService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<SfmDeparturesService>());

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddProvider(new TourmalineLogger(Path.Combine(AppContext.BaseDirectory, "Logs")));

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

// ── Cámaras: snapshot JPEG y stream MJPEG (proxy RTSP nativo) ──────────────
// El <img src="..."> del HMI apunta aquí; no pasa por antiforgery de formularios.
app.MapGet("/api/cameras/{id:int}/snapshot.jpg", async (
    int id,
    CameraStreamService cameras,
    HttpContext http,
    CancellationToken ct) =>
{
    await cameras.WriteSnapshotAsync(id, http.Response, ct);
}).DisableAntiforgery();

app.MapGet("/api/cameras/{id:int}/mjpeg", async (
    int id,
    CameraStreamService cameras,
    HttpContext http,
    CancellationToken ct) =>
{
    await cameras.WriteMjpegAsync(id, http.Response, ct);
}).DisableAntiforgery();

app.MapGet("/api/cameras", (CameraStreamService cameras) =>
    Results.Ok(cameras.Cameras.Select(c => new
    {
        c.Id,
        c.Name,
        c.Address,
        c.CameraType,
        Stream = $"/api/cameras/{c.Id}/mjpeg",
        Snapshot = $"/api/cameras/{c.Id}/snapshot.jpg"
    }))).DisableAntiforgery();

app.Run();
