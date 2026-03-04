#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:SqlSugarSplitTableExecutor
// Guid:900ef47d-42c7-4e6e-950f-c37c7d277d8e
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/03/05 20:30:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using SqlSugar;
using XiHan.Framework.Data.SqlSugar.Helpers;

namespace XiHan.Framework.Data.SqlSugar.SplitTables;

/// <summary>
/// SqlSugar 分表执行器
/// </summary>
public sealed class SqlSugarSplitTableExecutor : ISqlSugarSplitTableExecutor
{
    /// <inheritdoc />
    public bool IsSplitTableEntity<TEntity>() where TEntity : class, new()
    {
        return SqlSugarEntityTypeHelper.IsSplitTableEntity<TEntity>();
    }

    /// <inheritdoc />
    public ISugarQueryable<TEntity> CreateQueryable<TEntity>(
        ISqlSugarClient dbClient,
        DateTimeOffset? beginTime = null,
        DateTimeOffset? endTime = null)
        where TEntity : class, new()
    {
        ArgumentNullException.ThrowIfNull(dbClient);

        var queryable = dbClient.Queryable<TEntity>();
        if (!IsSplitTableEntity<TEntity>())
        {
            return queryable;
        }

        if (beginTime.HasValue && endTime.HasValue)
        {
            return queryable.SplitTable(beginTime.Value.UtcDateTime, endTime.Value.UtcDateTime);
        }

        return queryable.SplitTable();
    }

    /// <inheritdoc />
    public async Task<int> InsertAsync<TEntity>(
        ISqlSugarClient dbClient,
        IReadOnlyCollection<TEntity> entities,
        CancellationToken cancellationToken = default)
        where TEntity : class, new()
    {
        ArgumentNullException.ThrowIfNull(dbClient);
        ArgumentNullException.ThrowIfNull(entities);

        if (entities.Count == 0)
        {
            return 0;
        }

        cancellationToken.ThrowIfCancellationRequested();
        var entityList = entities as List<TEntity> ?? entities.ToList();

        if (IsSplitTableEntity<TEntity>())
        {
            return await dbClient.Insertable(entityList).SplitTable().ExecuteCommandAsync();
        }

        return await dbClient.Insertable(entityList).ExecuteCommandAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<int> UpdateAsync<TEntity>(
        ISqlSugarClient dbClient,
        IReadOnlyCollection<TEntity> entities,
        CancellationToken cancellationToken = default)
        where TEntity : class, new()
    {
        ArgumentNullException.ThrowIfNull(dbClient);
        ArgumentNullException.ThrowIfNull(entities);

        if (entities.Count == 0)
        {
            return 0;
        }

        cancellationToken.ThrowIfCancellationRequested();
        var entityList = entities as List<TEntity> ?? entities.ToList();

        if (IsSplitTableEntity<TEntity>())
        {
            return await dbClient.Updateable(entityList).SplitTable().ExecuteCommandAsync();
        }

        return await dbClient.Updateable(entityList).ExecuteCommandAsync(cancellationToken);
    }
}
