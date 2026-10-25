// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using XiHan.Framework.Auditing.Abstractions.Pipeline;
using XiHan.Framework.Auditing.Abstractions.Trail;
using XiHan.Framework.Kernel.Diagnostics;
using XiHan.Framework.Primitives;
using XiHan.Framework.Testing;

namespace XiHan.Framework.Auditing.InMemory.Tests;

/// <summary>
/// 内存实现特有的行为：容量淘汰、特性装配、选项解析。
/// </summary>
public sealed class InMemoryAuditingTests
{
    [Fact]
    public async Task Capacity_EvictsOldest_AndTokensStayValid()
    {
        var trail = new InMemoryAuditTrail(new InMemoryAuditingOptions { Capacity = 3, LogEvents = false }, NullLoggerFactory.Instance);
        for (var i = 1; i <= 5; i++)
        {
            await trail.WriteAsync(new AuditEvent(DateTimeOffset.UnixEpoch.AddMinutes(i), "a" + i, Result.Success(), "t"), CancellationToken.None);
        }

        Assert.Equal((3, 3), (trail.Count, trail.Capacity));
        var first = (await trail.QueryAsync(new AuditQuery { PageSize = 2 }, CancellationToken.None)).ValueOrThrow();
        Assert.Equal(["a3", "a4"], first.Events.Select(static e => e.Action));
        Assert.NotNull(first.ContinuationToken);

        // 续页期间再写入并淘汰：令牌仍指向正确的位置
        await trail.WriteAsync(new AuditEvent(DateTimeOffset.UnixEpoch.AddMinutes(6), "a6", Result.Success(), "t"), CancellationToken.None);
        var second = (await trail.QueryAsync(new AuditQuery { PageSize = 2, ContinuationToken = first.ContinuationToken }, CancellationToken.None)).ValueOrThrow();
        Assert.Equal(["a5", "a6"], second.Events.Select(static e => e.Action));
        Assert.Null(second.ContinuationToken);
    }

    [Fact]
    public async Task Feature_BindsSinkAndTrail_AndRecordsExplicitActions()
    {
        await using var host = await TestHost.StartAsync(
            static b => b.UseFeature<InMemoryAuditingFeature>(),
            [new("XiHan:Auditing:InMemory:Capacity", "50"), new("XiHan:Auditing:InMemory:LogEvents", "false")]);
        var trail = host.Services.GetRequiredService<InMemoryAuditTrail>();
        Assert.Same(trail, host.Services.GetRequiredService<IAuditSink>());
        Assert.Same(trail, host.Services.GetRequiredService<IAuditTrail>());
        Assert.Equal(50, trail.Capacity);
        Assert.Equal(["auditing.trail"], host.App.Graph["xihan.auditing.inmemory"].SelectedCapabilities);
        Assert.Contains(nameof(AuditMiddleware), host.App.DescribePipeline(), StringComparison.Ordinal);

        Assert.True((await host.InvokeAsync(static ctx =>
        {
            ctx.SetAuditAction("reports.rebuild");
            ctx.AddAuditDetail("rows", 3);
            return ValueTask.CompletedTask;
        }, baggage: new Dictionary<string, string> { [BaggageKeys.Principal] = "ops" })).IsSuccess);
        var page = (await trail.QueryAsync(new AuditQuery { Action = "reports.rebuild" }, CancellationToken.None)).ValueOrThrow();
        var recorded = Assert.Single(page.Events);
        Assert.Equal(("ops", 3), (recorded.PrincipalId, recorded.Details?["rows"]));
    }

    [Fact]
    public void Options_ValidateAndParse()
    {
        Assert.Throws<ArgumentOutOfRangeException>(static () => new InMemoryAuditingOptions { Capacity = 0 });
        var options = InMemoryAuditingOptions.FromConfiguration(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["XiHan:Auditing:InMemory:Capacity"] = "7",
            ["XiHan:Auditing:InMemory:LogEvents"] = "false",
        }).Build());
        Assert.Equal((7, false), (options.Capacity, options.LogEvents));
        Assert.Throws<ArgumentException>(static () => InMemoryAuditingOptions.FromConfiguration(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["XiHan:Auditing:InMemory:Capacity"] = "-1" }).Build()));
        Assert.Throws<ArgumentException>(static () => InMemoryAuditingOptions.FromConfiguration(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["XiHan:Auditing:InMemory:LogEvents"] = "yes" }).Build()));
    }
}
