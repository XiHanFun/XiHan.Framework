#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:CrudApplicationServiceBase
// Guid:108d8506-b544-42f5-99c4-ddc499bd7f68
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/10/24 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Mapster;
using Microsoft.AspNetCore.Mvc;
using System.Linq.Expressions;
using XiHan.Framework.Application.Contracts.Services;
using XiHan.Framework.Domain.Entities.Abstracts;
using XiHan.Framework.Domain.Repositories;
using XiHan.Framework.Domain.Shared.Paging.Dtos;

namespace XiHan.Framework.Application.Services;

/// <summary>
/// CRUD 应用服务基类（支持创建和更新DTO分离）
/// </summary>
/// <typeparam name="TEntity">实体类型</typeparam>
/// <typeparam name="TEntityDto">实体DTO类型</typeparam>
/// <typeparam name="TKey">主键类型</typeparam>
/// <typeparam name="TCreateDto">创建DTO类型</typeparam>
/// <typeparam name="TUpdateDto">更新DTO类型</typeparam>
/// <typeparam name="TPageRequestDto">分页请求DTO类型</typeparam>
public abstract class CrudApplicationServiceBase<TEntity, TEntityDto, TKey, TCreateDto, TUpdateDto, TPageRequestDto>
    : ApplicationServiceBase, ICrudApplicationService<TEntityDto, TKey, TCreateDto, TUpdateDto, TPageRequestDto>
    where TEntity : class, IEntityBase<TKey>
    where TEntityDto : class
    where TKey : IEquatable<TKey>
    where TCreateDto : class
    where TUpdateDto : class
    where TPageRequestDto : PageRequestDtoBase
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
    [HttpGet]
    public virtual async Task<TEntityDto?> GetByIdAsync(TKey id)
    {
        var entity = await Repository.GetByIdAsync(id);
        return entity == null ? null : await MapEntityToDtoAsync(entity);
    }

    /// <summary>
    /// 获取分页列表
    /// </summary>
    /// <param name="input">分页查询参数</param>
    /// <returns>分页响应</returns>
    [HttpPost]
    public virtual async Task<PageResultDtoBase<TEntityDto>> GetPageAsync(TPageRequestDto input)
    {
        // 构建额外的过滤表达式（子类可重写以添加额外过滤逻辑）
        var additionalPredicate = BuildAdditionalFilterPredicate(input);

        // 使用仓储层的 PageQuery 重载方法，直接处理 Filters、Sorts 和 DisablePaging
        var entityPageResponse = additionalPredicate != null
            ? await Repository.GetPagedAutoAsync(input, additionalPredicate)
            : await Repository.GetPagedAutoAsync(input);

        // 映射实体到 DTO
        var dtos = await MapEntitiesToDtosAsync(entityPageResponse.Items);

        return new PageResultDtoBase<TEntityDto>(dtos, entityPageResponse.PageResultMetadata);
    }

    /// <summary>
    /// 创建实体
    /// </summary>
    /// <param name="input">创建DTO</param>
    /// <returns>创建后的实体DTO</returns>
    [HttpPost]
    public virtual async Task<TEntityDto> CreateAsync(TCreateDto input)
    {
        var entity = await MapDtoToEntityAsync(input);
        entity = await Repository.AddAsync(entity);
        return await MapEntityToDtoAsync(entity);
    }

    /// <summary>
    /// 更新实体
    /// </summary>
    /// <param name="id">实体主键</param>
    /// <param name="input">更新DTO</param>
    /// <returns>更新后的实体DTO</returns>
    [HttpPut]
    public virtual async Task<TEntityDto> UpdateAsync(TKey id, TUpdateDto input)
    {
        var entity = await Repository.GetByIdAsync(id) ??
            throw new KeyNotFoundException($"未找到 ID 为 {id} 的实体");
        await MapDtoToEntityAsync(input, entity);
        entity = await Repository.UpdateAsync(entity);
        return await MapEntityToDtoAsync(entity);
    }

    /// <summary>
    /// 删除实体
    /// </summary>
    /// <param name="id">实体主键</param>
    /// <returns>删除结果</returns>
    [HttpDelete]
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
    /// 构建额外的过滤谓词表达式
    /// </summary>
    /// <param name="input">分页查询参数</param>
    /// <returns>额外的过滤谓词，如果没有则返回 null</returns>
    /// <remarks>
    /// 子类可以重写此方法以添加额外的过滤逻辑，例如基于当前用户权限的数据过滤。
    /// PageQuery 中的 Filters 和 Sorts 会由仓储层自动处理，此方法用于添加额外的业务逻辑过滤。
    /// </remarks>
    protected virtual Expression<Func<TEntity, bool>>? BuildAdditionalFilterPredicate(PageRequestDtoBase input)
    {
        // 子类可以重写此方法以添加额外的过滤逻辑，例如：权限过滤、租户过滤等
        return null;
    }

    /// <summary>
    /// 映射实体到DTO
    /// </summary>
    /// <param name="entity">实体</param>
    /// <returns>DTO</returns>
    protected virtual Task<TEntityDto> MapEntityToDtoAsync(TEntity entity)
    {
        return Task.FromResult(entity.Adapt<TEntityDto>());
    }

    /// <summary>
    /// 映射实体列表到DTO列表
    /// </summary>
    /// <param name="entities">实体列表</param>
    /// <returns>DTO列表</returns>
    protected virtual async Task<IList<TEntityDto>> MapEntitiesToDtosAsync(IEnumerable<TEntity> entities)
    {
        return await Task.FromResult(entities.Adapt<IList<TEntityDto>>());
    }

    /// <summary>
    /// 映射DTO到实体
    /// </summary>
    /// <param name="dto">DTO</param>
    /// <returns>实体</returns>
    protected virtual Task<TEntity> MapDtoToEntityAsync(TEntityDto dto)
    {
        return Task.FromResult(dto.Adapt<TEntity>());
    }

    /// <summary>
    /// 映射DTO到现有实体
    /// </summary>
    /// <param name="dto">DTO</param>
    /// <param name="entity">实体</param>
    protected virtual Task MapDtoToEntityAsync(TEntityDto dto, TEntity entity)
    {
        entity = dto.Adapt<TEntity>();
        return Task.CompletedTask;
    }

    /// <summary>
    /// 映射创建DTO到实体
    /// </summary>
    /// <param name="createDto">创建DTO</param>
    /// <returns>实体</returns>
    protected virtual Task<TEntity> MapDtoToEntityAsync(TCreateDto createDto)
    {
        return Task.FromResult(createDto.Adapt<TEntity>());
    }

    /// <summary>
    /// 映射更新DTO到现有实体
    /// </summary>
    /// <param name="updateDto">更新DTO</param>
    /// <param name="entity">实体</param>
    protected virtual Task MapDtoToEntityAsync(TUpdateDto updateDto, TEntity entity)
    {
        entity = updateDto.Adapt<TEntity>();
        return Task.CompletedTask;
    }
}
