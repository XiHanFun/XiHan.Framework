// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Data.Common;
using Microsoft.Extensions.DependencyInjection;
using SqlSugar;
using XiHan.Framework.Testing;

namespace XiHan.Framework.Persistence.SqlSugar.Tests;

/// <summary>
/// 命令超时在真实 PostgreSQL 上真的生效（ADR-0119）
/// </summary>
/// <remarks>
/// 环境变量 <c>XIHAN_TEST_POSTGRES</c>，未设置即跳过。SQLite 上造不出慢语句，这里用 <c>pg_sleep</c>
/// </remarks>
public sealed class PostgresCommandTimeoutTests : IDisposable
{
    /// <summary>
    /// 本测试实例独占的临时库
    /// </summary>
    private PostgresDatabase? _database;

    /// <inheritdoc />
    public void Dispose() => _database?.Dispose();

    [Fact]
    public async Task StatementLongerThanTheTimeout_Fails_AndACallSiteCanRaiseIt()
    {
        _database = PostgresTestServer.CreateDatabase();
        await using var host = await TestHost.StartAsync(
            static builder => builder.UseFeature(PostgresDatabase.CreateFeature()),
            [.. _database.Configuration, new("XiHan:Persistence:SqlSugar:CommandTimeoutSeconds", "1")]);

        await using (var scope = host.Services.CreateAsyncScope())
        {
            var client = scope.ServiceProvider.GetRequiredService<ISqlSugarClient>();
            var thrown = Assert.ThrowsAny<DbException>(() => client.Ado.ExecuteCommand("select pg_sleep(3)"));
            Assert.IsType<TimeoutException>(thrown.InnerException);
        }

        // 个别长任务在调用点临时调大
        await using var longer = host.Services.CreateAsyncScope();
        var patient = longer.ServiceProvider.GetRequiredService<ISqlSugarClient>();
        patient.Ado.CommandTimeOut = 10;
        patient.Ado.ExecuteCommand("select pg_sleep(2)");
    }
}
