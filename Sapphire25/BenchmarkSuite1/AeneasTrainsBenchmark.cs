using BenchmarkDotNet.Attributes;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Sapphire2025Models.Aeneas;
using Sapphire2025Server.Comunications;
using Sapphire2025Server.Controllers;
using Sapphire2026.Data;
using Sapphire2026.Data.Models;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Sapphire2025Server.Benchmarks;

[MemoryDiagnoser]
public class AeneasTrainsBenchmark
{
    private IConfiguration mvarConfig = default!;
    private SapphireAeneasController mvarController = default!;

    [GlobalSetup]
    public async Task Setup()
    {
        mvarConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:RemoteConnection"] = "server=88.99.33.109;port=4406;database=zafiro;user=zafiroextern;password=zafiroextern2233;"
            })
            .Build();

        using DataStorage almacen = new DataStorage(mvarConfig);
        _ = await almacen.Trains.AsNoTracking().CountAsync();
        mvarController = new SapphireAeneasController(mvarConfig, new NullHubContext<SignalRHub>(), NullLogger<SapphireAeneasController>.Instance);
    }

    [Benchmark]
    public async Task<List<TrainModel>> ProjectTrainList()
    {
        return await mvarController.TrainsRequest();
    }
}

internal sealed class NullHubContext<THub> : IHubContext<THub> where THub : Hub
{
    public IHubClients Clients { get; } = new NullHubClients();
    public IGroupManager Groups { get; } = new NullGroupManager();
}

internal sealed class NullHubClients : IHubClients
{
    public IClientProxy All => NullClientProxy.Instance;
    public IClientProxy AllExcept(IReadOnlyList<string> excludedConnectionIds) => NullClientProxy.Instance;
    public IClientProxy Client(string connectionId) => NullClientProxy.Instance;
    public IClientProxy Clients(IReadOnlyList<string> connectionIds) => NullClientProxy.Instance;
    public IClientProxy Group(string groupName) => NullClientProxy.Instance;
    public IClientProxy GroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds) => NullClientProxy.Instance;
    public IClientProxy Groups(IReadOnlyList<string> groupNames) => NullClientProxy.Instance;
    public IClientProxy User(string userId) => NullClientProxy.Instance;
    public IClientProxy Users(IReadOnlyList<string> userIds) => NullClientProxy.Instance;
}

internal sealed class NullGroupManager : IGroupManager
{
    public Task AddToGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task RemoveFromGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default) => Task.CompletedTask;
}

internal sealed class NullClientProxy : IClientProxy
{
    public static readonly NullClientProxy Instance = new();
    public Task SendCoreAsync(string method, object?[] args, CancellationToken cancellationToken = default) => Task.CompletedTask;
}