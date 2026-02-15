using Microsoft.EntityFrameworkCore;
using TimeNet2026.Storage;
using TimeNetWebServer.Components;
using TimeNet2026.DBStorage;
using TimeNet2026Data;

// Inicializa SQLitePCL
SQLitePCL.Batteries_V2.Init();

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
	.AddInteractiveServerComponents();
string dbPath = Path.Combine(builder.Environment.ContentRootPath, "App_Data", "TimeNet.sqlite");

builder.Services.AddDbContext<TimeNet2026Data.TimeNetContext>
	(options => options.UseSqlite($"Data Source={dbPath}"));

builder.Services.AddScoped<ITimeNetContextStorage>(provider =>
provider.GetRequiredService<TimeNet2026Data.TimeNetContext>());
builder.Services.AddScoped<OnyxStorage>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
	app.UseExceptionHandler("/Error", createScopeForErrors: true);
	// The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
	app.UseHsts();
}

app.UseHttpsRedirection();


app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
	.AddInteractiveServerRenderMode();

app.Run();
