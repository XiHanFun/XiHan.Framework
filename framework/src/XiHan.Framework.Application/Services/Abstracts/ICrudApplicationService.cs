#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ICrudApplicationService
// Guid:b2c3d4e5-f6g7-4h8i-9j0k-1l2m3n4o5p6q
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/10/24 0:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Domain.Shared.Paging.Dtos;

namespace XiHan.Framework.Application.Services.Abstracts;

/// <summary>
/// CRUD 应用服务接口（支持创建和更新DTO分离）
/// </summary>
/// <typeparam name="TEntityDto">实体DTO类型</typeparam>
/// <typeparam name="TKey">主键类型</typeparam>
/// <typeparam name="TCreateDto">创建DTO类型</typeparam>
/// <typeparam name="TUpdateDto">更新DTO类型</typeparam>
public interface ICrudApplicationService<TEntityDto, TKey, TCreateDto, TUpdateDto> : IApplicationService
    where TEntityDto : class
    where TKey : IEquatable<TKey>
    where TCreateDto : class
    where TUpdateDto : class
{
    /// <summary>
    /// 获取单个实体
    /// </summary>
    /// <param name="id">实体主键</param>
    /// <returns>实体DTO</returns>
    Task<TEntityDto?> GetByIdAsync(TKey id);

    /// <summary>
    /// 获取分页列表
    /// </summary>
    /// <param name="input">分页查询参数</param>
    /// <returns>分页响应</returns>
    Task<PageResponse<TEntityDto>> GetPageAsync(PageQuery input);

    /// <summary>
    /// 创建实体
    /// </summary>
    /// <param name="input">创建DTO</param>
    /// <returns>创建后的实体DTO</returns>
    Task<TEntityDto> CreateAsync(TCreateDto input);

    /// <summary>
    /// 更新实体
    /// </summary>
    /// <param name="id">实体主键</param>
    /// <param name="input">更新DTO</param>
    /// <returns>更新后的实体DTO</returns>
    Task<TEntityDto> UpdateAsync(TKey id, TUpdateDto input);

    /// <summary>
    /// 删除实体
    /// </summary>
    /// <param name="id">实体主键</param>
    /// <returns>删除结果</returns>
    Task<bool> DeleteAsync(TKey id);
}
