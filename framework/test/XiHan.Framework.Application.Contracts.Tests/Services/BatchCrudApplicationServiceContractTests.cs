// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Application.Contracts.Dtos;
using XiHan.Framework.Application.Contracts.Tests.Services.Fakes;

namespace XiHan.Framework.Application.Contracts.Tests.Services;

/// <summary>
/// 批量 CRUD 应用服务契约测试
/// </summary>
/// <remarks>
/// 批量契约在单条契约之上追加四个方法，并复用 BatchOperationRequest/Response 这组信封。
/// 这里锁定四个方法的签名，并用手写内存实现验证信封的聚合语义：
/// 成功数、失败数、逐项索引与 ContinueOnError 的短路行为。
/// </remarks>
public class BatchCrudApplicationServiceContractTests
{
    /// <summary>
    /// 批量契约继承单条契约，实现方无需重复声明
    /// </summary>
    [Fact]
    public void BatchContract_ExtendsCrudContract()
    {
        Assert.True(ContractTypes.Crud.IsAssignableFrom(ContractTypes.BatchCrud));
        Assert.Contains(ContractTypes.Crud, ContractTypes.BatchCrud.GetInterfaces());
    }

    /// <summary>
    /// 批量契约自身恰好声明四个方法
    /// </summary>
    [Fact]
    public void BatchContract_DeclaresExactlyFourMethods()
    {
        var names = ContractTypes.BatchCrud.GetMethods().Select(method => method.Name).OrderBy(name => name, StringComparer.Ordinal).ToArray();
        string[] expected = ["BatchCreateAsync", "BatchDeleteAsync", "BatchGetAsync", "BatchUpdateAsync"];

        Assert.Equal(expected, names);
    }

    /// <summary>
    /// 四个批量方法的入参与返回类型稳定
    /// </summary>
    /// <remarks>
    /// 注意 BatchDeleteAsync 的响应泛型是 bool 而不是实体 DTO——删除没有实体可回传。
    /// </remarks>
    [Fact]
    public void BatchMethods_SignaturesAreStable()
    {
        var batchGet = ContractTypes.BatchCrud.GetMethod("BatchGetAsync");
        var batchCreate = ContractTypes.BatchCrud.GetMethod("BatchCreateAsync");
        var batchUpdate = ContractTypes.BatchCrud.GetMethod("BatchUpdateAsync");
        var batchDelete = ContractTypes.BatchCrud.GetMethod("BatchDeleteAsync");

        Assert.NotNull(batchGet);
        Assert.NotNull(batchCreate);
        Assert.NotNull(batchUpdate);
        Assert.NotNull(batchDelete);

        Assert.Equal(typeof(Task<List<ContractTestEntityDto>>), batchGet!.ReturnType);
        Assert.Equal(typeof(List<long>), batchGet.GetParameters()[0].ParameterType);

        Assert.Equal(typeof(Task<BatchOperationResponse<ContractTestEntityDto>>), batchCreate!.ReturnType);
        Assert.Equal(typeof(BatchOperationRequest<ContractTestCreateDto>), batchCreate.GetParameters()[0].ParameterType);

        Assert.Equal(typeof(Task<BatchOperationResponse<ContractTestEntityDto>>), batchUpdate!.ReturnType);
        Assert.Equal(typeof(BatchUpdateRequest<ContractTestUpdateDto>), batchUpdate.GetParameters()[0].ParameterType);

        Assert.Equal(typeof(Task<BatchOperationResponse<bool>>), batchDelete!.ReturnType);
        Assert.Equal(typeof(BatchDeleteRequest<long>), batchDelete.GetParameters()[0].ParameterType);
    }

