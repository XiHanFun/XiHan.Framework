// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using XiHan.Framework.Application.Contracts.Services;
using XiHan.Framework.Web.Api.DynamicApi.Conventions;
using XiHan.Framework.Web.Api.DynamicApi.Options;

namespace XiHan.Framework.Web.Api.Tests.DynamicApiGeneration;

/// <summary>
/// 动态 API 参数绑定与路由段推导的测试
/// </summary>
/// <remarks>
/// 覆盖「路由段只由显式 [FromRoute] 产生」这一契约：参数名不再决定 URL，
/// 给既有方法增删参数不会静默改变线上路由。
/// </remarks>
public class DynamicApiParameterBindingTests
{
    /// <summary>
    /// 名称像主键的参数不再进入路由
    /// </summary>
    /// <param name="methodName">方法名</param>
    [Theory]
    [InlineData(nameof(BindingSampleAppService.GetOrderAsync))]
    [InlineData(nameof(BindingSampleAppService.DeleteOrderAsync))]
    [InlineData(nameof(BindingSampleAppService.GetOrdersAsync))]
    public void IdLikeParameters_DoNotEnterRoute(string methodName)
    {
        Assert.DoesNotContain("{", ResolveRouteTemplate(methodName), StringComparison.Ordinal);
    }

    /// <summary>
    /// 显式标注的参数进入路由
    /// </summary>
    [Fact]
    public void ExplicitFromRoute_EntersRoute()
    {
        Assert.Contains("{id}", ResolveRouteTemplate(nameof(BindingSampleAppService.GetOrderByRouteAsync)), StringComparison.Ordinal);
    }

    /// <summary>
    /// 显式标注的绑定名优先于参数名
    /// </summary>
    [Fact]
    public void ExplicitFromRoute_UsesBindingName()
    {
        var template = ResolveRouteTemplate(nameof(BindingSampleAppService.GetOrderByNamedRouteAsync));

        Assert.Contains("{orderId}", template, StringComparison.Ordinal);
        Assert.DoesNotContain("{id}", template, StringComparison.Ordinal);
    }

    /// <summary>
    /// 多个显式路由参数按声明顺序全部进入路由
    /// </summary>
    [Fact]
    public void MultipleExplicitFromRoute_AllEnterRouteInOrder()
    {
        Assert.EndsWith("{tenantId}/{id}", ResolveRouteTemplate(nameof(BindingSampleAppService.GetTenantOrderAsync)), StringComparison.Ordinal);
    }

    /// <summary>
    /// 非路由的显式绑定不进入路由
    /// </summary>
    /// <param name="methodName">方法名</param>
    [Theory]
    [InlineData(nameof(BindingSampleAppService.GetOrderByQueryAsync))]
    [InlineData(nameof(BindingSampleAppService.UpdateOrderAsync))]
    public void NonRouteExplicitBindings_DoNotEnterRoute(string methodName)
    {
        Assert.DoesNotContain("{", ResolveRouteTemplate(methodName), StringComparison.Ordinal);
    }

    /// <summary>
    /// 无参数方法的路由仅为动作名
    /// </summary>
    [Fact]
    public void ParameterlessMethod_RouteIsActionNameOnly()
    {
        var method = typeof(BindingSampleAppService).GetMethod(nameof(BindingSampleAppService.ListOrdersAsync), [])!;

        Assert.Equal("Orders", ResolveRouteTemplate(method));
    }

    /// <summary>
    /// 给既有方法新增名称像主键的参数不改变路由
    /// </summary>
    /// <remarks>
    /// 用同名重载构造「加参数前后」两个版本，排除方法名差异的干扰。
    /// </remarks>
    [Fact]
    public void AddingIdLikeParameter_DoesNotChangeRoute()
    {
        var before = typeof(BindingSampleAppService).GetMethod(nameof(BindingSampleAppService.ListOrdersAsync), [])!;
        var after = typeof(BindingSampleAppService).GetMethod(nameof(BindingSampleAppService.ListOrdersAsync), [typeof(long?)])!;

        Assert.Equal(ResolveRouteTemplate(before), ResolveRouteTemplate(after));
    }

