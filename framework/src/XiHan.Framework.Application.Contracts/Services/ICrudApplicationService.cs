#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ICrudApplicationService
// Guid:27598b7d-60db-4228-b1e6-37837ea3c705
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/10/24 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Domain.Shared.Paging.Dtos;

namespace XiHan.Framework.Application.Contracts.Services;

/// <summary>
/// CRUD 应用服务接口（支持创建和更新DTO分离）
/// </summary>
/// <typeparam name="TEntityDto">实体DTO类型</typeparam>
/// <typeparam name="TKey">主键类型</typeparam>
/// <typeparam name="TCreateDto">创建DTO类型</typeparam>
/// <typeparam name="TUpdateDto">更新DTO类型</typeparam>
/// <typeparam name="TPageRequestDto">分页请求DTO类型</typeparam>
public interface ICrudApplicationService<TEntityDto, TKey, TCreateDto, TUpdateDto, TPageRequestDto> : IApplicationService
    where TEntityDto : class
    where TKey : IEquatable<TKey>
    where TCreateDto : class
    where TUpdateDto : class
    where TPageRequestDto : PageRequestDtoBase
{
    /// <summary>
    /// 获取单个
    /// </summary>
    /// <param name="id">实体主键</param>
    /// <returns>实体DTO</returns>
    Task<TEntityDto?> GetByIdAsync(TKey id);

    /// <summary>
    /// 分页
    /// </summary>
    /// <param name="input">分页查询参数</param>
    /// <returns>分页响应</returns>
    Task<PageResultDtoBase<TEntityDto>> PageAsync(TPageRequestDto input);

    /// <summary>
    /// 创建
    /// </summary>
    /// <param name="input">创建DTO</param>
    /// <returns>创建后的实体DTO</returns>
    Task<TEntityDto> CreateAsync(TCreateDto input);

    /// <summary>
    /// 更新
    /// </summary>
    /// <param name="id">实体主键</param>
    /// <param name="input">更新DTO</param>
    /// <returns>更新后的实体DTO</returns>
    Task<TEntityDto> UpdateAsync(TKey id, TUpdateDto input);

    /// <summary>
    /// 删除
    /// </summary>
    /// <param name="id">实体主键</param>
    /// <returns>删除结果</returns>
    Task<bool> DeleteAsync(TKey id);
}