    /// <summary>
    /// 批量创建全部成功时，计数与逐项索引都对齐
    /// </summary>
    [Fact]
    public async Task BatchCreateAsync_WhenAllValid_AggregatesSuccessCounts()
    {
        var service = new FakeBatchCrudApplicationService();
        var request = new BatchOperationRequest<ContractTestCreateDto>
        {
            Items =
            [
                new ContractTestCreateDto { Name = "甲" },
                new ContractTestCreateDto { Name = "乙" },
                new ContractTestCreateDto { Name = "丙" }
            ]
        };

        var response = await service.BatchCreateAsync(request);

        Assert.Equal(3, response.TotalCount);
        Assert.Equal(3, response.SuccessCount);
        Assert.Equal(0, response.FailureCount);
        Assert.True(response.IsAllSuccess);
        Assert.Empty(response.Errors);
        Assert.Equal(3, response.Results.Count);

        int[] expectedIndexes = [0, 1, 2];

        Assert.Equal(expectedIndexes, response.Results.Select(result => result.Index).ToArray());
        Assert.All(response.Results, result => Assert.True(result.IsSuccess));
    }

    /// <summary>
    /// ContinueOnError 为 false 时，首个失败项之后不再继续处理
    /// </summary>
    /// <remarks>
    /// 请求默认就是 false，所以「不填 ContinueOnError」的调用方拿到的是遇错即停语义。
    /// </remarks>
    [Fact]
    public async Task BatchCreateAsync_WhenFailFast_StopsAtFirstFailure()
    {
        var service = new FakeBatchCrudApplicationService();
        var request = new BatchOperationRequest<ContractTestCreateDto>
        {
            Items =
            [
                new ContractTestCreateDto { Name = "甲" },
                new ContractTestCreateDto { Name = "   " },
                new ContractTestCreateDto { Name = "丙" }
            ]
        };

        Assert.False(request.ContinueOnError);

        var response = await service.BatchCreateAsync(request);

        Assert.Equal(3, response.TotalCount);
        Assert.Equal(1, response.SuccessCount);
        Assert.Equal(1, response.FailureCount);
        Assert.False(response.IsAllSuccess);
        Assert.Equal(2, response.Results.Count);
        Assert.Equal(1, service.StoredCount);
        Assert.Equal("NameRequired", response.Results[1].ErrorCode);
    }

    /// <summary>
    /// ContinueOnError 为 true 时，失败项之后的合法项仍会被处理
    /// </summary>
    [Fact]
    public async Task BatchCreateAsync_WhenContinueOnError_ProcessesRemainingItems()
    {
        var service = new FakeBatchCrudApplicationService();
        var request = new BatchOperationRequest<ContractTestCreateDto>
        {
            ContinueOnError = true,
            Items =
            [
                new ContractTestCreateDto { Name = "甲" },
                new ContractTestCreateDto { Name = string.Empty },
                new ContractTestCreateDto { Name = "丙" }
            ]
        };

        var response = await service.BatchCreateAsync(request);

        Assert.Equal(3, response.TotalCount);
        Assert.Equal(2, response.SuccessCount);
        Assert.Equal(1, response.FailureCount);
        Assert.Equal(3, response.Results.Count);
        Assert.Single(response.Errors);
        Assert.Equal(2, service.StoredCount);
    }

    /// <summary>
    /// 空批次是合法输入，返回全零计数且判定为全成功
    /// </summary>
    [Fact]
    public async Task BatchCreateAsync_WithEmptyRequest_ReturnsEmptyResponse()
    {
        var service = new FakeBatchCrudApplicationService();

        var response = await service.BatchCreateAsync(new BatchOperationRequest<ContractTestCreateDto>());

        Assert.Equal(0, response.TotalCount);
        Assert.Equal(0, response.SuccessCount);
        Assert.Equal(0, response.FailureCount);
        Assert.Empty(response.Results);
        Assert.True(response.IsAllSuccess);
    }

