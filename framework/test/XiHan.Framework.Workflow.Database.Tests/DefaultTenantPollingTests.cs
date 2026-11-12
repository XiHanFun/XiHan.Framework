// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using XiHan.Framework.MultiTenancy.InMemory;
using XiHan.Framework.Workflow.Database.Polling;

namespace XiHan.Framework.Workflow.Database.Tests;

/// <summary>
/// 配了缺省租户的宿主里轮询平台分区（ADR-0192）
/// </summary>
/// <remarks>
/// 轮询器为平台分区开的调用是有意不带租户的平台调用：<c>DefaultTenant</c> 与 <c>RequireTenant</c> 只补外部调用里缺的租户，不把平台调用改成缺省租户的调用
/// </remarks>
public sealed class DefaultTenantPollingTests
{
    [Theory]
    [InlineData("XiHan:MultiTenancy:DefaultTenant", "acme")]
    [InlineData("XiHan:MultiTenancy:RequireTenant", "true")]
    public async Task PlatformPartition_IsPolledAsThePlatform_OnSqlite(string key, string value)
    {
        var logs = new LogCapture();
        await using var database = new SqliteDatabase();
        var host = await database.StartHostAsync(
            [new(key, value), new("XiHan:MultiTenancy:Tenants:acme:DisplayName", "Acme")],
            services => services.AddSingleton<ILoggerFactory>(logs),
            static b => b.UseFeature<InMemoryMultiTenancyFeature>());
        var loop = host.Services.GetRequiredService<DueBookmarkPollingLoop>();

        Assert.Equal(0, await loop.RunCycleAsync(CancellationToken.None));

        // 修之前平台分区的扫描以缺省租户的工作单元执行，存储发现分区不符即抛出；或因没有租户被拒绝（MULTITENANCY_VALIDATION_002）
        Assert.DoesNotContain(logs.Entries, static entry => entry.Level >= LogLevel.Warning && entry.Category.StartsWith("XiHan.Framework.Workflow", StringComparison.Ordinal));
    }
}
