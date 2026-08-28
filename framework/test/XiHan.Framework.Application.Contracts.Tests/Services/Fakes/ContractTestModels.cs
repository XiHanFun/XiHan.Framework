// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Application.Contracts.Dtos;
using XiHan.Framework.Application.Contracts.Services;
using XiHan.Framework.Domain.Shared.Paging.Dtos;

namespace XiHan.Framework.Application.Contracts.Tests.Services.Fakes;

/// <summary>
/// 契约测试统一使用的构造类型，避免每个用例重复拼一长串泛型实参
/// </summary>
internal static class ContractTypes
{
    /// <summary>
    /// 构造后的 CRUD 应用服务契约类型
    /// </summary>
    public static Type Crud { get; } =
        typeof(ICrudApplicationService<ContractTestEntityDto, long, ContractTestCreateDto, ContractTestUpdateDto, ContractTestPageRequestDto>);

    /// <summary>
    /// 构造后的批量 CRUD 应用服务契约类型
    /// </summary>
    public static Type BatchCrud { get; } =
        typeof(IBatchCrudApplicationService<ContractTestEntityDto, long, ContractTestCreateDto, ContractTestUpdateDto, ContractTestPageRequestDto>);
}

/// <summary>
/// 契约测试用实体 DTO
/// </summary>
internal sealed class ContractTestEntityDto : DtoBase<long>
{
    /// <summary>
    /// 名称
    /// </summary>
    public string Name { get; set; } = string.Empty;
}

/// <summary>
/// 契约测试用创建 DTO
/// </summary>
internal sealed class ContractTestCreateDto : CreationDtoBase<long>
{
    /// <summary>
    /// 名称
    /// </summary>
    public string Name { get; set; } = string.Empty;
}

/// <summary>
/// 契约测试用更新 DTO
/// </summary>
internal sealed class ContractTestUpdateDto : UpdateDtoBase<long>
{
    /// <summary>
    /// 名称
    /// </summary>
    public string Name { get; set; } = string.Empty;
}

/// <summary>
/// 契约测试用分页请求 DTO
/// </summary>
internal sealed class ContractTestPageRequestDto : PageRequestDtoBase
{
}

