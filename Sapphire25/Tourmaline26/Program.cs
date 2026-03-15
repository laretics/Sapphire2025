using Tourmaline26.Components;
using MonoGameRenderer;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();


//Inicio el renderizador en segundo plano.
builder.Services.AddSingleton<Render2DProcess>();

var app = builder.Build();

//Inicia el motor de render en segundo plano.
var renderer = app.Services.GetRequiredService<Render2DProcess>();
Task.Run(() => renderer.Run());

//Configura el endpoint MJPEG
app.MapGet("/stream", async context =>
{
    context.Response.ContentType = "multipart/x-mixed-replace; boundary=frame";
    var render = app.Services.GetRequiredService<Render2DProcess>();
    while (true)
    {
        var frame = render.LastFrameBuffer;
        if (frame != null)
        {
            await context.Response.WriteAsync("--frame\r\n");
            await context.Response.WriteAsync("Content-Type: image/jpeg\r\n\r\n");
            await context.Response.Body.WriteAsync(frame, 0, frame.Length);
            await context.Response.WriteAsync("\r\n");
            await context.Response.Body.FlushAsync();
        }
        await Task.Delay(33); // ~30 FPS
    }
});

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
