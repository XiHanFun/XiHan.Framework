// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using SqlSugar;
using XiHan.Framework.EventBus.Abstractions.Distributed;
using XiHan.Framework.EventBus.SqlSugar.Entities;
using XiHan.Framework.EventBus.SqlSugar.Mapping;
using XiHan.Framework.EventBus.SqlSugar.Outbox;

namespace XiHan.Framework.EventBus.SqlSugar.Tests;

/// <summary>
/// 多实例按投递目标并发投递测试，需要真实数据库
/// </summary>
/// <remarks>
/// 地址取环境变量 <c>XIHAN_TEST_MYSQL</c>，未设置时整类跳过。
/// </remarks>
[Collection("MySqlEventOutbox")]
public class OutboxTenantTargetConcurrencyTests
{
    private const string SkipReason = "未设置 XIHAN_TEST_MYSQL，跳过真实数据库并发测试。";
    private const long TenantId = 1001;

    private static readonly string? ConnectionString =
        Environment.GetEnvironmentVariable("XIHAN_TEST_MYSQL");

    /// <summary>
    /// 多个实例并发按目标投递时每条事件只投递一次
    /// </summary>
    [Fact]
    public async Task 多实例并发按目标投递时不重复投递()
    {
        Assert.SkipWhen(string.IsNullOrWhiteSpace(ConnectionString), SkipReason);

        const int eventCount = 200;
        const int instanceCount = 4;

        using var setupClient = CreateClient();
        setupClient.CodeFirst.InitTables(typeof(SysEventOutbox));
        await setupClient.Deleteable<SysEventOutbox>().ExecuteCommandAsync();

        var baseTime = DateTime.UtcNow.AddMinutes(-10);
        for (var index = 0; index < eventCount; index++)
        {
            var info = new OutgoingEventInfo(Guid.NewGuid(), "Order.Created", [1], baseTime.AddSeconds(index));
            await setupClient.Insertable(EventOutboxMapper.ToEntity(info)).ExecuteCommandAsync();
        }

        var workers = Enumerable.Range(0, instanceCount).Select(_ => Task.Run(async () =>
        {
            using var client = CreateClient();
            var tenantConfigId = OutboxTestContext.TenantConfigId(TenantId);
            var clients = new Dictionary<string, SqlSugarClient>(StringComparer.Ordinal)
            {
                [OutboxTestContext.MainConfigId] = client,
                [tenantConfigId] = client
            };
            var currentTenant = new FakeCurrentTenant();
            var resolver = new StubClientResolver(clients, [OutboxTestContext.MainConfigId], OutboxTestContext.MainConfigId)
            {
                CurrentLayoutSelector = () => currentTenant.Id == TenantId ? [tenantConfigId] : [OutboxTestContext.MainConfigId]
            };
            var directory = new StubTargetProvider();
            directory.Targets.Add(new OutboxDeliveryTarget(TenantId));

            using var host = new OutboxTenantHost(
                currentTenant, resolver, new AsyncLocalSqlSugarOutboxConnectionScope(), directory, batchSize: 10);

            var idleRounds = 0;
            for (var round = 0; round < 1000 && idleRounds < 3; round++)
            {
                idleRounds = await host.RoundAsync() == 0 ? idleRounds + 1 : 0;
            }

            return host.Bus.Published.Select(item => item.Id).ToList();
        })).ToArray();

        var published = (await Task.WhenAll(workers)).SelectMany(ids => ids).ToList();

        Assert.Equal(eventCount, published.Count);
        Assert.Equal(published.Count, published.Distinct().Count());
        Assert.Equal(0, await setupClient.Queryable<SysEventOutbox>().CountAsync());
    }

    private static SqlSugarClient CreateClient()
    {
        return new SqlSugarClient(new ConnectionConfig
        {
            ConnectionString = ConnectionString,
            DbType = DbType.MySql,
            IsAutoCloseConnection = true
        });
    }
}