/// <summary>
/// 契约测试用批量 CRUD 应用服务
/// </summary>
/// <remarks>
/// 手写内存实现，用来验证 <see cref="IBatchCrudApplicationService{TEntityDto, TKey, TCreateDto, TUpdateDto, TPageRequestDto}"/>
/// 这套泛型约束在真实 DTO 基类下确实可落地，并且能把 Domain.Shared 的分页元数据正确回填到响应里。
/// 不连任何外部依赖。
/// </remarks>
internal sealed class FakeBatchCrudApplicationService
    : IBatchCrudApplicationService<ContractTestEntityDto, long, ContractTestCreateDto, ContractTestUpdateDto, ContractTestPageRequestDto>
{
    private readonly Dictionary<long, ContractTestEntityDto> _store = [];
    private long _lastId;

    /// <summary>
    /// 获取单个，不存在时返回 null
    /// </summary>
    public Task<ContractTestEntityDto?> GetByIdAsync(long id)
    {
        _store.TryGetValue(id, out var dto);
        return Task.FromResult<ContractTestEntityDto?>(dto);
    }

    /// <summary>
    /// 分页，页码与每页大小取自请求的分页元数据
    /// </summary>
    public Task<PageResultDtoBase<ContractTestEntityDto>> PageAsync(ContractTestPageRequestDto input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var ordered = _store.Values.OrderBy(item => item.BasicId).ToList();
        var skip = (input.Page.PageIndex - 1) * input.Page.PageSize;
        var items = ordered.Skip(skip).Take(input.Page.PageSize).ToList();

        return Task.FromResult(PageResultDtoBase<ContractTestEntityDto>.Create(items, input, ordered.Count));
    }

    /// <summary>
    /// 创建，主键由服务端分配
    /// </summary>
    public Task<ContractTestEntityDto> CreateAsync(ContractTestCreateDto input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var dto = new ContractTestEntityDto
        {
            BasicId = ++_lastId,
            Name = input.Name
        };
        _store[dto.BasicId] = dto;

        return Task.FromResult(dto);
    }

    /// <summary>
    /// 更新，主键来自更新 DTO 自身
    /// </summary>
    public Task<ContractTestEntityDto> UpdateAsync(ContractTestUpdateDto input)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (!_store.TryGetValue(input.BasicId, out var stored))
        {
            throw new KeyNotFoundException($"未找到主键为 {input.BasicId} 的记录。");
        }

        stored.Name = input.Name;
        return Task.FromResult(stored);
    }

    /// <summary>
    /// 删除，返回是否真的删掉了
    /// </summary>
    public Task<bool> DeleteAsync(long id)
    {
        return Task.FromResult(_store.Remove(id));
    }

    /// <summary>
    /// 批量获取，缺失的主键被静默跳过
    /// </summary>
    public Task<List<ContractTestEntityDto>> BatchGetAsync(List<long> ids)
    {
        ArgumentNullException.ThrowIfNull(ids);

        var found = new List<ContractTestEntityDto>();
        foreach (var id in ids)
        {
            if (_store.TryGetValue(id, out var stored))
            {
                found.Add(stored);
            }
        }

        return Task.FromResult(found);
    }

    /// <summary>
    /// 批量创建，名称为空视为该项失败
    /// </summary>
    public async Task<BatchOperationResponse<ContractTestEntityDto>> BatchCreateAsync(BatchOperationRequest<ContractTestCreateDto> request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var response = new BatchOperationResponse<ContractTestEntityDto>
        {
            TotalCount = request.Items.Count
        };

        for (var index = 0; index < request.Items.Count; index++)
        {
            var item = request.Items[index];
            if (string.IsNullOrWhiteSpace(item.Name))
            {
                AddFailure(response, index, "NameRequired", "名称不能为空");
                if (!request.ContinueOnError)
                {
                    break;
                }

                continue;
            }

            var created = await CreateAsync(item);
            response.SuccessCount++;
            response.Results.Add(new BatchOperationResult<ContractTestEntityDto>
            {
                Index = index,
                IsSuccess = true,
                Data = created
            });
        }

        return response;
    }

    /// <summary>
    /// 批量更新，主键不存在视为该项失败
    /// </summary>
    public Task<BatchOperationResponse<ContractTestEntityDto>> BatchUpdateAsync(BatchUpdateRequest<ContractTestUpdateDto> request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var response = new BatchOperationResponse<ContractTestEntityDto>
        {
            TotalCount = request.Items.Count
        };

        for (var index = 0; index < request.Items.Count; index++)
        {
            var data = request.Items[index].Data;
            if (data is null || !_store.TryGetValue(data.BasicId, out var stored))
            {
                AddFailure(response, index, "NotFound", "记录不存在");
                if (!request.ContinueOnError)
                {
                    break;
                }

                continue;
            }

            stored.Name = data.Name;
            response.SuccessCount++;
            response.Results.Add(new BatchOperationResult<ContractTestEntityDto>
            {
                Index = index,
                IsSuccess = true,
                Data = stored
            });
        }

        return Task.FromResult(response);
    }

    /// <summary>
    /// 批量删除，软删除标记只影响是否真正移除
    /// </summary>
    public Task<BatchOperationResponse<bool>> BatchDeleteAsync(BatchDeleteRequest<long> request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var response = new BatchOperationResponse<bool>
        {
            TotalCount = request.Ids.Count
        };

        for (var index = 0; index < request.Ids.Count; index++)
        {
            var id = request.Ids[index];
            if (!_store.ContainsKey(id))
            {
                response.FailureCount++;
                response.Errors.Add($"第 {index} 项记录不存在");
                response.Results.Add(new BatchOperationResult<bool>
                {
                    Index = index,
                    IsSuccess = false,
                    Data = false,
                    ErrorCode = "NotFound",
                    ErrorMessage = "记录不存在"
                });

                if (!request.ContinueOnError)
                {
                    break;
                }

                continue;
            }

            if (!request.SoftDelete)
            {
                _store.Remove(id);
            }

            response.SuccessCount++;
            response.Results.Add(new BatchOperationResult<bool>
            {
                Index = index,
                IsSuccess = true,
                Data = true
            });
        }

        return Task.FromResult(response);
    }

    /// <summary>
    /// 当前已存储的记录数（仅测试断言使用）
    /// </summary>
    public int StoredCount => _store.Count;

    private static void AddFailure(
        BatchOperationResponse<ContractTestEntityDto> response,
        int index,
        string errorCode,
        string errorMessage)
    {
        response.FailureCount++;
        response.Errors.Add($"第 {index} 项{errorMessage}");
        response.Results.Add(new BatchOperationResult<ContractTestEntityDto>
        {
            Index = index,
            IsSuccess = false,
            ErrorCode = errorCode,
            ErrorMessage = errorMessage
        });
    }
}
