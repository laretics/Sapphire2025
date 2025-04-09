using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Sapphire25Server.Storage;
//using Zafiro25.Models.Authentication;
// Servidor

var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("RemoteConnection") ?? throw new
	InvalidOperationException("Connection string 'DefaultConnection' not found.");

addServices(builder.Services,connectionString); // Add services to the container.

//builder.Services.AddAuthentication("MontefaroAuthenticator")
//	.AddScheme<AuthenticationSchemeOptions, MontefaroAuthenticator>("MontefaroAuthenticator", null);
//builder.Services.AddAuthentication();

	var app = builder.Build();
init(app);


app.Run();

void addServices(IServiceCollection ss, string connectionString)
{
	//ss.AddAuthentication("BasicAuthentication")
	//	.AddScheme<AuthenticationSchemeOptions, BasicAuthenticationHandler>
	//	("BasicAuthentication", null);
	ss.AddAuthorization(options =>
	{
		options.AddPolicy("BasicPolicy", policy =>
		{
			policy.RequireAuthenticatedUser();
		});
	});

	ss.AddControllers();
	// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
	ss.AddEndpointsApiExplorer();
	ss.AddSwaggerGen();
	ss.AddDbContextFactory<DataStorage>(options =>
		options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));

	ss.AddDbContext<DataStorage>(options =>
		options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));
}
void init(WebApplication app)
{
	if (app.Environment.IsDevelopment())
	{
		app.UseSwagger();
		app.UseSwaggerUI();
		app.UseDeveloperExceptionPage();
	}
	app.UseHttpsRedirection();

	app.UseAuthentication();
	app.UseAuthorization();
	app.MapControllers();
}