    /// <summary>
    /// 经约定解析指定方法的路由模板
    /// </summary>
    /// <param name="methodName">方法名</param>
    /// <returns>路由模板</returns>
    private static string ResolveRouteTemplate(string methodName)
    {
        return ResolveRouteTemplate(typeof(BindingSampleAppService).GetMethod(methodName)!);
    }

    /// <summary>
    /// 经约定解析指定方法的路由模板
    /// </summary>
    /// <param name="methodInfo">方法信息</param>
    /// <returns>路由模板</returns>
    private static string ResolveRouteTemplate(MethodInfo methodInfo)
    {
        var context = new DynamicApiConventionContext
        {
            ServiceType = typeof(BindingSampleAppService),
            MethodInfo = methodInfo
        };

        new DefaultDynamicApiConvention(new DynamicApiOptions()).Apply(context);

        return context.RouteTemplate ?? string.Empty;
    }
}

/// <summary>
/// 参数绑定测试用的应用服务
/// </summary>
public class BindingSampleAppService : IApplicationService
{
    /// <summary>
    /// 按主键查询订单，参数未显式标注
    /// </summary>
    /// <param name="id">订单主键</param>
    /// <returns></returns>
    public Task<string> GetOrderAsync(long id) => Task.FromResult(id.ToString());

    /// <summary>
    /// 按主键删除订单，参数未显式标注
    /// </summary>
    /// <param name="orderId">订单主键</param>
    /// <returns></returns>
    public Task<string> DeleteOrderAsync(long orderId) => Task.FromResult(orderId.ToString());

    /// <summary>
    /// 按多个主键查询订单，参数未显式标注
    /// </summary>
    /// <param name="tenantId">租户主键</param>
    /// <param name="orderId">订单主键</param>
    /// <returns></returns>
    public Task<string> GetOrdersAsync(long tenantId, long orderId) => Task.FromResult($"{tenantId}{orderId}");

    /// <summary>
    /// 按主键查询订单，参数显式标注为路由
    /// </summary>
    /// <param name="id">订单主键</param>
    /// <returns></returns>
    public Task<string> GetOrderByRouteAsync([FromRoute] long id) => Task.FromResult(id.ToString());

    /// <summary>
    /// 按主键查询订单，路由绑定名与参数名不同
    /// </summary>
    /// <param name="id">订单主键</param>
    /// <returns></returns>
    public Task<string> GetOrderByNamedRouteAsync([FromRoute(Name = "orderId")] long id) => Task.FromResult(id.ToString());

    /// <summary>
    /// 按租户与主键查询订单，两个参数均显式标注为路由
    /// </summary>
    /// <param name="tenantId">租户主键</param>
    /// <param name="id">订单主键</param>
    /// <returns></returns>
    public Task<string> GetTenantOrderAsync([FromRoute] long tenantId, [FromRoute] long id) => Task.FromResult($"{tenantId}{id}");

    /// <summary>
    /// 按主键查询订单，参数显式标注为查询串
    /// </summary>
    /// <param name="id">订单主键</param>
    /// <returns></returns>
    public Task<string> GetOrderByQueryAsync([FromQuery] long id) => Task.FromResult(id.ToString());

    /// <summary>
    /// 更新订单，主键参数未显式标注
    /// </summary>
    /// <param name="id">订单主键</param>
    /// <param name="name">订单名称</param>
    /// <returns></returns>
    public Task<string> UpdateOrderAsync(long id, string name) => Task.FromResult($"{id}{name}");

    /// <summary>
    /// 查询订单列表，无参数
    /// </summary>
    /// <returns></returns>
    public Task<string> ListOrdersAsync() => Task.FromResult(string.Empty);

    /// <summary>
    /// 查询订单列表，重载新增一个名称像主键的参数
    /// </summary>
    /// <param name="parentId">上级主键</param>
    /// <returns></returns>
    public Task<string> ListOrdersAsync(long? parentId) => Task.FromResult(parentId?.ToString() ?? string.Empty);
}
