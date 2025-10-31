#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:CrudApplicationServiceBase
// Guid:d4e5f6g7-h8i9-4j0k-1l2m-3n4o5p6q7r8s
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/10/24 0:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Linq.Expressions;
using XiHan.Framework.Domain.Entities.Abstracts;
using XiHan.Framework.Domain.Paging.Dtos;
using XiHan.Framework.Domain.Repositories;
using XiHan.Framework.Utils.Linq.Expressions;

namespace XiHan.Framework.Application.Services;

/// <summary>
/// CRUD 应用服务基类
/// </summary>
/// <typeparam name="TEntity">实体类型</typeparam>
/// <typeparam name="TEntityDto">实体DTO类型</typeparam>
/// <typeparam name="TKey">主键类型</typeparam>
public abstract class CrudApplicationServiceBase<TEntity, TEntityDto, TKey> : ApplicationServiceBase, ICrudApplicationService<TEntityDto, TKey>
    where TEntity : class, IEntityBase<TKey>
    where TEntityDto : class
    where TKey : IEquatable<TKey>
{
    /// <summary>
    /// 仓储
    /// </summary>
    protected readonly IRepositoryBase<TEntity, TKey> Repository;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="repository">仓储</param>
    protected CrudApplicationServiceBase(IRepositoryBase<TEntity, TKey> repository)
    {
        Repository = repository;
    }

    /// <summary>
    /// 获取单个实体
    /// </summary>
    /// <param name="id">实体主键</param>
    /// <returns>实体DTO</returns>
    public virtual async Task<TEntityDto?> GetByIdAsync(TKey id)
    {
        var entity = await Repository.GetByIdAsync(id);
        return entity == null ? null : await MapToEntityDtoAsync(entity);
    }

    /// <summary>
    /// 获取分页列表
    /// </summary>
    /// <param name="input">分页查询参数</param>
    /// <returns>分页响应</returns>
    public virtual async Task<PageResponse<TEntityDto>> GetPageResponseAsync(PageQuery input)
    {
        // 第一步：使用 ExpressionBuilder 构建所有的过滤条件表达式
        var expressionBuilder = new ExpressionBuilder<TEntity>();

        // 构建基础过滤表达式
        var predicate = BuildFilterPredicate(expressionBuilder, input.Filters);

        // 应用自定义过滤表达式
        predicate = await BuildCustomFilterPredicateAsync(expressionBuilder, input, predicate);

        // 第二步：执行查询，获取满足条件的实体总数
        var totalCount = predicate != null
            ? await Repository.CountAsync(predicate)
            : await Repository.CountAsync();

        // 如果没有数据，直接返回空结果
        if (totalCount == 0)
        {
            return new PageResponse<TEntityDto>([], input.PageIndex, input.PageSize, 0);
        }

        // 获取满足条件的实体列表（如果有过滤条件）
        var entities = predicate != null
            ? await Repository.GetListAsync(predicate)
            : await Repository.GetListAsync();

        // 第三步：在内存中应用排序和分页，注意：子类可以重写此方法以在数据库层面进行排序和分页（推荐使用 Specification 模式）
        entities = ApplySortingInMemory(entities, input.Sorts);

        // 应用分页（如果未禁用）
        if (!input.DisablePaging)
        {
            var pageInfo = input.ToPageInfo();
            entities = entities.Skip(pageInfo.Skip).Take(pageInfo.Take);
        }

        // 转换为DTO
        var dtos = await MapToEntityDtosAsync(entities);

        // 构建分页响应
        var pageData = new PageData(input.PageIndex, input.PageSize, (int)totalCount);
        return new PageResponse<TEntityDto>(dtos, pageData);
    }

    /// <summary>
    /// 创建实体
    /// </summary>
    /// <param name="input">创建DTO</param>
    /// <returns>创建后的实体DTO</returns>
    public virtual async Task<TEntityDto> CreateAsync(TEntityDto input)
    {
        var entity = await MapToEntityAsync(input);
        entity = await Repository.AddAsync(entity);
        return await MapToEntityDtoAsync(entity);
    }

    /// <summary>
    /// 更新实体
    /// </summary>
    /// <param name="id">实体主键</param>
    /// <param name="input">更新DTO</param>
    /// <returns>更新后的实体DTO</returns>
    public virtual async Task<TEntityDto> UpdateAsync(TKey id, TEntityDto input)
    {
        var entity = await Repository.GetByIdAsync(id);
        if (entity == null)
        {
            throw new KeyNotFoundException($"未找到 ID 为 {id} 的实体");
        }

        await MapToEntityAsync(input, entity);
        entity = await Repository.UpdateAsync(entity);
        return await MapToEntityDtoAsync(entity);
    }

    /// <summary>
    /// 删除实体
    /// </summary>
    /// <param name="id">实体主键</param>
    /// <returns>删除结果</returns>
    public virtual async Task<bool> DeleteAsync(TKey id)
    {
        var entity = await Repository.GetByIdAsync(id);
        if (entity == null)
        {
            return false;
        }

        await Repository.DeleteAsync(entity);
        return true;
    }

    /// <summary>
    /// 构建过滤谓词表达式
    /// </summary>
    /// <param name="expressionBuilder">表达式构建器</param>
    /// <param name="filters">过滤条件列表</param>
    /// <returns>组合后的过滤谓词，如果没有过滤条件则返回 null</returns>
    protected virtual Expression<Func<TEntity, bool>>? BuildFilterPredicate(ExpressionBuilder<TEntity> expressionBuilder, List<SelectCondition>? filters)
    {
        if (filters == null || filters.Count == 0)
        {
            return null;
        }

        // 使用 PredicateComposer 组合多个过滤条件
        var predicate = PredicateComposer.True<TEntity>();

        foreach (var filter in filters)
        {
            var filterExpression = BuildSingleFilterExpression(filter, expressionBuilder);
            if (filterExpression != null)
            {
                predicate = predicate.And(filterExpression);
            }
        }

        return predicate;
    }

    /// <summary>
    /// 构建单个过滤条件的表达式
    /// </summary>
    /// <param name="condition">过滤条件</param>
    /// <param name="expressionBuilder">表达式构建器</param>
    /// <returns>过滤表达式</returns>
    protected virtual Expression<Func<TEntity, bool>>? BuildSingleFilterExpression(SelectCondition condition, ExpressionBuilder<TEntity> expressionBuilder)
    {
        // 子类可以重写此方法以实现自定义过滤逻辑
        // 默认实现可以使用 ExpressionBuilder 构建基本的相等性比较
        return null;
    }

    /// <summary>
    /// 构建自定义过滤谓词表达式
    /// </summary>
    /// <param name="expressionBuilder">表达式构建器</param>
    /// <param name="input">查询参数</param>
    /// <param name="currentPredicate">当前的过滤谓词</param>
    /// <returns>组合后的过滤谓词</returns>
    protected virtual Task<Expression<Func<TEntity, bool>>?> BuildCustomFilterPredicateAsync(ExpressionBuilder<TEntity> expressionBuilder, PageQuery input, Expression<Func<TEntity, bool>>? currentPredicate)
    {
        // 子类可以重写此方法以实现额外的自定义过滤逻辑
        // 使用 expressionBuilder 构建条件，然后与 currentPredicate 组合
        return Task.FromResult(currentPredicate);
    }

    /// <summary>
    /// 在内存中应用排序
    /// </summary>
    /// <param name="entities">实体集合</param>
    /// <param name="sorts">排序条件列表</param>
    /// <returns>应用排序后的实体集合</returns>
    protected virtual IEnumerable<TEntity> ApplySortingInMemory(IEnumerable<TEntity> entities, List<SortCondition>? sorts)
    {
        if (sorts == null || sorts.Count == 0)
        {
            return entities;
        }

        // 子类可以重写此方法以实现自定义排序逻辑
        // 默认实现：可以使用反射或表达式树来实现动态排序
        // 这里提供一个基础实现框架
        return entities;
    }

    /// <summary>
    /// 映射实体到DTO
    /// </summary>
    /// <param name="entity">实体</param>
    /// <returns>DTO</returns>
    protected abstract Task<TEntityDto> MapToEntityDtoAsync(TEntity entity);

    /// <summary>
    /// 映射实体列表到DTO列表
    /// </summary>
    /// <param name="entities">实体列表</param>
    /// <returns>DTO列表</returns>
    protected virtual async Task<IReadOnlyList<TEntityDto>> MapToEntityDtosAsync(IEnumerable<TEntity> entities)
    {
        var tasks = entities.Select(MapToEntityDtoAsync);
        return (await Task.WhenAll(tasks)).ToList();
    }

    /// <summary>
    /// 映射DTO到实体
    /// </summary>
    /// <param name="dto">DTO</param>
    /// <returns>实体</returns>
    protected abstract Task<TEntity> MapToEntityAsync(TEntityDto dto);

    /// <summary>
    /// 映射DTO到现有实体
    /// </summary>
    /// <param name="dto">DTO</param>
    /// <param name="entity">实体</param>
    protected abstract Task MapToEntityAsync(TEntityDto dto, TEntity entity);
}

