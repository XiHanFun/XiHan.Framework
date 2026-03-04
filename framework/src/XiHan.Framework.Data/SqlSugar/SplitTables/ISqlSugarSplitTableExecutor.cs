#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ISqlSugarSplitTableExecutor
// Guid:0c6e3ea6-07ca-4476-bf1f-6e0688721322
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/03/05 20:30:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using SqlSugar;

namespace XiHan.Framework.Data.SqlSugar.SplitTables;

/// <summary>
/// SqlSugar 分表执行器
/// </summary>
public interface ISqlSugarSplitTableExecutor
{
    /// <summary>
    /// 是否分表实体
    /// </summary>
    /// <typeparam name="TEntity"></typeparam>
    /// <returns></returns>
    bool IsSplitTableEntity<TEntity>() where TEntity : class, new();

    /// <summary>
    /// 创建查询对象
    /// </summary>
    /// <typeparam name="TEntity"></typeparam>
    /// <param name="dbClient"></param>
    /// <param name="beginTime"></param>
    /// <param name="endTime"></param>
    /// <returns></returns>
    ISugarQueryable<TEntity> CreateQueryable<TEntity>(
        ISqlSugarClient dbClient,
        DateTimeOffset? beginTime = null,
        DateTimeOffset? endTime = null)
        where TEntity : class, new();

    /// <summary>
    /// 写入实体集合
    /// </summary>
    /// <typeparam name="TEntity"></typeparam>
    /// <param name="dbClient"></param>
    /// <param name="entities"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<int> InsertAsync<TEntity>(
        ISqlSugarClient dbClient,
        IReadOnlyCollection<TEntity> entities,
        CancellationToken cancellationToken = default)
        where TEntity : class, new();

    /// <summary>
    /// 更新实体集合
    /// </summary>
    /// <typeparam name="TEntity"></typeparam>
    /// <param name="dbClient"></param>
    /// <param name="entities"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<int> UpdateAsync<TEntity>(
        ISqlSugarClient dbClient,
        IReadOnlyCollection<TEntity> entities,
        CancellationToken cancellationToken = default)
        where TEntity : class, new();
}
