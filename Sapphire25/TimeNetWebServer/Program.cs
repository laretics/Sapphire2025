using TimeNet2026.Storage;
using TimeNetWebServer.Components;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
	.AddInteractiveServerComponents();
string dbPath = Path.Combine(builder.Environment.ContentRootPath, "App_Data", "TimeNet.sqlite");

builder.Services.AddScoped<OnyxStorage>
	(sp => { OnyxStorage storage = new OnyxStorage();
		storage.StorageFile = dbPath;
		return storage;
	});

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
