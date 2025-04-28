#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:UnitOfWorkInterceptor
// Guid:8794b98a-9f2d-4617-a945-a01ffb53a629
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/4/1 20:38:13
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using XiHan.Framework.Core.DependencyInjection.ServiceLifetimes;
using XiHan.Framework.Core.DynamicProxy;
using XiHan.Framework.Uow.Abstracts;
using XiHan.Framework.Uow.Attributes;
using XiHan.Framework.Uow.Options;

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
        var options = CreateOptions(scope.ServiceProvider, invocation, unitOfWorkAttribute);

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

    /// <summary>
    /// 创建选项
    /// </summary>
    /// <param name="serviceProvider"></param>
    /// <param name="invocation"></param>
    /// <param name="unitOfWorkAttribute"></param>
    /// <returns></returns>
    private static XiHanUnitOfWorkOptions CreateOptions(IServiceProvider serviceProvider, IXiHanMethodInvocation invocation, UnitOfWorkAttribute? unitOfWorkAttribute)
    {
        var options = new XiHanUnitOfWorkOptions();

        unitOfWorkAttribute?.SetOptions(options);

        if (unitOfWorkAttribute?.IsTransactional != null)
        {
            return options;
        }

        var defaultOptions = serviceProvider.GetRequiredService<IOptions<XiHanUnitOfWorkDefaultOptions>>().Value;
        options.IsTransactional = defaultOptions.CalculateIsTransactional(
            autoValue: serviceProvider.GetRequiredService<IUnitOfWorkTransactionBehaviourProvider>().IsTransactional
            ?? !invocation.Method.Name.StartsWith("Get", StringComparison.InvariantCultureIgnoreCase)
        );

        return options;
    }
}
