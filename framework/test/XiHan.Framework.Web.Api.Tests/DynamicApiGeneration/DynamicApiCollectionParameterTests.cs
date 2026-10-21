// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Mvc;
using System.Reflection;
using XiHan.Framework.Application.Contracts.Services;
using XiHan.Framework.Web.Api.DynamicApi.Controllers;
using XiHan.Framework.Web.Api.DynamicApi.Conventions;
using XiHan.Framework.Web.Api.DynamicApi.Options;
using XiHan.Framework.Web.Api.DynamicApi.ParameterAnalysis;

namespace XiHan.Framework.Web.Api.Tests.DynamicApiGeneration;

/// <summary>
/// 动态 API 集合参数绑定来源的测试
/// </summary>
/// <remarks>
/// 覆盖「允许请求体的方法上，未显式标注的集合参数按请求体绑定」这一契约：
/// 此前集合一律落到查询串，客户端以 JSON 数组发送的请求体到达服务方法时是空集合。
/// 本类内的用例共享 <see cref="DynamicApiControllerFactory"/> 的静态缓存，故不并行执行。
/// </remarks>
[Collection("DynamicApiFactory")]
public class DynamicApiCollectionParameterTests : IDisposable
{
    /// <summary>
    /// 构造函数
    /// </summary>
    public DynamicApiCollectionParameterTests()
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
    /// 允许请求体的方法上，各种集合形态的参数都按请求体绑定
    /// </summary>
    /// <param name="methodName">方法名</param>
    /// <param name="httpMethod">HTTP 方法</param>
    [Theory]
    [InlineData(nameof(CollectionSampleAppService.KillSessionsAsync), "POST")]
    [InlineData(nameof(CollectionSampleAppService.ImportOrdersAsync), "POST")]
    [InlineData(nameof(CollectionSampleAppService.SyncOrdersAsync), "PUT")]
    [InlineData(nameof(CollectionSampleAppService.ArchiveOrdersAsync), "PATCH")]
    public void CollectionParameter_OnBodyAllowedMethod_BindsFromBody(string methodName, string httpMethod)
    {
        var descriptor = Analyze(methodName, httpMethod).Single(d => d.Kind == ParameterKind.Collection);

        Assert.Equal(ParameterSource.Body, descriptor.Source);
        Assert.False(descriptor.IsExplicit);
    }

    /// <summary>
    /// 不允许请求体的方法上，集合参数仍走查询串
    /// </summary>
    /// <param name="httpMethod">HTTP 方法</param>
    [Theory]
    [InlineData("GET")]
    [InlineData("DELETE")]
    [InlineData("HEAD")]
    public void CollectionParameter_OnBodylessMethod_BindsFromQuery(string httpMethod)
    {
        var descriptor = Analyze(nameof(CollectionSampleAppService.ArchiveOrdersAsync), httpMethod).Single(d => d.Kind == ParameterKind.Collection);

        Assert.Equal(ParameterSource.Query, descriptor.Source);
    }

    /// <summary>
    /// 显式标注 [FromBody] 的集合参数被接受，而不是被当成非法的请求体类型
    /// </summary>
    [Fact]
    public void ExplicitFromBodyCollection_IsAccepted()
    {
        var descriptor = Analyze(nameof(CollectionSampleAppService.KillSessionsByBodyAsync), "POST").Single(d => d.Name == "input");

        Assert.Equal(ParameterSource.Body, descriptor.Source);
        Assert.True(descriptor.IsExplicit);
    }

    /// <summary>
    /// 请求体只给第一个可绑定请求体的参数，排在其后的集合降级为查询串
    /// </summary>
    /// <param name="methodName">方法名</param>
    /// <param name="collectionParameterName">集合参数名</param>
    [Theory]
    [InlineData(nameof(CollectionSampleAppService.MoveOrdersAsync), "orderIds")]
    [InlineData(nameof(CollectionSampleAppService.ReplaceOrdersAsync), "items")]
    public void CollectionAfterBodyParameter_BindsFromQuery(string methodName, string collectionParameterName)
    {
        var descriptors = Analyze(methodName, "POST");

        Assert.Equal(ParameterSource.Body, descriptors.Single(d => d.Name == "target").Source);
        Assert.Equal(ParameterSource.Query, descriptors.Single(d => d.Name == collectionParameterName).Source);
    }

    /// <summary>
    /// 集合排在前面时由它占用请求体，其后的复杂类型降级为查询串
    /// </summary>
    [Fact]
    public void ComplexParameterAfterCollection_BindsFromQuery()
    {
        var descriptors = Analyze(nameof(CollectionSampleAppService.AssignOrdersAsync), "POST");

        Assert.Equal(ParameterSource.Body, descriptors.Single(d => d.Name == "orderIds").Source);
        Assert.Equal(ParameterSource.Query, descriptors.Single(d => d.Name == "target").Source);
    }

    /// <summary>
    /// 显式标注 [FromQuery] 的集合参数在允许请求体的方法上仍走查询串
    /// </summary>
    [Fact]
    public void ExplicitFromQueryCollection_StaysInQuery()
    {
        var descriptor = Analyze(nameof(CollectionSampleAppService.TagOrdersAsync), "POST").Single(d => d.Name == "orderIds");

        Assert.Equal(ParameterSource.Query, descriptor.Source);
        Assert.True(descriptor.IsExplicit);
    }

