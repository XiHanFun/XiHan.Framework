// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Application.Contracts.Tests.Services.Fakes;
using XiHan.Framework.Domain.Shared.Paging.Dtos;
using XiHan.Framework.Domain.Shared.Paging.Models;

namespace XiHan.Framework.Application.Contracts.Tests.Services;

/// <summary>
/// CRUD 应用服务契约测试
/// </summary>
/// <remarks>
/// 该接口是跨项目的编排边界：DTO 基类来自本项目，分页出入参来自 Domain.Shared。
/// 这里一半用反射锁死方法签名（签名变了下游全部实现要改），
/// 一半用手写内存实现跑通完整调用链，验证「主键由服务端分配、分页元数据从请求回填到响应」这条契约。
/// </remarks>
public class CrudApplicationServiceContractTests
{
    /// <summary>
    /// 契约恰好声明五个方法，不多不少
    /// </summary>
    [Fact]
    public void Contract_DeclaresExactlyFiveMethods()
    {
        var names = ContractTypes.Crud.GetMethods().Select(method => method.Name).OrderBy(name => name, StringComparer.Ordinal).ToArray();
        string[] expected = ["CreateAsync", "DeleteAsync", "GetByIdAsync", "PageAsync", "UpdateAsync"];

        Assert.Equal(expected, names);
    }

    /// <summary>
    /// 单个查询：入参为主键，返回可空实体 DTO
    /// </summary>
    [Fact]
    public void GetByIdAsync_SignatureIsStable()
    {
        var method = ContractTypes.Crud.GetMethod("GetByIdAsync");

        Assert.NotNull(method);
        Assert.Equal(typeof(Task<ContractTestEntityDto>), method!.ReturnType);

        var parameterTypes = method.GetParameters().Select(parameter => parameter.ParameterType).ToArray();

        Assert.Single(parameterTypes);
        Assert.Equal(typeof(long), parameterTypes[0]);
    }

    /// <summary>
    /// 分页：入参为分页请求 DTO，返回 Domain.Shared 的分页结果
    /// </summary>
    [Fact]
    public void PageAsync_SignatureIsStable()
    {
        var method = ContractTypes.Crud.GetMethod("PageAsync");

        Assert.NotNull(method);
        Assert.Equal(typeof(Task<PageResultDtoBase<ContractTestEntityDto>>), method!.ReturnType);

        var parameterTypes = method.GetParameters().Select(parameter => parameter.ParameterType).ToArray();

        Assert.Single(parameterTypes);
        Assert.Equal(typeof(ContractTestPageRequestDto), parameterTypes[0]);
    }

    /// <summary>
    /// 写入类方法的入参与返回类型稳定：创建收创建 DTO、更新收更新 DTO、删除返回 bool
    /// </summary>
    [Fact]
    public void WriteMethods_SignaturesAreStable()
    {
        var create = ContractTypes.Crud.GetMethod("CreateAsync");
        var update = ContractTypes.Crud.GetMethod("UpdateAsync");
        var delete = ContractTypes.Crud.GetMethod("DeleteAsync");

        Assert.NotNull(create);
        Assert.NotNull(update);
        Assert.NotNull(delete);

        Assert.Equal(typeof(Task<ContractTestEntityDto>), create!.ReturnType);
        Assert.Equal(typeof(ContractTestCreateDto), create.GetParameters()[0].ParameterType);

        Assert.Equal(typeof(Task<ContractTestEntityDto>), update!.ReturnType);
        Assert.Equal(typeof(ContractTestUpdateDto), update.GetParameters()[0].ParameterType);

        Assert.Equal(typeof(Task<bool>), delete!.ReturnType);
        Assert.Equal(typeof(long), delete.GetParameters()[0].ParameterType);
    }

    /// <summary>
    /// 创建入参不带主键，主键必须由服务端分配后写回实体 DTO
    /// </summary>
    [Fact]
    public async Task CreateAsync_AssignsKeyOnServerSide()
    {
        var service = new FakeBatchCrudApplicationService();

        var first = await service.CreateAsync(new ContractTestCreateDto { Name = "甲" });
        var second = await service.CreateAsync(new ContractTestCreateDto { Name = "乙" });

        Assert.NotEqual(0L, first.BasicId);
        Assert.NotEqual(first.BasicId, second.BasicId);
        Assert.Equal("甲", first.Name);
        Assert.Equal(2, service.StoredCount);
    }

