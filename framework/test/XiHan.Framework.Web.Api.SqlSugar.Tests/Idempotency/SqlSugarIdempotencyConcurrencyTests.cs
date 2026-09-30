// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using SqlSugar;
using XiHan.Framework.Uow;
using XiHan.Framework.Web.Api.Idempotency;
using XiHan.Framework.Web.Api.SqlSugar.Entities;
using XiHan.Framework.Web.Api.SqlSugar.Idempotency;

namespace XiHan.Framework.Web.Api.SqlSugar.Tests.Idempotency;

/// <summary>
/// SqlSugar 幂等存储在 MySQL 上的并发测试，未设置 XIHAN_TEST_MYSQL 时跳过
/// </summary>
public class SqlSugarIdempotencyConcurrencyTests
{
    private const string ConnectionStringVariable = "XIHAN_TEST_MYSQL";

    /// <summary>
    /// 两个客户端的 50 个并发请求抢同一个键，只有一个取得
    /// </summary>
    [Fact]
    public async Task 两个MySQL客户端并发取得同一键只有一个成功()
    {
        var connectionString = Environment.GetEnvironmentVariable(ConnectionStringVariable);
        Assert.SkipWhen(string.IsNullOrWhiteSpace(connectionString), $"未设置 {ConnectionStringVariable}，跳过 MySQL 并发测试");

        using var firstClient = CreateClient(connectionString!);
        using var secondClient = CreateClient(connectionString!);
        firstClient.CodeFirst.InitTables(typeof(SysIdempotencyRecord));
        await using var provider = IdempotencyStoreTestContext.BuildUnitOfWorkProvider();
        var unitOfWorkManager = provider.GetRequiredService<IUnitOfWorkManager>();
        var options = Microsoft.Extensions.Options.Options.Create(new XiHanIdempotencyOptions());
        var stores = new[]
        {
            new SqlSugarIdempotencyStore(new StubClientResolver(firstClient), unitOfWorkManager, options, TimeProvider.System),
            new SqlSugarIdempotencyStore(new StubClientResolver(secondClient), unitOfWorkManager, options, TimeProvider.System)
        };
        var key = new IdempotencyRecordKey(string.Empty, "42", "POST", "/api/orders", $"mysql-{Guid.NewGuid():N}");
        var keyHash = key.ComputeHash();

        try
        {
            using var gate = new ManualResetEventSlim(false);
            var tasks = Enumerable.Range(0, 50)
                .Select(index => Task.Factory.StartNew(() =>
                {
                    gate.Wait();
                    return stores[index % 2].TryAcquireAsync(key, "fp-a", isTransactional: true).GetAwaiter().GetResult();
                }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default))
                .ToArray();
            gate.Set();
            var results = await Task.WhenAll(tasks);

            Assert.Single(results, result => result.Status == IdempotencyAcquireStatus.Acquired);
            Assert.Equal(49, results.Count(result => result.Status == IdempotencyAcquireStatus.InProgress));
        }
        finally
        {
            await firstClient.Deleteable<SysIdempotencyRecord>()
                .Where(record => record.KeyHash == keyHash)
                .ExecuteCommandAsync();
        }
    }

    private static SqlSugarScope CreateClient(string connectionString)
    {
        return new SqlSugarScope(new ConnectionConfig
        {
            ConnectionString = connectionString,
            DbType = DbType.MySql,
            IsAutoCloseConnection = true
        });
    }
}
