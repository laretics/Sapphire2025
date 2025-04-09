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
	options.AddPolicy("PolicyName", builder => 
		builder.AllowAnyOrigin().
		AllowAnyHeader().
		AllowAnyMethod());
});

builder.Services.AddSingleton<IConfiguration>(builder.Configuration);

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
	app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.UseCors("PolicyName");

app.Run();


