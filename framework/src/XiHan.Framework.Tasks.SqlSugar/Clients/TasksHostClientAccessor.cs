// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using SqlSugar;
using XiHan.Framework.Data.SqlSugar.Clients;
using XiHan.Framework.MultiTenancy.Abstractions;

namespace XiHan.Framework.Tasks.SqlSugar.Clients;

/// <summary>
/// 任务存储的数据库访问器，在宿主上下文中取得默认布局主库的客户端执行操作
/// </summary>
/// <remarks>
/// 每次操作新建一个服务作用域解析 <see cref="ISqlSugarClientResolver"/>；
/// 操作执行期间当前租户切换为宿主，操作结束后恢复。
/// 存在事务型环境工作单元时，客户端由解析器登记进该工作单元。
/// </remarks>
public sealed class TasksHostClientAccessor
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ICurrentTenant _currentTenant;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="scopeFactory">服务作用域工厂</param>
    /// <param name="currentTenant">当前租户</param>
    public TasksHostClientAccessor(IServiceScopeFactory scopeFactory, ICurrentTenant currentTenant)
    {
        _scopeFactory = scopeFactory;
        _currentTenant = currentTenant;
    }

    /// <summary>
    /// 在宿主上下文中执行有返回值的数据库操作
    /// </summary>
    /// <typeparam name="TResult">返回值类型</typeparam>
    /// <param name="operation">数据库操作</param>
    /// <returns>操作的返回值</returns>
    public async Task<TResult> ExecuteAsync<TResult>(Func<ISqlSugarClient, Task<TResult>> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);

        using var scope = _scopeFactory.CreateScope();

        using (_currentTenant.Change(null))
        {
            var client = scope.ServiceProvider
                .GetRequiredService<ISqlSugarClientResolver>()
                .GetCurrentClient();

            return await operation(client);
        }
    }

    /// <summary>
    /// 在宿主上下文中执行无返回值的数据库操作
    /// </summary>
    /// <param name="operation">数据库操作</param>
    /// <returns>任务</returns>
    public async Task ExecuteAsync(Func<ISqlSugarClient, Task> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);

        await ExecuteAsync(async client =>
        {
            await operation(client);
            return true;
        });
    }
}
