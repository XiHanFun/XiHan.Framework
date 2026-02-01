#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:BatchCrudApplicationServiceBase
// Guid:b8597b60-edd6-42a5-ae64-cdd99f626c7f
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/10/24 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Application.Contracts.Dtos;
using XiHan.Framework.Application.Contracts.Services;
using XiHan.Framework.Domain.Entities.Abstracts;
using XiHan.Framework.Domain.Repositories;

namespace XiHan.Framework.Application.Services;

/// <summary>
/// 批量 CRUD 应用服务基类（支持创建和更新DTO分离）
/// </summary>
/// <typeparam name="TEntity">实体类型</typeparam>
/// <typeparam name="TEntityDto">实体DTO类型</typeparam>
/// <typeparam name="TKey">主键类型</typeparam>
/// <typeparam name="TCreateDto">创建DTO类型</typeparam>
/// <typeparam name="TUpdateDto">更新DTO类型</typeparam>
public abstract class BatchCrudApplicationServiceBase<TEntity, TEntityDto, TKey, TCreateDto, TUpdateDto>
    : CrudApplicationServiceBase<TEntity, TEntityDto, TKey, TCreateDto, TUpdateDto>, IBatchCrudApplicationService<TEntityDto, TKey, TCreateDto, TUpdateDto>
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
    protected BatchCrudApplicationServiceBase(IRepositoryBase<TEntity, TKey> repository) : base(repository)
    {
    }

    /// <summary>
    /// 批量获取
    /// </summary>
    public virtual async Task<List<TEntityDto>> BatchGetAsync(List<TKey> ids)
    {
        if (ids == null || ids.Count == 0)
        {
            return [];
        }

        // 使用仓储的批量获取方法
        var entities = await Repository.GetByIdsAsync(ids);
        return [.. (await MapEntitiesToDtosAsync(entities))];
    }

    /// <summary>
    /// 批量创建（使用 TCreateDto）
    /// </summary>
    public virtual async Task<BatchOperationResponse<TEntityDto>> BatchCreateAsync(BatchOperationRequest<TCreateDto> request)
    {
        return await ExecuteBatchOperationAsync(
            request.Items,
            request.ContinueOnError,
            async (item, index) =>
            {
                var entity = await MapDtoToEntityAsync(item);
                entity = await Repository.AddAsync(entity);
                return await MapEntityToDtoAsync(entity);
            },
            index => $"索引 {index}"
        );
    }

    /// <summary>
    /// 批量更新（使用 TUpdateDto）
    /// </summary>
    public virtual async Task<BatchOperationResponse<TEntityDto>> BatchUpdateAsync(BatchUpdateRequest<TKey, TUpdateDto> request)
    {
        return await ExecuteBatchOperationAsync(
            request.Items,
            request.ContinueOnError,
            async (item, index) =>
            {
                var entity = await Repository.GetByIdAsync(item.Id) ?? throw new KeyNotFoundException($"未找到 ID 为 {item.Id} 的实体");
                await MapDtoToEntityAsync(item.Data, entity);
                entity = await Repository.UpdateAsync(entity);
                return await MapEntityToDtoAsync(entity);
            },
            index => $"索引 {index} (ID: {request.Items[index].Id})"
        );
    }

    /// <summary>
    /// 批量删除
    /// </summary>
    public virtual async Task<BatchOperationResponse<bool>> BatchDeleteAsync(BatchDeleteRequest<TKey> request)
    {
        return await ExecuteBatchOperationAsync(
            request.Ids,
            request.ContinueOnError,
            async (id, index) =>
            {
                var entity = await Repository.GetByIdAsync(id) ?? throw new KeyNotFoundException($"未找到 ID 为 {id} 的实体");
                if (request.SoftDelete && entity is IDeletionEntity deletionEntity)
                {
                    deletionEntity.IsDeleted = true;
                    deletionEntity.DeletedTime = DateTimeOffset.UtcNow;
                    await Repository.UpdateAsync(entity);
                }
                else
                {
                    await Repository.DeleteAsync(entity);
                }

                return true;
            },
            index => $"索引 {index} (ID: {request.Ids[index]})"
        );
    }

    /// <summary>
    /// 执行批量操作的通用方法
    /// </summary>
    /// <typeparam name="TInput">输入类型</typeparam>
    /// <typeparam name="TOutput">输出类型</typeparam>
    /// <param name="items">要处理的项目列表</param>
    /// <param name="continueOnError">出错时是否继续</param>
    /// <param name="operation">要执行的操作</param>
    /// <param name="errorMessageFormatter">错误信息格式化函数</param>
    /// <returns>批量操作响应</returns>
    protected virtual async Task<BatchOperationResponse<TOutput>> ExecuteBatchOperationAsync<TInput, TOutput>(
        IReadOnlyList<TInput> items,
        bool continueOnError,
        Func<TInput, int, Task<TOutput>> operation,
        Func<int, string> errorMessageFormatter)
    {
        var response = new BatchOperationResponse<TOutput>
        {
            TotalCount = items.Count
        };

        for (var i = 0; i < items.Count; i++)
        {
            var result = new BatchOperationResult<TOutput> { Index = i };

            try
            {
                result.Data = await operation(items[i], i);
                result.IsSuccess = true;
                response.SuccessCount++;
            }
            catch (Exception ex)
            {
                result.IsSuccess = false;
                result.ErrorMessage = ex.Message;
                response.FailureCount++;
                response.Errors.Add($"{errorMessageFormatter(i)}: {ex.Message}");

                if (!continueOnError)
                {
                    response.Results.Add(result);
                    break;
                }
            }

            response.Results.Add(result);
        }

        return response;
    }
}
