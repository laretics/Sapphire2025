using BenchmarkDotNet.Attributes;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Sapphire2025Models.Authentication;
using Sapphire2025Server.Comunications;
using Sapphire2025Server.Controllers;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.VSDiagnostics;

namespace Sapphire2025Server.Benchmarks;
[CPUUsageDiagnoser]
public class ChangesUsersBenchmark
{
    private IConfiguration mvarConfig = default!;
    private SapphireAeneasController mvarController = default!;
    [GlobalSetup]
    public Task Setup()
    {
        mvarConfig = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:RemoteConnection"] = "server=88.99.33.109;port=4406;database=zafiro;user=zafiroextern;password=zafiroextern2233;" }).Build();
        mvarController = new SapphireAeneasController(mvarConfig, new NullHubContext<SignalRHub>(), NullLogger<SapphireAeneasController>.Instance);
        return Task.CompletedTask;
    }

    [Benchmark]
    public Task<Dictionary<Guid, UserModel>> ProjectChangesUsers()
    {
        return mvarController.ChangesUsers();
    }
}