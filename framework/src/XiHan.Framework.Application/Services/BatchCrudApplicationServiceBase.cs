//#region <<版权版本注释>>

//// ----------------------------------------------------------------
//// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
//// Licensed under the MIT License. See LICENSE in the project root for license information.
//// FileName:BatchCrudApplicationServiceBase
//// Guid:n4o5p6q7-r8s9-4t0u-1v2w-3x4y5z6a7b8c
//// Author:zhaifanhua
//// Email:me@zhaifanhua.com
//// CreateTime:2025/10/24 0:00:00
//// ----------------------------------------------------------------

//#endregion <<版权版本注释>>

//using XiHan.Framework.Domain.Entities.Abstracts;
//using XiHan.Framework.Domain.Repositories;

//namespace XiHan.Framework.Application.Services;

///// <summary>
///// 批量 CRUD 应用服务基类
///// </summary>
///// <typeparam name="TEntity">实体类型</typeparam>
///// <typeparam name="TEntityDto">实体DTO类型</typeparam>
///// <typeparam name="TKey">主键类型</typeparam>
//public abstract class BatchCrudApplicationServiceBase<TEntity, TEntityDto, TKey> : CrudApplicationServiceBase<TEntity, TEntityDto, TKey>, IBatchCrudApplicationService<TEntityDto, TKey>
//    where TEntity : class, IEntityBase<TKey>
//    where TEntityDto : class
//    where TKey : IEquatable<TKey>
//{
//    /// <summary>
//    /// 构造函数
//    /// </summary>
//    /// <param name="repository">仓储</param>
//    protected BatchCrudApplicationServiceBase(IRepositoryBase<TEntity, TKey> repository) : base(repository)
//    {
//    }

//    /// <summary>
//    /// 批量创建
//    /// </summary>
//    public virtual async Task<BatchOperationResponse<TEntityDto>> BatchCreateAsync(BatchOperationRequest<TEntityDto> request)
//    {
//        var response = new BatchOperationResponse<TEntityDto>
//        {
//            TotalCount = request.Items.Count
//        };

//        for (var i = 0; i < request.Items.Count; i++)
//        {
//            var result = new BatchOperationResult<TEntityDto> { Index = i };

//            try
//            {
//                var entity = await MapToEntityAsync(request.Items[i]);
//                entity = await Repository.AddAsync(entity);
//                result.Data = await MapToEntityDtoAsync(entity);
//                result.IsSuccess = true;
//                response.SuccessCount++;
//            }
//            catch (Exception ex)
//            {
//                result.IsSuccess = false;
//                result.ErrorMessage = ex.Message;
//                response.FailureCount++;
//                response.Errors.Add($"索引 {i}: {ex.Message}");

//                if (!request.ContinueOnError)
//                {
//                    break;
//                }
//            }

//            response.Results.Add(result);
//        }

//        return response;
//    }

//    /// <summary>
//    /// 批量更新
//    /// </summary>
//    public virtual async Task<BatchOperationResponse<TEntityDto>> BatchUpdateAsync(BatchUpdateRequest<TKey, TEntityDto> request)
//    {
//        var response = new BatchOperationResponse<TEntityDto>
//        {
//            TotalCount = request.Items.Count
//        };

//        for (var i = 0; i < request.Items.Count; i++)
//        {
//            var result = new BatchOperationResult<TEntityDto> { Index = i };
//            var item = request.Items[i];

//            try
//            {
//                var entity = await Repository.GetByIdAsync(item.Id);
//                if (entity == null)
//                {
//                    throw new KeyNotFoundException($"未找到 ID 为 {item.Id} 的实体");
//                }

//                await MapToEntityAsync(item.Data, entity);
//                entity = await Repository.UpdateAsync(entity);
//                result.Data = await MapToEntityDtoAsync(entity);
//                result.IsSuccess = true;
//                response.SuccessCount++;
//            }
//            catch (Exception ex)
//            {
//                result.IsSuccess = false;
//                result.ErrorMessage = ex.Message;
//                response.FailureCount++;
//                response.Errors.Add($"索引 {i} (ID: {item.Id}): {ex.Message}");

//                if (!request.ContinueOnError)
//                {
//                    break;
//                }
//            }

//            response.Results.Add(result);
//        }

//        return response;
//    }

//    /// <summary>
//    /// 批量删除
//    /// </summary>
//    public virtual async Task<BatchOperationResponse<bool>> BatchDeleteAsync(BatchDeleteRequest<TKey> request)
//    {
//        var response = new BatchOperationResponse<bool>
//        {
//            TotalCount = request.Ids.Count
//        };

//        for (var i = 0; i < request.Ids.Count; i++)
//        {
//            var result = new BatchOperationResult<bool> { Index = i };
//            var id = request.Ids[i];

