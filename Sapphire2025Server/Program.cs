using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.FileProviders;
using Sapphire2025Server.Comunications;
using Sapphire2025Server.Storage;

var builder = WebApplication.CreateBuilder(args);

// Accede a la cadena de conexi�n remota
var remoteConnectionString = builder.Configuration.GetConnectionString("RemoteConnection");

// Configuraci�n de CORS para permitir solicitudes tanto en modo desarrollo como en producci�n
builder.Services.AddCors(options =>
{
	options.AddPolicy("AllowAll", policy =>
	{
		policy.AllowAnyOrigin()
			  .AllowAnyHeader()
			  .AllowAnyMethod();
	});
});

builder.Services.Configure<FormOptions>(options =>
{
	options.MultipartBodyLengthLimit = 25L * 1024 * 1024;
});
builder.WebHost.ConfigureKestrel(options =>
{
	options.Limits.MaxRequestBodySize = 25L * 1024 * 1024;
});

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddSignalR(); //Componente de SignalR

// Configuraci�n de Forwaded Headers para Nginx
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
	options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
	options.KnownNetworks.Clear(); // Limpia las redes conocidas para aceptar cualquier red
	options.KnownProxies.Clear();  // Limpia los proxies conocidos para aceptar cualquier proxy
});

builder.Services.AddSingleton<IConfiguration>(builder.Configuration);
//builder.Services.AddSingleton<BotSoul>();

var app = builder.Build();

IHostApplicationLifetime mvarLifeTime = app.Services.GetRequiredService<IHostApplicationLifetime>();
//BotSoul instancia = app.Services.GetRequiredService<BotSoul>(); //De esta forma se crea la instancia nada m�s arrancar el programa.

mvarLifeTime.ApplicationStopping.Register(() =>
{
	// Aqu� tu l�gica de parada, por ejemplo:
	//instancia.BroadcastByRole("Mensaje desde el servidor: \"�Sistema detenido!\"",true, new Sapphire2025Models.Common.UserRole[] { Sapphire2025Models.Common.UserRole.Root }).GetAwaiter().GetResult();
});

//await instancia.BroadcastByRole("Mensaje desde el servidor: \"�Sistema iniciado!\"",true, new Sapphire2025Models.Common.UserRole[] { Sapphire2025Models.Common.UserRole.Root });

Directory.CreateDirectory(NoteMediaStore.GetRoot(app.Configuration));

app.UseForwardedHeaders();

app.UseCors("AllowAll");

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
	app.MapOpenApi();
}

app.UseStaticFiles(new StaticFileOptions
{
	FileProvider = new PhysicalFileProvider(
		Path.Combine(Directory.GetCurrentDirectory(), "Resources", "Images")),
	RequestPath = "/images"
});

app.UseAuthorization();

app.MapControllers();

app.MapHub<SignalRHub>("/signalrhub");

app.Run();


