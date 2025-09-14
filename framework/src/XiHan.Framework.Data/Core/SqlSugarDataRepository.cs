#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:SqlSugarDataRepository
// Guid:c3d4e5f6-a7b8-3456-7890-123456789012
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/9/12 21:30:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.DependencyInjection;
using SqlSugar;
using System.Linq.Expressions;
using XiHan.Framework.Core.DependencyInjection.ServiceLifetimes;
using XiHan.Framework.Data.SqlSugar;
using XiHan.Framework.Domain.Entities.Abstracts;
using XiHan.Framework.Domain.Repositories;
using XiHan.Framework.Domain.Specifications.Abstracts;
using XiHan.Framework.Uow;

namespace XiHan.Framework.Data.Core;

/// <summary>
/// SqlSugar 数据仓储基础实现
/// </summary>
/// <typeparam name="TEntity">实体类型</typeparam>
/// <typeparam name="TKey">主键类型</typeparam>
public class SqlSugarDataRepository<TEntity, TKey> : IDataRepository<TEntity, TKey>, IScopedDependency
    where TEntity : class, IEntityBase<TKey>, new()
    where TKey : IEquatable<TKey>
{
    private readonly IUnitOfWorkManager _unitOfWorkManager;
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="unitOfWorkManager">工作单元管理器</param>
    /// <param name="serviceProvider">服务提供者</param>
    public SqlSugarDataRepository(IUnitOfWorkManager unitOfWorkManager, IServiceProvider serviceProvider)
    {
        _unitOfWorkManager = unitOfWorkManager;
        _serviceProvider = serviceProvider;
    }

    /// <summary>
    /// 数据库上下文
    /// </summary>
    protected SqlSugarDbContext DbContext
    {
        get
        {
            var unitOfWork = _unitOfWorkManager.Current ?? throw new InvalidOperationException("当前没有活动的工作单元");
            return (SqlSugarDbContext)unitOfWork.GetOrAddDatabaseApi(
                "SqlSugarDbContext",
                () => _serviceProvider.GetRequiredService<ISqlSugarDbContext>());
        }
    }

    /// <summary>
    /// SqlSugar客户端
    /// </summary>
    protected ISqlSugarClient DbClient => DbContext.GetClient();

    /// <summary>
    /// 实体查询器
    /// </summary>
    protected ISugarQueryable<TEntity> Entities => DbClient.Queryable<TEntity>();

    /// <summary>
    /// 根据主键查找实体
    /// </summary>
    /// <param name="id">主键</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>实体</returns>
    public virtual async Task<TEntity?> FindByIdAsync(TKey id, CancellationToken cancellationToken = default)
    {
        return await Entities.Where(e => e.BasicId.Equals(id)).FirstAsync(cancellationToken);
    }

    /// <summary>
    /// 根据主键列表查找实体
    /// </summary>
    /// <param name="ids">主键列表</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>实体列表</returns>
    public virtual async Task<IEnumerable<TEntity>> FindByIdsAsync(IEnumerable<TKey> ids, CancellationToken cancellationToken = default)
    {
        var idList = ids.ToList();
        if (idList.Count == 0)
        {
            return [];
        }

        return await Entities.Where(e => idList.Contains(e.BasicId)).ToListAsync(cancellationToken);
    }

    /// <summary>
    /// 根据条件查找单个实体
    /// </summary>
    /// <param name="predicate">查询条件</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>实体</returns>
    public virtual async Task<TEntity?> FindAsync(System.Linq.Expressions.Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        return await Entities.FirstAsync(predicate, cancellationToken);
    }

    /// <summary>
    /// 根据规约查询单个实体
    /// </summary>
    /// <param name="specification">规约</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>实体</returns>
    public virtual async Task<TEntity?> FindAsync(ISpecification<TEntity> specification, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(specification);
        return await Entities.Where(specification.ToExpression()).FirstAsync(cancellationToken);
    }

    /// <summary>
    /// 根据规约查询实体列表
    /// </summary>
    /// <param name="specification">规约</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>实体列表</returns>
    public virtual async Task<IEnumerable<TEntity>> FindListAsync(ISpecification<TEntity> specification, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(specification);
        return await Entities.Where(specification.ToExpression()).ToListAsync(cancellationToken);
    }

    /// <summary>
    /// 根据表达式查询
    /// </summary>
    /// <param name="predicate">查询表达式</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>实体列表</returns>
    public virtual async Task<IEnumerable<TEntity>> FindListAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        return await Entities.Where(predicate).ToListAsync(cancellationToken);
    }

    /// <summary>
    /// 获取所有实体
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>实体列表</returns>
    public virtual async Task<IEnumerable<TEntity>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await Entities.ToListAsync(cancellationToken);
    }

    /// <summary>
    /// 根据条件获取实体集合
    /// </summary>
    /// <param name="predicate">查询条件</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>实体集合</returns>
    public virtual async Task<IEnumerable<TEntity>> GetAllAsync(System.Linq.Expressions.Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        return await Entities.Where(predicate).ToListAsync(cancellationToken);
    }

    /// <summary>
    /// 根据规约获取实体集合
    /// </summary>
    /// <param name="specification">规约</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>实体集合</returns>
    public virtual async Task<IEnumerable<TEntity>> GetAllAsync(ISpecification<TEntity> specification, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(specification);
        return await Entities.Where(specification.ToExpression()).ToListAsync(cancellationToken);
    }

    /// <summary>
    /// 统计实体数量
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>数量</returns>
    public virtual async Task<long> CountAsync(CancellationToken cancellationToken = default)
    {
        return await Entities.CountAsync(cancellationToken);
    }

    /// <summary>
    /// 根据条件统计实体数量
    /// </summary>
    /// <param name="predicate">查询条件</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>数量</returns>
    public virtual async Task<long> CountAsync(System.Linq.Expressions.Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        return await Entities.CountAsync(predicate, cancellationToken);
    }

    /// <summary>
    /// 根据规约统计数量
    /// </summary>
    /// <param name="specification">规约</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>数量</returns>
    public virtual async Task<long> CountAsync(ISpecification<TEntity> specification, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(specification);
        return await Entities.CountAsync(specification.ToExpression(), cancellationToken);
    }

    /// <summary>
    /// 根据条件检查是否存在
    /// </summary>
    /// <param name="predicate">查询条件</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否存在</returns>
    public virtual async Task<bool> AnyAsync(System.Linq.Expressions.Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        return await Entities.AnyAsync(predicate, cancellationToken);
    }

    /// <summary>
    /// 根据规约检查是否存在
    /// </summary>
    /// <param name="specification">规约</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否存在</returns>
    public virtual async Task<bool> AnyAsync(ISpecification<TEntity> specification, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(specification);
        return await Entities.AnyAsync(specification.ToExpression(), cancellationToken);
    }

    /// <summary>
    /// 添加实体
    /// </summary>
    /// <param name="entity">实体</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>添加的实体</returns>
    public virtual async Task<TEntity> AddAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);

        await DbClient.Insertable(entity).ExecuteCommandAsync(cancellationToken);
        return entity;
    }

    /// <summary>
    /// 批量添加实体
    /// </summary>
    /// <param name="entities">实体集合</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>添加的实体集合</returns>
    public virtual async Task<IEnumerable<TEntity>> AddRangeAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entities);

        var entityList = entities.ToList();
        if (entityList.Count == 0)
        {
            return entityList;
        }

        await DbClient.Insertable(entityList).ExecuteCommandAsync(cancellationToken);
        return entityList;
    }

    /// <summary>
    /// 更新实体
    /// </summary>
    /// <param name="entity">实体</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>更新的实体</returns>
    public virtual async Task<TEntity> UpdateAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);

        await DbClient.Updateable(entity).ExecuteCommandAsync(cancellationToken);
        return entity;
    }

    /// <summary>
    /// 批量更新实体
    /// </summary>
    /// <param name="entities">实体集合</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>更新的实体集合</returns>
    public virtual async Task<IEnumerable<TEntity>> UpdateRangeAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entities);

        var entityList = entities.ToList();
        if (entityList.Count == 0)
        {
            return entityList;
        }

        await DbClient.Updateable(entityList).ExecuteCommandAsync(cancellationToken);
        return entityList;
    }

    /// <summary>
    /// 删除实体
    /// </summary>
    /// <param name="entity">实体</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>任务</returns>
    public virtual async Task DeleteAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        await DbClient.Deleteable(entity).ExecuteCommandAsync(cancellationToken);
    }

    /// <summary>
    /// 根据主键删除实体
    /// </summary>
    /// <param name="id">主键</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>任务</returns>
    public virtual async Task DeleteAsync(TKey id, CancellationToken cancellationToken = default)
    {
        await DbClient.Deleteable<TEntity>().Where(e => e.BasicId.Equals(id)).ExecuteCommandAsync(cancellationToken);
    }

    /// <summary>
    /// 批量删除实体
    /// </summary>
    /// <param name="entities">实体集合</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>任务</returns>
    public virtual async Task DeleteRangeAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entities);

        var entityList = entities.ToList();
        if (entityList.Count == 0)
        {
            return;
        }

        await DbClient.Deleteable(entityList).ExecuteCommandAsync(cancellationToken);
    }

    /// <summary>
    /// 批量删除实体（根据主键）
    /// </summary>
    /// <param name="ids">主键集合</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>任务</returns>
    public virtual async Task DeleteRangeAsync(IEnumerable<TKey> ids, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ids);

        var idList = ids.ToList();
        if (idList.Count == 0)
        {
            return;
        }

        await DbClient.Deleteable<TEntity>().Where(e => idList.Contains(e.BasicId)).ExecuteCommandAsync(cancellationToken);
    }

    /// <summary>
    /// 获取可查询的实体集合
    /// </summary>
    /// <returns>可查询的实体集合</returns>
    public virtual IQueryable<TEntity> GetQueryable()
    {
        // SqlSugar的ISugarQueryable实现了IQueryable接口，可以直接强制转换
        return (IQueryable<TEntity>)Entities;
    }

    /// <summary>
    /// 执行原生SQL查询
    /// </summary>
    /// <param name="sql">SQL语句</param>
    /// <param name="parameters">参数</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>实体列表</returns>
    public virtual async Task<IEnumerable<TEntity>> SqlQueryAsync(string sql, object? parameters = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);

        if (parameters == null)
        {
            return await DbClient.SqlQueryable<TEntity>(sql).ToListAsync(cancellationToken);
        }

        return await DbClient.SqlQueryable<TEntity>(sql).AddParameters(parameters).ToListAsync(cancellationToken);
    }
}
