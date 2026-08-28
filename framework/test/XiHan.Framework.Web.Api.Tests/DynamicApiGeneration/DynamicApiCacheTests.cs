// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using XiHan.Framework.Caching.Interceptors;
using XiHan.Framework.Web.Api.DynamicApi.Attributes;
using XiHan.Framework.Web.Api.DynamicApi.Controllers;
using XiHan.Framework.Web.Api.DynamicApi.Conventions;
using XiHan.Framework.Web.Api.DynamicApi.Options;
using XiHan.Framework.Web.Api.Filters;

namespace XiHan.Framework.Web.Api.Tests.DynamicApiGeneration;

/// <summary>
/// 动态 API 动作的缓存特性测试
/// </summary>
/// <remarks>
/// 覆盖「应用服务方法上的 [Cacheable] / [CacheEvict] 在 HTTP 入口必须真实生效」这一契约：
/// 动态控制器注入的是应用服务的具体类、不经过接口动态代理，一旦缓存只挂在拦截器上，
/// 这两个特性对请求全部无效。
/// 本类内的用例共享 <see cref="DynamicApiControllerFactory"/> 的静态缓存，故不并行执行。
/// </remarks>
[Collection("DynamicApiFactory")]
public class DynamicApiCacheTests : IDisposable
{
    /// <summary>
    /// 构造函数
    /// </summary>
    public DynamicApiCacheTests()
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
    /// 动态控制器动作能回查到应用服务方法上的缓存特性
    /// </summary>
    [Fact]
    public void ResolveCacheMethod_ForDynamicAction_FindsServiceMethodAttribute()
    {
        var actionMethod = GetDynamicActionMethod(nameof(CacheableAppService.GetProfileAsync));

        var resolved = XiHanCacheFilter.ResolveCacheMethodOrNull(CreateActionDescriptor(actionMethod));

        Assert.NotNull(resolved);
        Assert.Equal(typeof(CacheableAppService), resolved.DeclaringType);
        Assert.NotNull(CacheAspect.GetCacheableAttributeOrNull(resolved));
    }

    /// <summary>
    /// 首次调用执行动作并写入缓存
    /// </summary>
    [Fact]
    public async Task FirstCall_ExecutesActionAndCachesResult()
    {
        await using var provider = BuildServiceProvider();
        var actionMethod = GetDynamicActionMethod(nameof(CacheableAppService.GetProfileAsync));

        var execution = await ExecuteFilterAsync(provider, actionMethod, "u1", () => "第一次");

        Assert.Equal(1, execution.ActionInvocations);
        Assert.Equal("第一次", execution.ResultValue);
    }

    /// <summary>
    /// 同键再次调用直接返回缓存值，不再执行动作
    /// </summary>
    [Fact]
    public async Task SecondCall_WithSameKey_ShortCircuitsWithCachedValue()
    {
        await using var provider = BuildServiceProvider();
        var actionMethod = GetDynamicActionMethod(nameof(CacheableAppService.GetProfileAsync));

        await ExecuteFilterAsync(provider, actionMethod, "u1", () => "第一次");
        var second = await ExecuteFilterAsync(provider, actionMethod, "u1", () => "第二次");

        Assert.Equal(0, second.ActionInvocations);
        Assert.Equal("第一次", second.ResultValue);
    }

    /// <summary>
    /// 键模板按参数取值，不同参数互不串用
    /// </summary>
    [Fact]
    public async Task DifferentKeyArgument_DoesNotHitOtherEntry()
    {
        await using var provider = BuildServiceProvider();
        var actionMethod = GetDynamicActionMethod(nameof(CacheableAppService.GetProfileAsync));

        await ExecuteFilterAsync(provider, actionMethod, "u1", () => "甲");
        var other = await ExecuteFilterAsync(provider, actionMethod, "u2", () => "乙");

        Assert.Equal(1, other.ActionInvocations);
        Assert.Equal("乙", other.ResultValue);
    }

    /// <summary>
    /// 动作抛异常时不写入缓存，下次仍会执行动作
    /// </summary>
    [Fact]
    public async Task ActionThrew_DoesNotCacheFailure()
    {
        await using var provider = BuildServiceProvider();
        var actionMethod = GetDynamicActionMethod(nameof(CacheableAppService.GetProfileAsync));

        var failed = await ExecuteFilterAsync(provider, actionMethod, "u1", () => throw new InvalidOperationException("查询失败"));
        Assert.NotNull(failed.Exception);

        var retried = await ExecuteFilterAsync(provider, actionMethod, "u1", () => "重试成功");

        Assert.Equal(1, retried.ActionInvocations);
        Assert.Equal("重试成功", retried.ResultValue);
    }

    /// <summary>
    /// 标注清除的动作执行后使缓存失效
    /// </summary>
    [Fact]
    public async Task EvictAction_RemovesCachedEntry()
    {
        await using var provider = BuildServiceProvider();
        var readMethod = GetDynamicActionMethod(nameof(CacheableAppService.GetProfileAsync));
        var evictMethod = GetDynamicActionMethod(nameof(CacheableAppService.UpdateProfileAsync));

        await ExecuteFilterAsync(provider, readMethod, "u1", () => "旧值");
        await ExecuteFilterAsync(provider, evictMethod, "u1", () => "已更新");
        var afterEvict = await ExecuteFilterAsync(provider, readMethod, "u1", () => "新值");

        Assert.Equal(1, afterEvict.ActionInvocations);
        Assert.Equal("新值", afterEvict.ResultValue);
    }

