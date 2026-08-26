// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Core.DependencyInjection.ServiceLifetimes;
using XiHan.Framework.Core.DynamicProxy;

namespace XiHan.Framework.Uow;

/// <summary>
/// 工作单元拦截器
/// </summary>
public class UnitOfWorkInterceptor : XiHanInterceptor, ITransientDependency
{
    private readonly IServiceScopeFactory _serviceScopeFactory;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="serviceScopeFactory"></param>
    public UnitOfWorkInterceptor(IServiceScopeFactory serviceScopeFactory)
    {
        _serviceScopeFactory = serviceScopeFactory;
    }

    /// <summary>
    /// 异步拦截
    /// </summary>
    /// <param name="invocation"></param>
    /// <returns></returns>
    public override async Task InterceptAsync(IXiHanMethodInvocation invocation)
    {
        if (!UnitOfWorkHelper.IsUnitOfWorkMethod(invocation.Method, out var unitOfWorkAttribute))
        {
            await invocation.ProceedAsync();
            return;
        }

        using var scope = _serviceScopeFactory.CreateScope();
        var options = UnitOfWorkHelper.CreateOptions(scope.ServiceProvider, invocation.Method, unitOfWorkAttribute);

        var unitOfWorkManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();

        // 如果已经有工作单元，则直接使用(试图通过 XiHanUnitOfWorkMiddleware 开始保留UOW)
        if (unitOfWorkManager.TryBeginReserved(UnitOfWork.UnitOfWorkReservationName, options))
        {
            await invocation.ProceedAsync();

            if (unitOfWorkManager.Current != null)
            {
                await unitOfWorkManager.Current.SaveChangesAsync();
            }

            return;
        }

        using var uow = unitOfWorkManager.Begin(options);
        await invocation.ProceedAsync();
        await uow.CompleteAsync();
    }
}