    /// <summary>
    /// 查询不存在的主键返回 null，而不是抛异常
    /// </summary>
    /// <remarks>
    /// 返回类型声明为可空正是为了表达这一点：调用方必须判空，不能指望异常。
    /// </remarks>
    [Fact]
    public async Task GetByIdAsync_WhenKeyMissing_ReturnsNull()
    {
        var service = new FakeBatchCrudApplicationService();

        Assert.Null(await service.GetByIdAsync(404L));
    }

    /// <summary>
    /// 更新按更新 DTO 自带的主键定位对象
    /// </summary>
    [Fact]
    public async Task UpdateAsync_LocatesTargetByDtoKey()
    {
        var service = new FakeBatchCrudApplicationService();
        var created = await service.CreateAsync(new ContractTestCreateDto { Name = "旧名" });

        var updated = await service.UpdateAsync(new ContractTestUpdateDto { BasicId = created.BasicId, Name = "新名" });

        Assert.Equal(created.BasicId, updated.BasicId);
        Assert.Equal("新名", updated.Name);

        var reloaded = await service.GetByIdAsync(created.BasicId);

        Assert.NotNull(reloaded);
        Assert.Equal("新名", reloaded!.Name);
    }

    /// <summary>
    /// 删除返回是否命中，重复删除返回 false
    /// </summary>
    [Fact]
    public async Task DeleteAsync_ReturnsWhetherKeyWasHit()
    {
        var service = new FakeBatchCrudApplicationService();
        var created = await service.CreateAsync(new ContractTestCreateDto { Name = "待删" });

        Assert.True(await service.DeleteAsync(created.BasicId));
        Assert.False(await service.DeleteAsync(created.BasicId));
        Assert.Equal(0, service.StoredCount);
    }

    /// <summary>
    /// 分页请求不填时走框架默认值：第 1 页、每页 20 条
    /// </summary>
    [Fact]
    public async Task PageAsync_WithDefaultRequest_UsesFrameworkDefaults()
    {
        var service = new FakeBatchCrudApplicationService();
        for (var index = 0; index < 25; index++)
        {
            await service.CreateAsync(new ContractTestCreateDto { Name = $"item-{index}" });
        }

        var result = await service.PageAsync(new ContractTestPageRequestDto());

        Assert.Equal(PageRequestMetadata.DefaultPageIndex, result.Page.PageIndex);
        Assert.Equal(PageRequestMetadata.DefaultPageSize, result.Page.PageSize);
        Assert.Equal(25, result.Page.TotalCount);
        Assert.Equal(PageRequestMetadata.DefaultPageSize, result.Items.Count);
    }

    /// <summary>
    /// 分页元数据从请求原样回填到响应，末页的剩余条数正确
    /// </summary>
    [Fact]
    public async Task PageAsync_OnLastPage_ReflectsRequestedWindow()
    {
        var service = new FakeBatchCrudApplicationService();
        for (var index = 0; index < 25; index++)
        {
            await service.CreateAsync(new ContractTestCreateDto { Name = $"item-{index}" });
        }

        var request = new ContractTestPageRequestDto();
        request.WithPage(3, 10);

        var result = await service.PageAsync(request);

        Assert.Equal(3, result.Page.PageIndex);
        Assert.Equal(10, result.Page.PageSize);
        Assert.Equal(25, result.Page.TotalCount);
        Assert.Equal(3, result.Page.TotalPages);
        Assert.True(result.Page.IsLastPage);
        Assert.Equal(5, result.Items.Count);
    }

    /// <summary>
    /// 空数据集的分页结果是空列表而非 null
    /// </summary>
    [Fact]
    public async Task PageAsync_WhenEmpty_ReturnsEmptyItems()
    {
        var service = new FakeBatchCrudApplicationService();

        var result = await service.PageAsync(new ContractTestPageRequestDto());

        Assert.NotNull(result.Items);
        Assert.Empty(result.Items);
        Assert.Equal(0, result.Page.TotalCount);
    }
}
