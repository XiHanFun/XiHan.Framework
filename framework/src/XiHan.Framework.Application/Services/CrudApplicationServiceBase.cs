//#region <<版权版本注释>>

//// ----------------------------------------------------------------
//// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
//// Licensed under the MIT License. See LICENSE in the project root for license information.
//// FileName:CrudApplicationServiceBase
//// Guid:d4e5f6g7-h8i9-4j0k-1l2m-3n4o5p6q7r8s
//// Author:zhaifanhua
//// Email:me@zhaifanhua.com
//// CreateTime:2025/10/24 0:00:00
//// ----------------------------------------------------------------

//#endregion <<版权版本注释>>

//using System.Linq.Expressions;
//using XiHan.Framework.Domain.Entities.Abstracts;
//using XiHan.Framework.Domain.Paging.Dtos;
//using XiHan.Framework.Domain.Repositories;
//using XiHan.Framework.Utils.Linq.Expressions;

//namespace XiHan.Framework.Application.Services;

///// <summary>
///// CRUD 应用服务基类
///// </summary>
///// <typeparam name="TEntity">实体类型</typeparam>
///// <typeparam name="TEntityDto">实体DTO类型</typeparam>
///// <typeparam name="TKey">主键类型</typeparam>
//public abstract class CrudApplicationServiceBase<TEntity, TEntityDto, TKey> : ApplicationServiceBase, ICrudApplicationService<TEntityDto, TKey>
//    where TEntity : class, IEntityBase<TKey>
//    where TEntityDto : class
//    where TKey : IEquatable<TKey>
//{
//    /// <summary>
//    /// 仓储
//    /// </summary>
//    protected readonly IRepositoryBase<TEntity, TKey> Repository;

//    /// <summary>
//    /// 构造函数
//    /// </summary>
//    /// <param name="repository">仓储</param>
//    protected CrudApplicationServiceBase(IRepositoryBase<TEntity, TKey> repository)
//    {
//        Repository = repository;
//    }

//    /// <summary>
//    /// 获取单个实体
//    /// </summary>
//    /// <param name="id">实体主键</param>
//    /// <returns>实体DTO</returns>
//    public virtual async Task<TEntityDto?> GetAsync(TKey id)
//    {
//        var entity = await Repository.GetByIdAsync(id);
//        return entity == null ? null : await MapToEntityDtoAsync(entity);
//    }

//    /// <summary>
//    /// 获取分页列表
//    /// </summary>
//    /// <param name="input">分页查询参数</param>
//    /// <returns>分页响应</returns>
//    public virtual async Task<PageResponse<TEntityDto>> GetPageResponseAsync(PageQuery input)
//    {
//        // 获取基础查询对象
//        var query = await Repository.GetQueryableAsync();

//        // 构建过滤表达式
//        var predicate = BuildFilterPredicate(input.Filters);
//        if (predicate != null)
//        {
//            query = query.Where(predicate);
//        }

//        // 应用自定义过滤
//        query = await ApplyCustomFiltersAsync(query, input);

//        // 应用排序
//        query = ApplySorting(query, input.Sorts);

//        // 计算总数（在分页前）
//        var totalCount = query.LongCount();

//        // 应用分页（如果未禁用）
//        if (!input.DisablePaging)
//        {
//            query = query.Skip(input.ToPageInfo().Skip).Take(input.ToPageInfo().Take);
//        }

//        // 获取数据
//        var entities = query.ToList();
//        var dtos = await MapToEntityDtosAsync(entities);

//        // 构建分页响应
//        var pageData = new PageData(input.PageIndex, input.PageSize, totalCount);
//        return new PageResponse<TEntityDto>(dtos, pageData);
//    }

//    /// <summary>
//    /// 创建实体
//    /// </summary>
//    /// <param name="input">创建DTO</param>
//    /// <returns>创建后的实体DTO</returns>
//    public virtual async Task<TEntityDto> CreateAsync(TEntityDto input)
//    {
//        var entity = await MapToEntityAsync(input);
//        entity = await Repository.AddAsync(entity);
//        return await MapToEntityDtoAsync(entity);
//    }

