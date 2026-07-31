using GpsEmitter;
using GpsEmitter.Models;

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<EmitterOptions>(
    builder.Configuration.GetSection(EmitterOptions.SectionName));

builder.Services.AddHostedService<GpsEmitterWorker>();

IHost host = builder.Build();
await host.RunAsync();
