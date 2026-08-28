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
using XiHan.Framework.Uow;
using XiHan.Framework.Uow.Abstracts;
using XiHan.Framework.Uow.Options;
using XiHan.Framework.Web.Api.DynamicApi.Attributes;
using XiHan.Framework.Web.Api.DynamicApi.Controllers;
using XiHan.Framework.Web.Api.DynamicApi.Conventions;
using XiHan.Framework.Web.Api.DynamicApi.Options;
using XiHan.Framework.Web.Api.Filters;

namespace XiHan.Framework.Web.Api.Tests.DynamicApiGeneration;

/// <summary>
/// 动态 API 动作的工作单元边界测试
/// </summary>
/// <remarks>
/// 覆盖「应用服务方法上的 [UnitOfWork] 在 HTTP 入口必须真实生效」这一契约：
/// 动态控制器注入的是应用服务的具体类、不经过接口动态代理，一旦工作单元只挂在拦截器上，
/// 写入会逐条自动提交，动作抛异常也不回滚。
/// 本类内的用例共享 <see cref="DynamicApiControllerFactory"/> 的静态缓存，故不并行执行。
/// </remarks>
[Collection("DynamicApiFactory")]
public class DynamicApiUnitOfWorkTests : IDisposable
{
    /// <summary>
    /// 构造函数
    /// </summary>
    public DynamicApiUnitOfWorkTests()
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
    /// 动态控制器动作能回查到应用服务方法上的工作单元特性
    /// </summary>
    [Fact]
    public void ResolveUnitOfWorkMethod_ForDynamicAction_FindsServiceMethodAttribute()
    {
        var actionMethod = GetDynamicActionMethod(nameof(TransactionalAppService.CreateItemAsync));

        var resolved = XiHanUnitOfWorkFilter.ResolveUnitOfWorkMethodOrNull(CreateActionDescriptor(actionMethod));

        Assert.NotNull(resolved);
        Assert.Equal(typeof(TransactionalAppService), resolved.DeclaringType);
        Assert.True(UnitOfWorkHelper.IsUnitOfWorkMethod(resolved, out var attribute));
        Assert.True(attribute?.IsTransactional);
    }

    /// <summary>
    /// 未标注工作单元的应用服务方法不开启工作单元
    /// </summary>
    [Fact]
    public async Task ActionWithoutUnitOfWorkAttribute_RunsWithoutUnitOfWork()
    {
        var actionMethod = GetDynamicActionMethod(nameof(TransactionalAppService.GetItemAsync));

        var execution = await ExecuteFilterAsync(actionMethod, throwInAction: false);

        Assert.Null(execution.UnitOfWorkDuringAction);
    }

    /// <summary>
    /// 动作正常返回时提交事务
    /// </summary>
    [Fact]
    public async Task ActionSucceeded_CommitsTransaction()
    {
        var actionMethod = GetDynamicActionMethod(nameof(TransactionalAppService.CreateItemAsync));

        var execution = await ExecuteFilterAsync(actionMethod, throwInAction: false);

        Assert.NotNull(execution.UnitOfWorkDuringAction);
        Assert.True(execution.UnitOfWorkDuringAction.Options.IsTransactional);
        Assert.True(execution.UnitOfWorkDuringAction.IsCompleted);
        Assert.True(execution.TransactionApi.Committed);
    }

    /// <summary>
    /// 动作抛出异常时不提交，事务随工作单元释放回滚
    /// </summary>
    [Fact]
    public async Task ActionThrew_RollsBackTransaction()
    {
        var actionMethod = GetDynamicActionMethod(nameof(TransactionalAppService.CreateItemAsync));

        var execution = await ExecuteFilterAsync(actionMethod, throwInAction: true);

        Assert.NotNull(execution.UnitOfWorkDuringAction);
        Assert.False(execution.UnitOfWorkDuringAction.IsCompleted);
        Assert.False(execution.TransactionApi.Committed);
        Assert.True(execution.TransactionApi.RolledBack);
    }

    /// <summary>
    /// 异常已被内层过滤器接管时同样不提交
    /// </summary>
    [Fact]
    public async Task ActionThrewAndExceptionHandled_StillRollsBackTransaction()
    {
        var actionMethod = GetDynamicActionMethod(nameof(TransactionalAppService.CreateItemAsync));

        var execution = await ExecuteFilterAsync(actionMethod, throwInAction: true, exceptionHandled: true);

        Assert.False(execution.TransactionApi.Committed);
        Assert.True(execution.TransactionApi.RolledBack);
    }

