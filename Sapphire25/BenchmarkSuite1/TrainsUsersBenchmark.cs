using BenchmarkDotNet.Attributes;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Sapphire2025Models.Authentication;
using Sapphire2025Server.Comunications;
using Sapphire2025Server.Controllers;
using Sapphire2026.Data;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.VSDiagnostics;

namespace Sapphire2025Server.Benchmarks;
[CPUUsageDiagnoser]
public class TrainsUsersBenchmark
{
    private IConfiguration mvarConfig = default!;
    private SapphireAeneasController mvarController = default!;
    [GlobalSetup]
    public async Task Setup()
    {
        mvarConfig = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:RemoteConnection"] = "server=88.99.33.109;port=4406;database=zafiro;user=zafiroextern;password=zafiroextern2233;" }).Build();
        using DataStorage almacen = new DataStorage(mvarConfig);
        _ = await almacen.Trains.AsNoTracking().CountAsync();
        mvarController = new SapphireAeneasController(mvarConfig, new NullHubContext<SignalRHub>(), NullLogger<SapphireAeneasController>.Instance);
    }

    [Benchmark]
    public async Task<Dictionary<Guid, UserModel>> ProjectTrainsUsers()
    {
        return await mvarController.TrainsUsers();
    }
}
