#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:UnitOfWorkExtensions
// Guid:f6a7b8c9-d0e1-6789-0123-456789012345
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/9/12 23:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Data.SqlSugar;
using XiHan.Framework.Uow;
using XiHan.Framework.Uow.Abstracts;

namespace XiHan.Framework.Data.Extensions;

/// <summary>
/// 工作单元扩展方法
/// 提供数据访问相关的工作单元功能
/// </summary>
public static class UnitOfWorkExtensions
{
    /// <summary>
    /// 获取SqlSugar数据库上下文
    /// </summary>
    /// <param name="unitOfWork">工作单元</param>
    /// <param name="dbContextFactory">数据库上下文工厂</param>
    /// <returns>SqlSugar数据库上下文</returns>
    public static SqlSugarDbContext GetSqlSugarDbContext(this IUnitOfWork unitOfWork, Func<ISqlSugarDbContext> dbContextFactory)
    {
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(dbContextFactory);

        return (SqlSugarDbContext)unitOfWork.GetOrAddDatabaseApi("SqlSugarDbContext", dbContextFactory);
    }

    /// <summary>
    /// 获取或添加数据库API
    /// </summary>
    /// <param name="unitOfWork">工作单元</param>
    /// <param name="key">键</param>
    /// <param name="factory">工厂方法</param>
    /// <returns>数据库API</returns>
    public static IDatabaseApi GetOrAddDatabaseApi(this IUnitOfWork unitOfWork, string key, Func<IDatabaseApi> factory)
    {
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(factory);

        return unitOfWork.GetOrAddDatabaseApi(key, factory);
    }

    /// <summary>
    /// 检查工作单元是否活跃
    /// </summary>
    /// <param name="unitOfWork">工作单元</param>
    /// <returns>是否活跃</returns>
    public static bool IsActive(this IUnitOfWork unitOfWork)
    {
        ArgumentNullException.ThrowIfNull(unitOfWork);
        return !unitOfWork.IsDisposed && !unitOfWork.IsCompleted;
    }

    /// <summary>
    /// 安全执行操作（确保在工作单元范围内）
    /// </summary>
    /// <param name="unitOfWork">工作单元</param>
    /// <param name="action">操作</param>
    public static void SafeExecute(this IUnitOfWork unitOfWork, Action action)
    {
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(action);

        if (!unitOfWork.IsActive())
        {
            throw new InvalidOperationException("工作单元未活跃，无法执行操作");
        }

        action();
    }

    /// <summary>
    /// 安全执行异步操作（确保在工作单元范围内）
    /// </summary>
    /// <param name="unitOfWork">工作单元</param>
    /// <param name="action">异步操作</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>任务</returns>
    public static async Task SafeExecuteAsync(this IUnitOfWork unitOfWork, Func<CancellationToken, Task> action, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(action);

        if (!unitOfWork.IsActive())
        {
            throw new InvalidOperationException("工作单元未活跃，无法执行操作");
        }

        await action(cancellationToken);
    }

    /// <summary>
    /// 安全执行异步操作并返回结果（确保在工作单元范围内）
    /// </summary>
    /// <typeparam name="T">返回类型</typeparam>
    /// <param name="unitOfWork">工作单元</param>
    /// <param name="func">异步操作</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>操作结果</returns>
    public static async Task<T> SafeExecuteAsync<T>(this IUnitOfWork unitOfWork, Func<CancellationToken, Task<T>> func, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(func);

        if (!unitOfWork.IsActive())
        {
            throw new InvalidOperationException("工作单元未活跃，无法执行操作");
        }

        return await func(cancellationToken);
    }
}
