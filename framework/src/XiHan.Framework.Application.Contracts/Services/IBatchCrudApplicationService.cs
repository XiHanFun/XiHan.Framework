#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IBatchCrudApplicationService
// Guid:m3n4o5p6-q7r8-4s9t-0u1v-2w3x4y5z6a7b
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/10/24 0:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Application.Contracts.Dtos;

namespace XiHan.Framework.Application.Contracts.Services;

/// <summary>
/// 批量 CRUD 应用服务接口（支持创建和更新DTO分离）
/// </summary>
/// <typeparam name="TEntityDto">实体DTO类型</typeparam>
/// <typeparam name="TKey">主键类型</typeparam>
/// <typeparam name="TCreateDto">创建DTO类型</typeparam>
/// <typeparam name="TUpdateDto">更新DTO类型</typeparam>
public interface IBatchCrudApplicationService<TEntityDto, TKey, TCreateDto, TUpdateDto> : ICrudApplicationService<TEntityDto, TKey, TCreateDto, TUpdateDto>
    where TEntityDto : class
    where TKey : IEquatable<TKey>
    where TCreateDto : class
    where TUpdateDto : class
{
    /// <summary>
    /// 批量获取
    /// </summary>
    /// <param name="ids">主键列表</param>
    /// <returns>实体DTO列表</returns>
    Task<List<TEntityDto>> BatchGetAsync(List<TKey> ids);

    /// <summary>
    /// 批量创建
    /// </summary>
    /// <param name="request">批量创建请求</param>
    /// <returns>批量操作响应</returns>
    Task<BatchOperationResponse<TEntityDto>> BatchCreateAsync(BatchOperationRequest<TCreateDto> request);

    /// <summary>
    /// 批量更新
    /// </summary>
    /// <param name="request">批量更新请求</param>
    /// <returns>批量操作响应</returns>
    Task<BatchOperationResponse<TEntityDto>> BatchUpdateAsync(BatchUpdateRequest<TKey, TUpdateDto> request);

    /// <summary>
    /// 批量删除
    /// </summary>
    /// <param name="request">批量删除请求</param>
    /// <returns>批量操作响应</returns>
    Task<BatchOperationResponse<bool>> BatchDeleteAsync(BatchDeleteRequest<TKey> request);
}
