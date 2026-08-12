// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Application.Contracts.Services;
using XiHan.Framework.Application.Attributes;
using XiHan.Framework.Web.Api.DynamicApi.Controllers;
using XiHan.Framework.Web.Api.DynamicApi.Conventions;
using XiHan.Framework.Web.Api.DynamicApi.Exceptions;
using XiHan.Framework.Web.Api.DynamicApi.Options;


namespace XiHan.Framework.Web.Api.Tests.DynamicApiGeneration;

/// <summary>
/// 动态 API 装配失败处理的测试
/// </summary>
/// <remarks>
/// 覆盖「装配错误必须暴露、不得静默丢端点」这一契约，以及主动禁用与生成失败两种结果的区分。
/// 本类内的用例共享 <see cref="DynamicApiControllerFactory"/> 的静态缓存，故不并行执行。
/// </remarks>
[Collection("DynamicApiFactory")]
public class DynamicApiGenerationFailureTests : IDisposable
{
    /// <summary>
    /// 构造函数
    /// </summary>
    public DynamicApiGenerationFailureTests()
    {
        DynamicApiControllerFactory.ClearCache();
    }

    /// <summary>
    /// 释放
    /// </summary>
    public void Dispose()
    {
        DynamicApiControllerFactory.ClearCache();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// 正常应用服务生成控制器类型
    /// </summary>
    [Fact]
    public void CreateControllerType_ForNormalService_ReturnsType()
    {
        var options = new DynamicApiOptions();

        var controllerType = DynamicApiControllerFactory.CreateControllerType(
            typeof(OrderAppService), new DefaultDynamicApiConvention(options), options);

        Assert.NotNull(controllerType);
    }

    /// <summary>
    /// 主动禁用动态 API 的服务返回空，而不是被当成失败
    /// </summary>
    [Fact]
    public void CreateControllerType_ForDisabledService_ReturnsNull()
    {
        var options = new DynamicApiOptions();

        var controllerType = DynamicApiControllerFactory.CreateControllerType(
            typeof(DisabledAppService), new DefaultDynamicApiConvention(options), options);

        Assert.Null(controllerType);
    }

    /// <summary>
    /// 控制器名冲突抛出并指明冲突双方
    /// </summary>
    [Fact]
    public void CreateControllerType_OnControllerNameCollision_Throws()
    {
        var options = new DynamicApiOptions();
        var convention = new DefaultDynamicApiConvention(options);

        DynamicApiControllerFactory.CreateControllerType(typeof(OrderAppService), convention, options);

        var exception = Assert.Throws<DynamicApiException>(
            () => DynamicApiControllerFactory.CreateControllerType(
                typeof(Duplicated.OrderAppService), convention, options));

        Assert.Contains("控制器名冲突", exception.Message);
        Assert.Contains(typeof(OrderAppService).FullName!, exception.Message);
        Assert.Contains(typeof(Duplicated.OrderAppService).FullName!, exception.Message);
    }

    /// <summary>
    /// 类级 Name 定制控制器名后，同简名服务不再冲突
    /// </summary>
    [Fact]
    public void CreateControllerType_WithClassLevelCustomName_AvoidsCollision()
    {
        var options = new DynamicApiOptions();
        var convention = new DefaultDynamicApiConvention(options);

        var first = DynamicApiControllerFactory.CreateControllerType(typeof(OrderAppService), convention, options);
        var second = DynamicApiControllerFactory.CreateControllerType(typeof(Renamed.OrderAppService), convention, options);

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.Equal("plugin-orderController", second.Name);
    }

    /// <summary>
    /// 控制器名判重忽略大小写
    /// </summary>
    [Fact]
    public void CreateControllerType_OnCaseInsensitiveNameCollision_Throws()
    {
        var options = new DynamicApiOptions();
        var convention = new DefaultDynamicApiConvention(options);

        DynamicApiControllerFactory.CreateControllerType(typeof(OrderAppService), convention, options);

        var exception = Assert.Throws<DynamicApiException>(
            () => DynamicApiControllerFactory.CreateControllerType(
                typeof(LowercaseOrderAppService), convention, options));

        Assert.Contains("控制器名冲突", exception.Message);
    }

    /// <summary>
    /// 同一服务重复生成走缓存，不误判为冲突
    /// </summary>
    [Fact]
    public void CreateControllerType_CalledTwiceForSameService_ReturnsCachedType()
    {
        var options = new DynamicApiOptions();
        var convention = new DefaultDynamicApiConvention(options);

        var first = DynamicApiControllerFactory.CreateControllerType(typeof(OrderAppService), convention, options);
        var second = DynamicApiControllerFactory.CreateControllerType(typeof(OrderAppService), convention, options);

        Assert.Same(first, second);
    }

    /// <summary>
    /// 生成失败默认中断装配
    /// </summary>
    [Fact]
    public void ThrowOnGenerationFailure_DefaultsToTrue()
    {
        Assert.True(new DynamicApiOptions().ThrowOnGenerationFailure);
    }
}

/// <summary>
/// 动态 API 工厂测试集合，保证共享静态缓存的用例串行执行
/// </summary>
[CollectionDefinition("DynamicApiFactory", DisableParallelization = true)]
public class DynamicApiFactoryCollection;

/// <summary>
/// 订单应用服务
/// </summary>
public class OrderAppService : IApplicationService
{
    /// <summary>
    /// 查询订单
    /// </summary>
    /// <returns></returns>
    public Task<string> GetOrderAsync() => Task.FromResult(string.Empty);
}

/// <summary>
/// 禁用动态 API 的应用服务
/// </summary>
[DynamicApiAttribute(false)]
public class DisabledAppService : IApplicationService
{
    /// <summary>
    /// 查询数据
    /// </summary>
    /// <returns></returns>
    public Task<string> GetDataAsync() => Task.FromResult(string.Empty);
}

/// <summary>
/// 控制器名仅大小写不同的应用服务
/// </summary>
[DynamicApi(Name = "order")]
public class LowercaseOrderAppService : IApplicationService
{
    /// <summary>
    /// 查询订单
    /// </summary>
    /// <returns></returns>
    public Task<string> GetOrderAsync() => Task.FromResult(string.Empty);
}
