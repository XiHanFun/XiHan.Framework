// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Application.Contracts.Dtos;
using XiHan.Framework.Application.Contracts.Services;
using XiHan.Framework.Domain.Entities;
using XiHan.Framework.Domain.Entities.Abstracts;
using XiHan.Framework.Domain.Repositories;
using XiHan.Framework.Domain.Shared.Paging.Dtos;

namespace XiHan.Framework.Application.Services;

/// <summary>
/// 批量 CRUD 应用服务基类
/// </summary>
/// <typeparam name="TEntity">实体类型</typeparam>
/// <typeparam name="TEntityDto">实体DTO类型</typeparam>
/// <typeparam name="TKey">主键类型</typeparam>
/// <typeparam name="TCreateDto">创建DTO类型</typeparam>
/// <typeparam name="TUpdateDto">更新DTO类型</typeparam>
/// <typeparam name="TPageRequestDto">分页请求DTO类型</typeparam>
public abstract class BatchCrudApplicationServiceBase<TEntity, TEntityDto, TKey, TCreateDto, TUpdateDto, TPageRequestDto>
    : CrudApplicationServiceBase<TEntity, TEntityDto, TKey, TCreateDto, TUpdateDto, TPageRequestDto>, IBatchCrudApplicationService<TEntityDto, TKey, TCreateDto, TUpdateDto, TPageRequestDto>
    where TEntity : EntityBase<TKey>
    where TEntityDto : DtoBase<TKey>
    where TKey : IEquatable<TKey>
    where TCreateDto : CreationDtoBase<TKey>
    where TUpdateDto : UpdateDtoBase<TKey>
    where TPageRequestDto : PageRequestDtoBase
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
    /// 批量创建
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
    /// 批量更新
    /// </summary>
    public virtual async Task<BatchOperationResponse<TEntityDto>> BatchUpdateAsync(BatchUpdateRequest<TUpdateDto> request)
    {
        return await ExecuteBatchOperationAsync(
            request.Items,
            request.ContinueOnError,
            async (item, index) =>
            {
                var basicId = item.Data.BasicId;
                var entity = await Repository.GetByIdAsync(basicId) ?? throw new KeyNotFoundException($"未找到 ID 为 {basicId} 的实体");
                await MapDtoToEntityAsync(item.Data, entity);
                entity = await Repository.UpdateAsync(entity);
                return await MapEntityToDtoAsync(entity);
            },
            index => $"索引 {index} (ID: {request.Items[index].Data.BasicId})"
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
            async (basicId, index) =>
            {
                var entity = await Repository.GetByIdAsync(basicId) ?? throw new KeyNotFoundException($"未找到 ID 为 {basicId} 的实体");
                if (request.SoftDelete && entity is ISoftDelete)
                {
                    var softDeleteRepoType = typeof(ISoftDeleteRepositoryBase<,>).MakeGenericType(typeof(TEntity), typeof(TKey));
                    var softDeleteRepo = ServiceProvider.GetService(softDeleteRepoType);
                    if (softDeleteRepo != null)
                    {
                        await ((dynamic)softDeleteRepo).SoftDeleteAsync((dynamic)entity);
                        return true;
                    }
                }

                await Repository.DeleteAsync(entity);
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