//            try
//            {
//                var entity = await Repository.GetByIdAsync(id);
//                if (entity == null)
//                {
//                    throw new KeyNotFoundException($"未找到 ID 为 {id} 的实体");
//                }

//                if (request.SoftDelete && entity is IDeletionEntity deletionEntity)
//                {
//                    deletionEntity.IsDeleted = true;
//                    deletionEntity.DeletionTime = DateTimeOffset.UtcNow;
//                    await Repository.UpdateAsync(entity);
//                }
//                else
//                {
//                    await Repository.DeleteAsync(entity);
//                }

//                result.Data = true;
//                result.IsSuccess = true;
//                response.SuccessCount++;
//            }
//            catch (Exception ex)
//            {
//                result.IsSuccess = false;
//                result.Data = false;
//                result.ErrorMessage = ex.Message;
//                response.FailureCount++;
//                response.Errors.Add($"索引 {i} (ID: {id}): {ex.Message}");

//                if (!request.ContinueOnError)
//                {
//                    break;
//                }
//            }

//            response.Results.Add(result);
//        }

//        return response;
//    }

//    /// <summary>
//    /// 批量获取
//    /// </summary>
//    public virtual async Task<List<TEntityDto>> BatchGetAsync(List<TKey> ids)
//    {
//        var entities = await Repository.GetAllAsync(e => ids.Contains(e.BasicId));
//        return (await MapToEntityDtosAsync(entities)).ToList();
//    }
//}

///// <summary>
///// 批量 CRUD 应用服务基类（支持创建和更新DTO分离）
///// </summary>
///// <typeparam name="TEntity">实体类型</typeparam>
///// <typeparam name="TEntityDto">实体DTO类型</typeparam>
///// <typeparam name="TKey">主键类型</typeparam>
///// <typeparam name="TCreateDto">创建DTO类型</typeparam>
///// <typeparam name="TUpdateDto">更新DTO类型</typeparam>
//public abstract class BatchCrudApplicationServiceBase<TEntity, TEntityDto, TKey, TCreateDto, TUpdateDto> : CrudApplicationServiceBase<TEntity, TEntityDto, TKey, TCreateDto, TUpdateDto>, IBatchCrudApplicationService<TEntityDto, TKey, TCreateDto, TUpdateDto>
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
//    protected BatchCrudApplicationServiceBase(IRepositoryBase<TEntity, TKey> repository) : base(repository)
//    {
//    }

//    /// <summary>
//    /// 批量创建
//    /// </summary>
//    public virtual async Task<BatchOperationResponse<TEntityDto>> BatchCreateAsync(BatchOperationRequest<TEntityDto> request)
//    {
//        var response = new BatchOperationResponse<TEntityDto>
//        {
//            TotalCount = request.Items.Count
//        };

//        for (var i = 0; i < request.Items.Count; i++)
//        {
//            var result = new BatchOperationResult<TEntityDto> { Index = i };

//            try
//            {
//                var entity = await MapToEntityAsync(request.Items[i]);
//                entity = await Repository.AddAsync(entity);
//                result.Data = await MapToEntityDtoAsync(entity);
//                result.IsSuccess = true;
//                response.SuccessCount++;
//            }
//            catch (Exception ex)
//            {
//                result.IsSuccess = false;
//                result.ErrorMessage = ex.Message;
//                response.FailureCount++;
//                response.Errors.Add($"索引 {i}: {ex.Message}");

//                if (!request.ContinueOnError)
//                {
//                    break;
//                }
//            }

//            response.Results.Add(result);
//        }

//        return response;
//    }

//    /// <summary>
//    /// 批量创建
//    /// </summary>
//    public virtual async Task<BatchOperationResponse<TEntityDto>> BatchCreateAsync(BatchOperationRequest<TCreateDto> request)
//    {
//        var response = new BatchOperationResponse<TEntityDto>
//        {
//            TotalCount = request.Items.Count
//        };

//        for (var i = 0; i < request.Items.Count; i++)
//        {
//            var result = new BatchOperationResult<TEntityDto> { Index = i };

//            try
//            {
//                var entity = await MapToEntityAsync(request.Items[i]);
//                entity = await Repository.AddAsync(entity);
//                result.Data = await MapToEntityDtoAsync(entity);
//                result.IsSuccess = true;
//                response.SuccessCount++;
//            }
//            catch (Exception ex)
//            {
//                result.IsSuccess = false;
//                result.ErrorMessage = ex.Message;
//                response.FailureCount++;
//                response.Errors.Add($"索引 {i}: {ex.Message}");

//                if (!request.ContinueOnError)
//                {
//                    break;
//                }
//            }

//            response.Results.Add(result);
//        }

//        return response;
//    }

