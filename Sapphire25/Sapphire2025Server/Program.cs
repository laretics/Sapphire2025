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


app.UseCors("TodoVale");

app.UseAuthorization();

app.MapControllers();

app.Run();