    /// <summary>
    /// 批量获取按主键列表取回，缺失主键被跳过而不是补 null 占位
    /// </summary>
    [Fact]
    public async Task BatchGetAsync_SkipsMissingKeys()
    {
        var service = new FakeBatchCrudApplicationService();
        var first = await service.CreateAsync(new ContractTestCreateDto { Name = "甲" });
        var second = await service.CreateAsync(new ContractTestCreateDto { Name = "乙" });

        var found = await service.BatchGetAsync([first.BasicId, 9999L, second.BasicId]);

        Assert.Equal(2, found.Count);
        Assert.DoesNotContain(found, item => item.BasicId == 9999L);
    }

    /// <summary>
    /// 批量更新通过更新项内部的 DTO 主键定位对象
    /// </summary>
    /// <remarks>
    /// BatchUpdateItem 本身不带主键，主键只能来自 TUpdateDto，
    /// 这也是批量更新的泛型参数被约束为 UpdateDtoBase&lt;TKey&gt; 的原因。
    /// </remarks>
    [Fact]
    public async Task BatchUpdateAsync_LocatesTargetsByItemDataKey()
    {
        var service = new FakeBatchCrudApplicationService();
        var first = await service.CreateAsync(new ContractTestCreateDto { Name = "甲" });
        var second = await service.CreateAsync(new ContractTestCreateDto { Name = "乙" });

        var request = new BatchUpdateRequest<ContractTestUpdateDto>
        {
            ContinueOnError = true,
            Items =
            [
                new BatchUpdateItem<ContractTestUpdateDto>
                {
                    Data = new ContractTestUpdateDto { BasicId = first.BasicId, Name = "甲改" }
                },
                new BatchUpdateItem<ContractTestUpdateDto>
                {
                    Data = new ContractTestUpdateDto { BasicId = 9999L, Name = "不存在" }
                },
                new BatchUpdateItem<ContractTestUpdateDto>
                {
                    Data = new ContractTestUpdateDto { BasicId = second.BasicId, Name = "乙改" }
                }
            ]
        };

        var response = await service.BatchUpdateAsync(request);

        Assert.Equal(3, response.TotalCount);
        Assert.Equal(2, response.SuccessCount);
        Assert.Equal(1, response.FailureCount);
        Assert.Equal("NotFound", response.Results[1].ErrorCode);

        var reloaded = await service.GetByIdAsync(first.BasicId);

        Assert.NotNull(reloaded);
        Assert.Equal("甲改", reloaded!.Name);
    }

    /// <summary>
    /// 批量删除的响应泛型是 bool：逐项回传删除是否命中
    /// </summary>
    [Fact]
    public async Task BatchDeleteAsync_ReturnsBooleanPerItem()
    {
        var service = new FakeBatchCrudApplicationService();
        var first = await service.CreateAsync(new ContractTestCreateDto { Name = "甲" });

        var response = await service.BatchDeleteAsync(new BatchDeleteRequest<long>
        {
            ContinueOnError = true,
            SoftDelete = false,
            Ids = [first.BasicId, 9999L]
        });

        Assert.Equal(2, response.TotalCount);
        Assert.Equal(1, response.SuccessCount);
        Assert.Equal(1, response.FailureCount);
        Assert.True(response.Results[0].Data);
        Assert.False(response.Results[1].Data);
        Assert.Equal(0, service.StoredCount);
    }

    /// <summary>
    /// 批量删除默认走软删除：记录不会被真正移除
    /// </summary>
    [Fact]
    public async Task BatchDeleteAsync_WithDefaultSoftDelete_KeepsRecords()
    {
        var service = new FakeBatchCrudApplicationService();
        var first = await service.CreateAsync(new ContractTestCreateDto { Name = "甲" });

        var request = new BatchDeleteRequest<long> { Ids = [first.BasicId] };

        Assert.True(request.SoftDelete);

        var response = await service.BatchDeleteAsync(request);

        Assert.True(response.IsAllSuccess);
        Assert.Equal(1, service.StoredCount);
    }
}