//    /// <summary>
//    /// 批量更新
//    /// </summary>
//    public virtual async Task<BatchOperationResponse<TEntityDto>> BatchUpdateAsync(BatchUpdateRequest<TKey, TEntityDto> request)
//    {
//        var response = new BatchOperationResponse<TEntityDto>
//        {
//            TotalCount = request.Items.Count
//        };

//        for (var i = 0; i < request.Items.Count; i++)
//        {
//            var result = new BatchOperationResult<TEntityDto> { Index = i };
//            var item = request.Items[i];

//            try
//            {
//                var entity = await Repository.GetByIdAsync(item.Id);
//                if (entity == null)
//                {
//                    throw new KeyNotFoundException($"未找到 ID 为 {item.Id} 的实体");
//                }

//                await MapToEntityAsync(item.Data, entity);
//                entity = await Repository.UpdateAsync(entity);
//                result.Data = await MapToEntityDtoAsync(entity);
//                result.IsSuccess = true;
//                response.SuccessCount++;
//            }
//            catch (Exception ex)
//            {
//                result.IsSuccess = false;
//                result.ErrorMessage = ex.Message;
//                response.FailureCount++;
//                response.Errors.Add($"索引 {i} (ID: {item.Id}): {ex.Message}");

//                if (!request.ContinueOnError)
//                {
//                    break;
//                }
//            }

//            response.Results.Add(result);
//        }

//        return response;
//    }

//    /// <summary>
//    /// 批量更新
//    /// </summary>
//    public virtual async Task<BatchOperationResponse<TEntityDto>> BatchUpdateAsync(BatchUpdateRequest<TKey, TUpdateDto> request)
//    {
//        var response = new BatchOperationResponse<TEntityDto>
//        {
//            TotalCount = request.Items.Count
//        };

//        for (var i = 0; i < request.Items.Count; i++)
//        {
//            var result = new BatchOperationResult<TEntityDto> { Index = i };
//            var item = request.Items[i];

//            try
//            {
//                var entity = await Repository.GetByIdAsync(item.Id);
//                if (entity == null)
//                {
//                    throw new KeyNotFoundException($"未找到 ID 为 {item.Id} 的实体");
//                }

//                await MapToEntityAsync(item.Data, entity);
//                entity = await Repository.UpdateAsync(entity);
//                result.Data = await MapToEntityDtoAsync(entity);
//                result.IsSuccess = true;
//                response.SuccessCount++;
//            }
//            catch (Exception ex)
//            {
//                result.IsSuccess = false;
//                result.ErrorMessage = ex.Message;
//                response.FailureCount++;
//                response.Errors.Add($"索引 {i} (ID: {item.Id}): {ex.Message}");

//                if (!request.ContinueOnError)
//                {
//                    break;
//                }
//            }

//            response.Results.Add(result);
//        }

//        return response;
//    }

//    /// <summary>
//    /// 批量删除
//    /// </summary>
//    public virtual async Task<BatchOperationResponse<bool>> BatchDeleteAsync(BatchDeleteRequest<TKey> request)
//    {
//        var response = new BatchOperationResponse<bool>
//        {
//            TotalCount = request.Ids.Count
//        };

//        for (var i = 0; i < request.Ids.Count; i++)
//        {
//            var result = new BatchOperationResult<bool> { Index = i };
//            var id = request.Ids[i];

//            try
//            {
//                var entity = await Repository.GetByIdAsync(id);
//                if (entity == null)
//                {
//                    throw new KeyNotFoundException($"未找到 ID 为 {id} 的实体");
//                }

//                if (request.SoftDelete && entity is IDeletionEntity deletionEntity)
//                {
//                    deletionEntity.IsDeleted = true;
//                    deletionEntity.DeletionTime = DateTimeOffset.UtcNow;
//                    await Repository.UpdateAsync(entity);
//                }
//                else
//                {
//                    await Repository.DeleteAsync(entity);
//                }

//                result.Data = true;
//                result.IsSuccess = true;
//                response.SuccessCount++;
//            }
//            catch (Exception ex)
//            {
//                result.IsSuccess = false;
//                result.Data = false;
//                result.ErrorMessage = ex.Message;
//                response.FailureCount++;
//                response.Errors.Add($"索引 {i} (ID: {id}): {ex.Message}");

//                if (!request.ContinueOnError)
//                {
//                    break;
//                }
//            }

//            response.Results.Add(result);
//        }

//        return response;
//    }

//    /// <summary>
//    /// 批量获取
//    /// </summary>
//    public virtual async Task<List<TEntityDto>> BatchGetAsync(List<TKey> ids)
//    {
//        var entities = await Repository.GetAllAsync(e => ids.Contains(e.BasicId));
//        return (await MapToEntityDtosAsync(entities)).ToList();
//    }
//}
