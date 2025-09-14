#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:SqlSugarRepository
// Guid:f38d2a04-ecf7-4eb1-89ac-91c7ddfa4bce
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2023-11-15 8:45:12
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.DependencyInjection;
using SqlSugar;
using System.Linq.Expressions;
using XiHan.Framework.Uow;

namespace XiHan.Framework.Data.SqlSugar.Repository;

/// <summary>
/// SqlSugar仓储基类
/// </summary>
/// <typeparam name="TEntity">实体类型</typeparam>
public class SqlSugarRepository<TEntity> : ISqlSugarRepository<TEntity> where TEntity : class, new()
{
    private readonly IUnitOfWorkManager _unitOfWorkManager;
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="unitOfWorkManager">工作单元管理器</param>
    /// <param name="serviceProvider">服务提供者</param>
    public SqlSugarRepository(IUnitOfWorkManager unitOfWorkManager, IServiceProvider serviceProvider)
    {
        _unitOfWorkManager = unitOfWorkManager;
        _serviceProvider = serviceProvider;
        DbContext = (SqlSugarDbContext)CurrentUnitOfWork.GetOrAddDatabaseApi(
            "SqlSugarDbContext",
            () => _serviceProvider.GetRequiredService<ISqlSugarDbContext>());
    }

    /// <summary>
    /// 数据库上下文提供者
    /// </summary>
    protected SqlSugarDbContext DbContext { get; }

    /// <summary>
    /// SqlSugar客户端
    /// </summary>
    protected ISqlSugarClient DbClient => DbContext.GetClient();

    /// <summary>
    /// 实体查询器
    /// </summary>
    protected ISugarQueryable<TEntity> Entities => DbClient.Queryable<TEntity>();

    /// <summary>
    /// 工作单元管理器
    /// </summary>
    protected IUnitOfWorkManager UnitOfWorkManager => _unitOfWorkManager;

    /// <summary>
    /// 当前工作单元
    /// </summary>
    protected IUnitOfWork CurrentUnitOfWork => _unitOfWorkManager.Current;

    /// <summary>
    /// 获取实体查询器
    /// </summary>
    /// <returns></returns>
    public virtual ISugarQueryable<TEntity> GetQueryable()
    {
        return Entities;
    }

    /// <summary>
    /// 获取第一个或默认实体
    /// </summary>
    /// <param name="predicate">条件表达式</param>
    /// <returns></returns>
    public virtual TEntity First(Expression<Func<TEntity, bool>> predicate)
    {
        return Entities.First(predicate);
    }

    /// <summary>
    /// 异步获取第一个或默认实体
    /// </summary>
    /// <param name="predicate">条件表达式</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    public virtual Task<TEntity> FirstAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default)
    {
        return Entities.FirstAsync(predicate, cancellationToken);
    }

    /// <summary>
    /// 获取实体列表
    /// </summary>
    /// <param name="predicate">条件表达式</param>
    /// <returns></returns>
    public virtual List<TEntity> GetList(Expression<Func<TEntity, bool>>? predicate = null)
    {
        return predicate == null ? Entities.ToList() : Entities.Where(predicate).ToList();
    }

    /// <summary>
    /// 异步获取实体列表
    /// </summary>
    /// <param name="predicate">条件表达式</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    public virtual Task<List<TEntity>> GetListAsync(Expression<Func<TEntity, bool>>? predicate = null, CancellationToken cancellationToken = default)
    {
        return predicate == null ? Entities.ToListAsync(cancellationToken) : Entities.Where(predicate).ToListAsync(cancellationToken);
    }

    /// <summary>
    /// 插入实体
    /// </summary>
    /// <param name="entity">实体</param>
    /// <returns></returns>
    public virtual int Insert(TEntity entity)
    {
        return DbClient.Insertable(entity).ExecuteCommand();
    }

    /// <summary>
    /// 异步插入实体
    /// </summary>
    /// <param name="entity">实体</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    public virtual Task<int> InsertAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        return DbClient.Insertable(entity).ExecuteCommandAsync(cancellationToken);
    }

    /// <summary>
    /// 更新实体
    /// </summary>
    /// <param name="entity">实体</param>
    /// <returns></returns>
    public virtual int Update(TEntity entity)
    {
        return DbClient.Updateable(entity).ExecuteCommand();
    }

    /// <summary>
    /// 异步更新实体
    /// </summary>
    /// <param name="entity">实体</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    public virtual Task<int> UpdateAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        return DbClient.Updateable(entity).ExecuteCommandAsync(cancellationToken);
    }

    /// <summary>
    /// 删除实体
    /// </summary>
    /// <param name="entity">实体</param>
    /// <returns></returns>
    public virtual int Delete(TEntity entity)
    {
        return DbClient.Deleteable(entity).ExecuteCommand();
    }

    /// <summary>
    /// 异步删除实体
    /// </summary>
    /// <param name="entity">实体</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    public virtual Task<int> DeleteAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        return DbClient.Deleteable(entity).ExecuteCommandAsync(cancellationToken);
    }

    /// <summary>
    /// 根据条件删除实体
    /// </summary>
    /// <param name="predicate">条件表达式</param>
    /// <returns></returns>
    public virtual int Delete(Expression<Func<TEntity, bool>> predicate)
    {
        return DbClient.Deleteable<TEntity>().Where(predicate).ExecuteCommand();
    }

    /// <summary>
    /// 异步根据条件删除实体
    /// </summary>
    /// <param name="predicate">条件表达式</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    public virtual Task<int> DeleteAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default)
    {
        return DbClient.Deleteable<TEntity>().Where(predicate).ExecuteCommandAsync(cancellationToken);
    }

    /// <summary>
    /// 获取计数
    /// </summary>
    /// <param name="predicate">条件表达式</param>
    /// <returns></returns>
    public virtual int Count(Expression<Func<TEntity, bool>>? predicate = null)
    {
        return predicate == null ? Entities.Count() : Entities.Count(predicate);
    }

    /// <summary>
    /// 异步获取计数
    /// </summary>
    /// <param name="predicate">条件表达式</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    public virtual Task<int> CountAsync(Expression<Func<TEntity, bool>>? predicate = null, CancellationToken cancellationToken = default)
    {
        return predicate == null ? Entities.CountAsync(cancellationToken) : Entities.CountAsync(predicate, cancellationToken);
    }
}