//    /// <summary>
//    /// 更新实体
//    /// </summary>
//    /// <param name="id">实体主键</param>
//    /// <param name="input">更新DTO</param>
//    /// <returns>更新后的实体DTO</returns>
//    public virtual async Task<TEntityDto> UpdateAsync(TKey id, TEntityDto input)
//    {
//        var entity = await Repository.GetByIdAsync(id);
//        if (entity == null)
//        {
//            throw new KeyNotFoundException($"未找到 ID 为 {id} 的实体");
//        }

//        await MapToEntityAsync(input, entity);
//        entity = await Repository.UpdateAsync(entity);
//        return await MapToEntityDtoAsync(entity);
//    }

//    /// <summary>
//    /// 删除实体
//    /// </summary>
//    /// <param name="id">实体主键</param>
//    /// <returns>删除结果</returns>
//    public virtual async Task<bool> DeleteAsync(TKey id)
//    {
//        var entity = await Repository.GetByIdAsync(id);
//        if (entity == null)
//        {
//            return false;
//        }

//        await Repository.DeleteAsync(entity);
//        return true;
//    }

//    /// <summary>
//    /// 构建过滤谓词表达式
//    /// </summary>
//    /// <param name="filters">过滤条件列表</param>
//    /// <returns>组合后的过滤谓词，如果没有过滤条件则返回 null</returns>
//    protected virtual Expression<Func<TEntity, bool>>? BuildFilterPredicate(List<SelectCondition>? filters)
//    {
//        if (filters == null || filters.Count == 0)
//        {
//            return null;
//        }

//        // 使用 PredicateComposer 组合多个过滤条件
//        var predicate = PredicateComposer.True<TEntity>();

//        foreach (var filter in filters)
//        {
//            var filterExpression = BuildSingleFilterExpression(filter);
//            if (filterExpression != null)
//            {
//                predicate = predicate.And(filterExpression);
//            }
//        }

//        return predicate;
//    }

//    /// <summary>
//    /// 构建单个过滤条件的表达式
//    /// </summary>
//    /// <param name="condition">过滤条件</param>
//    /// <returns>过滤表达式</returns>
//    protected virtual Expression<Func<TEntity, bool>>? BuildSingleFilterExpression(SelectCondition condition)
//    {
//        // 子类可以重写此方法以实现自定义过滤逻辑
//        // 默认实现可以使用 ExpressionBuilder 构建基本的相等性比较
//        return null;
//    }

//    /// <summary>
//    /// 应用自定义过滤
//    /// </summary>
//    /// <param name="query">查询对象</param>
//    /// <param name="input">查询参数</param>
//    /// <returns>应用过滤后的查询对象</returns>
//    protected virtual Task<IQueryable<TEntity>> ApplyCustomFiltersAsync(IQueryable<TEntity> query, PageQuery input)
//    {
//        // 子类可以重写此方法以实现额外的自定义过滤逻辑
//        return Task.FromResult(query);
//    }

//    /// <summary>
//    /// 应用排序
//    /// </summary>
//    /// <param name="query">查询对象</param>
//    /// <param name="sorts">排序条件列表</param>
//    /// <returns>应用排序后的查询对象</returns>
//    protected virtual IQueryable<TEntity> ApplySorting(IQueryable<TEntity> query, List<SortCondition>? sorts)
//    {
//        if (sorts == null || sorts.Count == 0)
//        {
//            return query;
//        }

//        // 子类可以重写此方法以实现自定义排序逻辑
//        // 默认实现：可以使用反射或表达式树来实现动态排序
//        return query;
//    }

//    /// <summary>
//    /// 映射实体到DTO
//    /// </summary>
//    /// <param name="entity">实体</param>
//    /// <returns>DTO</returns>
//    protected abstract Task<TEntityDto> MapToEntityDtoAsync(TEntity entity);