/// <summary>
/// CRUD 应用服务基类（支持创建和更新DTO分离）
/// </summary>
/// <typeparam name="TEntity">实体类型</typeparam>
/// <typeparam name="TEntityDto">实体DTO类型</typeparam>
/// <typeparam name="TKey">主键类型</typeparam>
/// <typeparam name="TCreateDto">创建DTO类型</typeparam>
/// <typeparam name="TUpdateDto">更新DTO类型</typeparam>
public abstract class CrudApplicationServiceBase<TEntity, TEntityDto, TKey, TCreateDto, TUpdateDto> : CrudApplicationServiceBase<TEntity, TEntityDto, TKey>, ICrudApplicationService<TEntityDto, TKey, TCreateDto, TUpdateDto>
    where TEntity : class, IEntityBase<TKey>
    where TEntityDto : class
    where TKey : IEquatable<TKey>
    where TCreateDto : class
    where TUpdateDto : class
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="repository">仓储</param>
    protected CrudApplicationServiceBase(IRepositoryBase<TEntity, TKey> repository) : base(repository)
    {
    }

    /// <summary>
    /// 创建实体
    /// </summary>
    /// <param name="input">创建DTO</param>
    /// <returns>创建后的实体DTO</returns>
    public virtual async Task<TEntityDto> CreateAsync(TCreateDto input)
    {
        var entity = await MapToEntityAsync(input);
        entity = await Repository.AddAsync(entity);
        return await MapToEntityDtoAsync(entity);
    }

    /// <summary>
    /// 更新实体
    /// </summary>
    /// <param name="id">实体主键</param>
    /// <param name="input">更新DTO</param>
    /// <returns>更新后的实体DTO</returns>
    public virtual async Task<TEntityDto> UpdateAsync(TKey id, TUpdateDto input)
    {
        var entity = await Repository.GetByIdAsync(id);
        if (entity == null)
        {
            throw new KeyNotFoundException($"未找到 ID 为 {id} 的实体");
        }

        await MapToEntityAsync(input, entity);
        entity = await Repository.UpdateAsync(entity);
        return await MapToEntityDtoAsync(entity);
    }

    /// <summary>
    /// 映射创建DTO到实体
    /// </summary>
    /// <param name="createDto">创建DTO</param>
    /// <returns>实体</returns>
    protected abstract Task<TEntity> MapToEntityAsync(TCreateDto createDto);

    /// <summary>
    /// 映射更新DTO到现有实体
    /// </summary>
    /// <param name="updateDto">更新DTO</param>
    /// <param name="entity">实体</param>
    protected abstract Task MapToEntityAsync(TUpdateDto updateDto, TEntity entity);
}
