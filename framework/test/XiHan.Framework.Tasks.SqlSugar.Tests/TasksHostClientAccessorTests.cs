// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using SqlSugar;
using XiHan.Framework.Data.SqlSugar.Clients;
using XiHan.Framework.MultiTenancy;
using XiHan.Framework.MultiTenancy.Abstractions;
using XiHan.Framework.Tasks.SqlSugar.Clients;

namespace XiHan.Framework.Tasks.SqlSugar.Tests;

/// <summary>
/// 宿主上下文客户端访问器测试
/// </summary>
public class TasksHostClientAccessorTests
{
    /// <summary>
    /// 在租户上下文中调用时操作期间处于宿主上下文
    /// </summary>
    [Fact]
    public async Task 在租户上下文中调用时操作期间处于宿主上下文()
    {
        using var client = CreateClient();
        var currentTenant = CreateCurrentTenant();
        var resolver = new StubClientResolver(client, currentTenant);
        using var provider = BuildProvider(_ => resolver);
        var accessor = new TasksHostClientAccessor(provider.GetRequiredService<IServiceScopeFactory>(), currentTenant);
        long? tenantDuringOperation = -1;

        using (currentTenant.Change(42))
        {
            await accessor.ExecuteAsync(_ =>
            {
                tenantDuringOperation = currentTenant.Id;
                return Task.CompletedTask;
            });

            Assert.Equal(42L, currentTenant.Id);
        }

        Assert.Null(tenantDuringOperation);
        var observed = Assert.Single(resolver.ObservedTenantIds);
        Assert.Null(observed);
    }

    /// <summary>
    /// 有返回值的操作把结果交回调用方
    /// </summary>
    [Fact]
    public async Task 有返回值的操作把结果交回调用方()
    {
        using var client = CreateClient();
        var currentTenant = CreateCurrentTenant();
        var resolver = new StubClientResolver(client, currentTenant);
        using var provider = BuildProvider(_ => resolver);
        var accessor = new TasksHostClientAccessor(provider.GetRequiredService<IServiceScopeFactory>(), currentTenant);

        var result = await accessor.ExecuteAsync(_ => Task.FromResult(7));

        Assert.Equal(7, result);
    }

    /// <summary>
    /// 每次操作都从新作用域解析客户端解析器
    /// </summary>
    [Fact]
    public async Task 每次操作都从新作用域解析客户端解析器()
    {
        using var client = CreateClient();
        var currentTenant = CreateCurrentTenant();
        var resolveCount = 0;
        using var provider = BuildProvider(_ =>
        {
            resolveCount++;
            return new StubClientResolver(client, currentTenant);
        });
        var accessor = new TasksHostClientAccessor(provider.GetRequiredService<IServiceScopeFactory>(), currentTenant);

        await accessor.ExecuteAsync(_ => Task.CompletedTask);
        await accessor.ExecuteAsync(_ => Task.CompletedTask);

        Assert.Equal(2, resolveCount);
    }

    private static SqlSugarClient CreateClient()
    {
        return new SqlSugarClient(new ConnectionConfig
        {
            ConnectionString = "DataSource=:memory:",
            DbType = DbType.Sqlite,
            IsAutoCloseConnection = true
        });
    }

    private static ICurrentTenant CreateCurrentTenant()
    {
        return new CurrentTenant(AsyncLocalCurrentTenantAccessor.Instance);
    }

    private static ServiceProvider BuildProvider(Func<IServiceProvider, ISqlSugarClientResolver> resolverFactory)
    {
        var services = new ServiceCollection();
        services.AddScoped(resolverFactory);
        return services.BuildServiceProvider();
    }
}