    /// <summary>
    /// 执行一次过滤器包裹下的动作，并回收工作单元与事务的观测结果
    /// </summary>
    private static async Task<FilterExecutionResult> ExecuteFilterAsync(
        MethodInfo actionMethod,
        bool throwInAction,
        bool exceptionHandled = false)
    {
        await using var provider = BuildServiceProvider();

        var ambientUnitOfWork = provider.GetRequiredService<IAmbientUnitOfWork>();
        var filter = new XiHanUnitOfWorkFilter(provider.GetRequiredService<IUnitOfWorkManager>());

        var httpContext = new DefaultHttpContext { RequestServices = provider };
        var actionContext = new ActionContext(httpContext, new RouteData(), CreateActionDescriptor(actionMethod));
        var executingContext = new ActionExecutingContext(
            actionContext,
            [],
            new Dictionary<string, object?>(),
            controller: new object());

        var result = new FilterExecutionResult();

        await filter.OnActionExecutionAsync(executingContext, () =>
        {
            var current = ambientUnitOfWork.UnitOfWork;
            result.UnitOfWorkDuringAction = current;
            current?.GetOrAddTransactionApi("test", () => result.TransactionApi);

            var executedContext = new ActionExecutedContext(actionContext, [], controller: new object());
            if (throwInAction)
            {
                executedContext.Exception = new InvalidOperationException("动作执行失败");
                executedContext.ExceptionHandled = exceptionHandled;
            }

            return Task.FromResult(executedContext);
        });

        return result;
    }

    /// <summary>
    /// 构建承载工作单元所需服务的容器
    /// </summary>
    private static ServiceProvider BuildServiceProvider()
    {
        var services = new ServiceCollection();

        services.AddLogging();
        services.AddOptions<XiHanUnitOfWorkDefaultOptions>();
        services.AddSingleton<IAmbientUnitOfWork, AmbientUnitOfWork>();
        services.AddSingleton<IUnitOfWorkManager, UnitOfWorkManager>();
        services.AddSingleton<IUnitOfWorkEventPublisher, NullUnitOfWorkEventPublisher>();
        services.AddSingleton<IUnitOfWorkTransactionBehaviourProvider, NullUnitOfWorkTransactionBehaviourProvider>();
        services.AddTransient<IUnitOfWork, UnitOfWork>();

        return services.BuildServiceProvider();
    }

    /// <summary>
    /// 取动态控制器上转发指定应用服务方法的动作
    /// </summary>
    private static MethodInfo GetDynamicActionMethod(string serviceMethodName)
    {
        var options = new DynamicApiOptions();
        var controllerType = DynamicApiControllerFactory.CreateControllerType(
            typeof(TransactionalAppService), new DefaultDynamicApiConvention(options), options);

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
        /// 动作执行期间的环境工作单元
        /// </summary>
        public IUnitOfWork? UnitOfWorkDuringAction { get; set; }

        /// <summary>
        /// 登记进工作单元的事务
        /// </summary>
        public RecordingTransactionApi TransactionApi { get; } = new();
    }

    /// <summary>
    /// 记录提交与回滚动作的事务 API
    /// </summary>
    /// <remarks>
    /// 释放时若未提交则视为回滚，与 SqlSugar 事务适配器一致。
    /// </remarks>
    private sealed class RecordingTransactionApi : ITransactionApi, ISupportsRollback
    {
        /// <summary>
        /// 是否已提交
        /// </summary>
        public bool Committed { get; private set; }

        /// <summary>
        /// 是否已回滚
        /// </summary>
        public bool RolledBack { get; private set; }

        /// <summary>
        /// 提交
        /// </summary>
        public Task CommitAsync(CancellationToken cancellationToken = default)
        {
            Committed = true;
            return Task.CompletedTask;
        }

        /// <summary>
        /// 回滚
        /// </summary>
        public Task RollbackAsync(CancellationToken cancellationToken = default)
        {
            RolledBack = true;
            return Task.CompletedTask;
        }

        /// <summary>
        /// 释放，未提交则回滚
        /// </summary>
        public void Dispose()
        {
            if (!Committed)
            {
                RolledBack = true;
            }
        }
    }
}