//    /// <summary>
//    /// 映射实体列表到DTO列表
//    /// </summary>
//    /// <param name="entities">实体列表</param>
//    /// <returns>DTO列表</returns>
//    protected virtual async Task<IReadOnlyList<TEntityDto>> MapToEntityDtosAsync(IEnumerable<TEntity> entities)
//    {
//        var tasks = entities.Select(MapToEntityDtoAsync);
//        return (await Task.WhenAll(tasks)).ToList();
//    }

//    /// <summary>
//    /// 映射DTO到实体
//    /// </summary>
//    /// <param name="dto">DTO</param>
//    /// <returns>实体</returns>
//    protected abstract Task<TEntity> MapToEntityAsync(TEntityDto dto);

//    /// <summary>
//    /// 映射DTO到现有实体
//    /// </summary>
//    /// <param name="dto">DTO</param>
//    /// <param name="entity">实体</param>
//    protected abstract Task MapToEntityAsync(TEntityDto dto, TEntity entity);
//}

///// <summary>
///// CRUD 应用服务基类（支持创建和更新DTO分离）
///// </summary>
///// <typeparam name="TEntity">实体类型</typeparam>
///// <typeparam name="TEntityDto">实体DTO类型</typeparam>
///// <typeparam name="TKey">主键类型</typeparam>
///// <typeparam name="TCreateDto">创建DTO类型</typeparam>
///// <typeparam name="TUpdateDto">更新DTO类型</typeparam>
//public abstract class CrudApplicationServiceBase<TEntity, TEntityDto, TKey, TCreateDto, TUpdateDto> : CrudApplicationServiceBase<TEntity, TEntityDto, TKey>, ICrudApplicationService<TEntityDto, TKey, TCreateDto, TUpdateDto>
//    where TEntity : class, IEntityBase<TKey>
//    where TEntityDto : class
//    where TKey : IEquatable<TKey>
//    where TCreateDto : class
//    where TUpdateDto : class
//{
//    /// <summary>
//    /// 构造函数
//    /// </summary>
//    /// <param name="repository">仓储</param>
//    protected CrudApplicationServiceBase(IRepositoryBase<TEntity, TKey> repository) : base(repository)
//    {
//    }

//    /// <summary>
//    /// 创建实体
//    /// </summary>
//    /// <param name="input">创建DTO</param>
//    /// <returns>创建后的实体DTO</returns>
//    public virtual async Task<TEntityDto> CreateAsync(TCreateDto input)
//    {
//        var entity = await MapToEntityAsync(input);
//        entity = await Repository.AddAsync(entity);
//        return await MapToEntityDtoAsync(entity);
//    }

//    /// <summary>
//    /// 更新实体
//    /// </summary>
//    /// <param name="id">实体主键</param>
//    /// <param name="input">更新DTO</param>
//    /// <returns>更新后的实体DTO</returns>
//    public virtual async Task<TEntityDto> UpdateAsync(TKey id, TUpdateDto input)
//    {
//        var entity = await Repository.GetByIdAsync(id);
//        if (entity == null)
//        {
//            throw new KeyNotFoundException($"未找到 ID 为 {id} 的实体");
//        }

//        await MapToEntityAsync(input, entity);
//        entity = await Repository.UpdateAsync(entity);
//        return await MapToEntityDtoAsync(entity);
//    }

//    /// <summary>
//    /// 映射创建DTO到实体
//    /// </summary>
//    /// <param name="createDto">创建DTO</param>
//    /// <returns>实体</returns>
//    protected abstract Task<TEntity> MapToEntityAsync(TCreateDto createDto);

//    /// <summary>
//    /// 映射更新DTO到现有实体
//    /// </summary>
//    /// <param name="updateDto">更新DTO</param>
//    /// <param name="entity">实体</param>
//    protected abstract Task MapToEntityAsync(TUpdateDto updateDto, TEntity entity);
//}