    /// <summary>
    /// 生成的动作方法在集合参数上标注 [FromBody]
    /// </summary>
    [Fact]
    public void GeneratedAction_MarksCollectionParameterFromBody()
    {
        var options = new DynamicApiOptions();

        var controllerType = DynamicApiControllerFactory.CreateControllerType(
            typeof(CollectionSampleAppService), new DefaultDynamicApiConvention(options), options);

        Assert.NotNull(controllerType);
        var parameter = controllerType
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .Where(m => m.GetParameters().Any(p => p.ParameterType == typeof(CancellationToken)))
            .SelectMany(m => m.GetParameters())
            .Single(p => p.ParameterType == typeof(List<KillSessionInput>));

        Assert.NotNull(parameter.GetCustomAttribute<FromBodyAttribute>());
        Assert.Null(parameter.GetCustomAttribute<FromQueryAttribute>());
    }

    /// <summary>
    /// 按指定 HTTP 方法分析示例服务的方法参数
    /// </summary>
    /// <param name="methodName">方法名</param>
    /// <param name="httpMethod">HTTP 方法</param>
    /// <returns>参数描述符列表</returns>
    private static List<ParameterDescriptor> Analyze(string methodName, string httpMethod)
    {
        var method = typeof(CollectionSampleAppService).GetMethod(methodName)!;

        return DynamicApiParameterAnalyzer.Analyze(method, httpMethod);
    }
}

/// <summary>
/// 集合参数绑定测试用的应用服务
/// </summary>
public class CollectionSampleAppService : IApplicationService
{
    /// <summary>
    /// 终止数据库会话，参数为复杂元素列表
    /// </summary>
    /// <param name="input">会话列表</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    public Task<int> KillSessionsAsync(List<KillSessionInput> input, CancellationToken cancellationToken = default) => Task.FromResult(input.Count);

    /// <summary>
    /// 终止数据库会话，参数显式标注 [FromBody]
    /// </summary>
    /// <param name="input">会话列表</param>
    /// <returns></returns>
    public Task<int> KillSessionsByBodyAsync([FromBody] List<KillSessionInput> input) => Task.FromResult(input.Count);

    /// <summary>
    /// 导入订单，参数为复杂元素数组
    /// </summary>
    /// <param name="orders">订单数组</param>
    /// <returns></returns>
    public Task<int> ImportOrdersAsync(OrderItemInput[] orders) => Task.FromResult(orders.Length);

    /// <summary>
    /// 同步订单，参数为复杂元素枚举
    /// </summary>
    /// <param name="orders">订单序列</param>
    /// <returns></returns>
    public Task<int> SyncOrdersAsync(IEnumerable<OrderItemInput> orders) => Task.FromResult(orders.Count());

    /// <summary>
    /// 归档订单，参数为简单元素列表
    /// </summary>
    /// <param name="orderIds">订单主键列表</param>
    /// <returns></returns>
    public Task<int> ArchiveOrdersAsync(List<long> orderIds) => Task.FromResult(orderIds.Count);

    /// <summary>
    /// 移动订单，复杂参数之后跟随简单元素列表
    /// </summary>
    /// <param name="target">目标位置</param>
    /// <param name="orderIds">订单主键列表</param>
    /// <returns></returns>
    public Task<int> MoveOrdersAsync(OrderItemInput target, List<long> orderIds) => Task.FromResult(orderIds.Count);

    /// <summary>
    /// 替换订单，复杂参数之后跟随复杂元素列表
    /// </summary>
    /// <param name="target">目标位置</param>
    /// <param name="items">订单列表</param>
    /// <returns></returns>
    public Task<int> ReplaceOrdersAsync(OrderItemInput target, List<OrderItemInput> items) => Task.FromResult(items.Count);

    /// <summary>
    /// 分配订单，简单元素列表之后跟随复杂参数
    /// </summary>
    /// <param name="orderIds">订单主键列表</param>
    /// <param name="target">目标位置</param>
    /// <returns></returns>
    public Task<int> AssignOrdersAsync(List<long> orderIds, OrderItemInput target) => Task.FromResult(orderIds.Count);

    /// <summary>
    /// 标记订单，集合参数显式标注 [FromQuery]
    /// </summary>
    /// <param name="orderIds">订单主键列表</param>
    /// <returns></returns>
    public Task<int> TagOrdersAsync([FromQuery] List<long> orderIds) => Task.FromResult(orderIds.Count);
}

/// <summary>
/// 终止会话输入
/// </summary>
public class KillSessionInput
{
    /// <summary>
    /// 会话标识
    /// </summary>
    public long SessionId { get; set; }

    /// <summary>
    /// 登录时间
    /// </summary>
    public string? LoginTime { get; set; }
}

/// <summary>
/// 订单项输入
/// </summary>
public class OrderItemInput
{
    /// <summary>
    /// 订单主键
    /// </summary>
    public long OrderId { get; set; }

    /// <summary>
    /// 数量
    /// </summary>
    public int Quantity { get; set; }
}
