// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Reflection;
using XiHan.Framework.Uow;
using XiHan.Framework.Web.Api.DynamicApi.Helpers;

namespace XiHan.Framework.Web.Api.Filters;

/// <summary>
/// WebApi Action 工作单元过滤器
/// </summary>
/// <remarks>
/// 控制器由 MVC 自行激活、动态控制器又直接注入应用服务的具体类，两者都不经过接口动态代理，
/// <c>UnitOfWorkInterceptor</c> 在 HTTP 入口不会执行；本过滤器按同一套 <see cref="UnitOfWorkHelper"/>
/// 规则在动作外层开启工作单元：动作正常返回才提交，动作抛出异常（无论后续是否被异常过滤器接管）一律不提交，
/// 由工作单元释放时回滚。
/// </remarks>
public class XiHanUnitOfWorkFilter : IAsyncActionFilter
{
    private readonly IUnitOfWorkManager _unitOfWorkManager;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="unitOfWorkManager">工作单元管理器</param>
    public XiHanUnitOfWorkFilter(IUnitOfWorkManager unitOfWorkManager)
    {
        _unitOfWorkManager = unitOfWorkManager;
    }

    /// <summary>
    /// 解析动作对应的工作单元方法，非控制器动作返回 null
    /// </summary>
    /// <param name="actionDescriptor">动作描述器</param>
    /// <returns>用于读取工作单元特性的方法</returns>
    public static MethodInfo? ResolveUnitOfWorkMethodOrNull(ActionDescriptor actionDescriptor)
    {
        return actionDescriptor is ControllerActionDescriptor controllerActionDescriptor
            ? OriginalMethodResolver.Resolve(controllerActionDescriptor.MethodInfo)
            : null;
    }

    /// <summary>
    /// Action 执行前后的工作单元边界
    /// </summary>
    /// <param name="context">动作执行上下文</param>
    /// <param name="next">后续管道</param>
    /// <returns>异步任务</returns>
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var unitOfWorkMethod = ResolveUnitOfWorkMethodOrNull(context.ActionDescriptor);
        if (unitOfWorkMethod is null ||
            !UnitOfWorkHelper.IsUnitOfWorkMethod(unitOfWorkMethod, out var unitOfWorkAttribute))
        {
            await next();
            return;
        }

        var options = UnitOfWorkHelper.CreateOptions(
            context.HttpContext.RequestServices,
            unitOfWorkMethod,
            unitOfWorkAttribute);

        using var unitOfWork = _unitOfWorkManager.Begin(options);

        var executedContext = await next();

        // 动作抛出异常（含已被接管的）时跳过提交，工作单元释放时回滚
        if (executedContext.Exception is not null)
        {
            return;
        }

        // 提交不接请求取消令牌，动作成功返回后不再因客户端断开而中断落库
        await unitOfWork.CompleteAsync(CancellationToken.None);
    }
}
