using Sapphire2025Server.Telegram;

var builder = WebApplication.CreateBuilder(args);

// Configura la lectura del archivo appsettings.json
builder.Configuration
	.SetBasePath(Directory.GetCurrentDirectory())
	.AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: true);

// Accede a la cadena de conexión remota
var remoteConnectionString = builder.Configuration.GetConnectionString("RemoteConnection");

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddCors(options =>
{
	options.AddPolicy("TodoVale", builder => 
		builder.AllowAnyOrigin().
		AllowAnyHeader().
		AllowAnyMethod());
});

builder.Services.AddSingleton<IConfiguration>(builder.Configuration);
builder.Services.AddSingleton<Sapphire2025Server.Telegram.BotSoul>();

var app = builder.Build();

IHostApplicationLifetime mvarLifeTime = app.Services.GetRequiredService<IHostApplicationLifetime>();
BotSoul instancia = app.Services.GetRequiredService<BotSoul>(); //De esta forma se crea la instancia nada más arrancar el programa.

mvarLifeTime.ApplicationStopping.Register(() =>
{
	// Aquí tu lógica de parada, por ejemplo:
	instancia.BroadcastByRole("Mensaje desde el servidor: \"¡Sistema detenido!\"",true, new Sapphire2025Models.Common.UserRole[] { Sapphire2025Models.Common.UserRole.Root }).GetAwaiter().GetResult();
});

await instancia.InitUsers();
await instancia.BroadcastByRole("Mensaje desde el servidor: \"¡Sistema iniciado!\"",true, new Sapphire2025Models.Common.UserRole[] { Sapphire2025Models.Common.UserRole.Root });


// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
	app.MapOpenApi();
	//Esto lo he tenido que comentar porque no me va el servidor desde fuera de local.
	//app.UseHttpsRedirection();
}


app.UseAuthorization();

app.MapControllers();

app.UseCors("TodoVale");

app.Run();