    /// <summary>
    /// 清除动作抛异常时不清缓存
    /// </summary>
    [Fact]
    public async Task EvictActionThrew_KeepsCachedEntry()
    {
        await using var provider = BuildServiceProvider();
        var readMethod = GetDynamicActionMethod(nameof(CacheableAppService.GetProfileAsync));
        var evictMethod = GetDynamicActionMethod(nameof(CacheableAppService.UpdateProfileAsync));

        await ExecuteFilterAsync(provider, readMethod, "u1", () => "旧值");
        await ExecuteFilterAsync(provider, evictMethod, "u1", () => throw new InvalidOperationException("更新失败"));
        var afterFailedEvict = await ExecuteFilterAsync(provider, readMethod, "u1", () => "新值");

        Assert.Equal(0, afterFailedEvict.ActionInvocations);
        Assert.Equal("旧值", afterFailedEvict.ResultValue);
    }

    /// <summary>
    /// 未标注缓存特性的动作照常执行
    /// </summary>
    [Fact]
    public async Task ActionWithoutCacheAttribute_AlwaysExecutes()
    {
        await using var provider = BuildServiceProvider();
        var actionMethod = GetDynamicActionMethod(nameof(CacheableAppService.PingAsync));

        await ExecuteFilterAsync(provider, actionMethod, "u1", () => "第一次");
        var second = await ExecuteFilterAsync(provider, actionMethod, "u1", () => "第二次");

        Assert.Equal(1, second.ActionInvocations);
        Assert.Equal("第二次", second.ResultValue);
    }

    /// <summary>
    /// 执行一次过滤器包裹下的动作，并回收执行次数与结果
    /// </summary>
    private static async Task<FilterExecutionResult> ExecuteFilterAsync(
        IServiceProvider provider,
        MethodInfo actionMethod,
        string userIdArgument,
        Func<string> actionBody)
    {
        var filter = new XiHanCacheFilter(provider.GetRequiredService<CacheAspect>());

        var httpContext = new DefaultHttpContext { RequestServices = provider };
        var actionContext = new ActionContext(httpContext, new RouteData(), CreateActionDescriptor(actionMethod));
        var executingContext = new ActionExecutingContext(
            actionContext,
            [],
            new Dictionary<string, object?> { ["userId"] = userIdArgument },
            controller: new object());

        var result = new FilterExecutionResult();

        await filter.OnActionExecutionAsync(executingContext, () =>
        {
            result.ActionInvocations++;

            var executedContext = new ActionExecutedContext(actionContext, [], controller: new object());
            try
            {
                executedContext.Result = new ObjectResult(actionBody());
            }
            catch (Exception exception)
            {
                executedContext.Exception = exception;
            }

            result.Exception = executedContext.Exception;
            result.ExecutedResult = executedContext.Result;

            return Task.FromResult(executedContext);
        });

        // 命中缓存时过滤器把结果短路写在执行上下文上，执行过动作则结果在动作自己的上下文上
        result.ResultValue = ((executingContext.Result ?? result.ExecutedResult) as ObjectResult)?.Value as string;

        return result;
    }

    /// <summary>
    /// 构建缓存所需服务的容器
    /// </summary>
    private static ServiceProvider BuildServiceProvider()
    {
        var services = new ServiceCollection();

        services.AddLogging();
        services.AddHybridCache();
        services.AddSingleton<CacheAspect>();

        return services.BuildServiceProvider();
    }

    /// <summary>
    /// 取动态控制器上转发指定应用服务方法的动作
    /// </summary>
    private static MethodInfo GetDynamicActionMethod(string serviceMethodName)
    {
        var options = new DynamicApiOptions();
        var controllerType = DynamicApiControllerFactory.CreateControllerType(
            typeof(CacheableAppService), new DefaultDynamicApiConvention(options), options);

        Assert.NotNull(controllerType);

        var actionMethod = controllerType
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .FirstOrDefault(method => method
                .GetCustomAttribute<OriginalMethodAttribute>()
                ?.MethodName == serviceMethodName);

        Assert.NotNull(actionMethod);

        return actionMethod;
    }

    /// <summary>
    /// 构造指向给定动作方法的控制器动作描述器
    /// </summary>
    private static ActionDescriptor CreateActionDescriptor(MethodInfo actionMethod)
    {
        return new ControllerActionDescriptor
        {
            MethodInfo = actionMethod,
            ControllerTypeInfo = actionMethod.DeclaringType!.GetTypeInfo(),
            ActionName = actionMethod.Name,
            ControllerName = actionMethod.DeclaringType!.Name
        };
    }

    /// <summary>
    /// 一次过滤器执行的观测结果
    /// </summary>
    private sealed class FilterExecutionResult
    {
        /// <summary>
        /// 动作被真实执行的次数
        /// </summary>
        public int ActionInvocations { get; set; }

        /// <summary>
        /// 动作抛出的异常
        /// </summary>
        public Exception? Exception { get; set; }

        /// <summary>
        /// 动作自身产出的结果
        /// </summary>
        public IActionResult? ExecutedResult { get; set; }

        /// <summary>
        /// 本次响应的字符串值
        /// </summary>
        public string? ResultValue { get; set; }
    }
}
