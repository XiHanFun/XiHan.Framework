// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Reflection;
using XiHan.Framework.Caching.Interceptors;
using XiHan.Framework.Web.Api.DynamicApi.Helpers;

namespace XiHan.Framework.Web.Api.Filters;

/// <summary>
/// WebApi Action 缓存过滤器
/// </summary>
/// <remarks>
/// 控制器由 MVC 自行激活、动态控制器又直接注入应用服务的具体类，两者都不经过接口动态代理，
/// <c>CacheInterceptor</c> 在 HTTP 入口不会执行；本过滤器在动作外层按同一套 <see cref="CacheAspect"/>
/// 语义处理 <c>[Cacheable]</c> 与 <c>[CacheEvict]</c>：命中则不执行动作直接返回缓存值，
/// 动作抛出异常时既不写入缓存也不清除缓存。
/// 注册位置在工作单元过滤器之外，命中缓存不会开启事务，清除缓存发生在事务提交之后。
/// </remarks>
public class XiHanCacheFilter : IAsyncActionFilter
{
    private readonly CacheAspect _cacheAspect;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="cacheAspect">缓存切面</param>
    public XiHanCacheFilter(CacheAspect cacheAspect)
    {
        _cacheAspect = cacheAspect;
    }

    /// <summary>
    /// 解析动作对应的缓存特性来源方法，非控制器动作返回 null
    /// </summary>
    /// <param name="actionDescriptor">动作描述器</param>
    /// <returns>用于读取缓存特性的方法</returns>
    public static MethodInfo? ResolveCacheMethodOrNull(ActionDescriptor actionDescriptor)
    {
        return actionDescriptor is ControllerActionDescriptor controllerActionDescriptor
            ? OriginalMethodResolver.Resolve(controllerActionDescriptor.MethodInfo)
            : null;
    }

    /// <summary>
    /// Action 执行前后的缓存读写
    /// </summary>
    /// <param name="context">动作执行上下文</param>
    /// <param name="next">后续管道</param>
    /// <returns>异步任务</returns>
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var cacheMethod = ResolveCacheMethodOrNull(context.ActionDescriptor);
        if (cacheMethod is null)
        {
            await next();
            return;
        }

        var cacheableAttribute = CacheAspect.GetCacheableAttributeOrNull(cacheMethod);
        if (cacheableAttribute is not null)
        {
            await HandleCacheableAsync(context, next, cacheMethod, cacheableAttribute.Key, cacheableAttribute.ExpireSeconds);
            return;
        }

        var evictAttributes = CacheAspect.GetCacheEvictAttributes(cacheMethod);
        if (evictAttributes.Length == 0)
        {
            await next();
            return;
        }

        var executedContext = await next();

        // 动作抛出异常（含已被接管的）时不清除缓存，避免为一次失败的写入丢掉有效缓存
        if (executedContext.Exception is null)
        {
            await _cacheAspect.EvictAsync(cacheMethod, ResolveArguments(cacheMethod, context.ActionArguments), evictAttributes);
        }
    }

    private async Task HandleCacheableAsync(
        ActionExecutingContext context,
        ActionExecutionDelegate next,
        MethodInfo cacheMethod,
        string keyTemplate,
        int expireSeconds)
    {
        var valueType = CacheAspect.GetCacheableValueTypeOrNull(cacheMethod);

        // 无返回值、或返回值本身就是动作结果（无法作为缓存值往返）时不缓存
        if (valueType is null || typeof(IActionResult).IsAssignableFrom(valueType))
        {
            await next();
            return;
        }

        var cacheKey = CacheKeyBuilder.Build(keyTemplate, cacheMethod, ResolveArguments(cacheMethod, context.ActionArguments));

        ActionExecutedContext? executedContext = null;
        object? value;

        try
        {
            value = await _cacheAspect.GetOrCreateAsync(valueType, cacheKey, expireSeconds, async () =>
            {
                executedContext = await next();

                if (executedContext.Exception is not null)
                {
                    // 抛出以中断缓存写入；异常本身留在 executedContext 上，返回后由 MVC 按原路处理
                    throw new CacheFactoryAbortedException();
                }

                return (executedContext.Result as ObjectResult)?.Value;
            });
        }
        catch (CacheFactoryAbortedException)
        {
            return;
        }

        // 动作已执行过，其自身结果就是本次响应；未执行说明命中缓存，用缓存值短路
        if (executedContext is null)
        {
            context.Result = new ObjectResult(value);
        }
    }

    /// <summary>
    /// 按方法形参顺序取实参，未绑定的形参按 null 处理
    /// </summary>
    private static object?[] ResolveArguments(MethodInfo method, IDictionary<string, object?> actionArguments)
    {
        var parameters = method.GetParameters();
        var arguments = new object?[parameters.Length];

        for (var i = 0; i < parameters.Length; i++)
        {
            arguments[i] = actionArguments.TryGetValue(parameters[i].Name!, out var value) ? value : null;
        }

        return arguments;
    }

    /// <summary>
    /// 中断缓存写入的内部信号，不外泄到调用方
    /// </summary>
    private sealed class CacheFactoryAbortedException : Exception;
}
