// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Core.DependencyInjection;
using XiHan.Framework.Core.DynamicProxy;

namespace XiHan.Framework.Core.Tests.DependencyInjection;

/// <summary>
/// 服务注册上下文与注册回调列表测试
/// </summary>
/// <remarks>
/// 注册上下文是各模块往服务上挂拦截器的唯一入口，拦截器列表必须支持去重添加，
/// 且只接受实现了拦截器契约的类型，否则错误会推迟到动态代理阶段才暴露。
/// </remarks>
public class OnServiceRegistredContextTests
{
    /// <summary>
    /// 构造后携带服务类型与实现类型且拦截器列表为空
    /// </summary>
    [Fact]
    public void Constructor_KeepsTypesAndStartsWithEmptyInterceptors()
    {
        var context = new OnServiceRegistredContext(typeof(IOsrContract), typeof(OsrService));

        Assert.Equal(typeof(IOsrContract), context.ServiceType);
        Assert.Equal(typeof(OsrService), context.ImplementationType);
        Assert.Empty(context.Interceptors);
    }

    /// <summary>
    /// 服务类型为空时抛出
    /// </summary>
    [Fact]
    public void Constructor_WhenServiceTypeNull_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
        {
            _ = new OnServiceRegistredContext(null!, typeof(OsrService));
        });
    }

    /// <summary>
    /// 实现类型为空时抛出
    /// </summary>
    [Fact]
    public void Constructor_WhenImplementationTypeNull_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
        {
            _ = new OnServiceRegistredContext(typeof(IOsrContract), null!);
        });
    }

    /// <summary>
    /// 重复尝试添加同一拦截器只保留一份
    /// </summary>
    [Fact]
    public void Interceptors_TryAdd_IsIdempotent()
    {
        var context = new OnServiceRegistredContext(typeof(IOsrContract), typeof(OsrService));

        Assert.True(context.Interceptors.TryAdd<OsrProbeInterceptor>());
        Assert.False(context.Interceptors.TryAdd<OsrProbeInterceptor>());
        Assert.Equal(typeof(OsrProbeInterceptor), Assert.Single(context.Interceptors));
    }

    /// <summary>
    /// 直接添加同一拦截器不去重
    /// </summary>
    [Fact]
    public void Interceptors_Add_AllowsDuplicates()
    {
        var context = new OnServiceRegistredContext(typeof(IOsrContract), typeof(OsrService));

        context.Interceptors.Add<OsrProbeInterceptor>();
        context.Interceptors.Add<OsrProbeInterceptor>();

        Assert.Equal(2, context.Interceptors.Count);
    }

    /// <summary>
    /// 添加非拦截器类型时抛出
    /// </summary>
    [Fact]
    public void Interceptors_WhenTypeIsNotInterceptor_Throws()
    {
        var context = new OnServiceRegistredContext(typeof(IOsrContract), typeof(OsrService));

        Assert.Throws<ArgumentException>(() => context.Interceptors.Add(typeof(OsrService)));
    }

    /// <summary>
    /// 注册回调列表默认不禁用类拦截器
    /// </summary>
    [Fact]
    public void ServiceRegistrationActionList_DefaultsToInterceptorsEnabled()
    {
        var list = new ServiceRegistrationActionList();

        Assert.Empty(list);
        Assert.False(list.IsClassInterceptorsDisabled);
    }

    /// <summary>
    /// 注册回调列表按加入顺序执行
    /// </summary>
    [Fact]
    public void ServiceRegistrationActionList_InvokesActionsInOrder()
    {
        var list = new ServiceRegistrationActionList();
        List<string> calls = [];
        list.Add(_ => calls.Add("first"));
        list.Add(_ => calls.Add("second"));

        var context = new OnServiceRegistredContext(typeof(IOsrContract), typeof(OsrService));
        foreach (var action in list)
        {
            action(context);
        }

        Assert.Equal(2, calls.Count);
        Assert.Equal("first", calls[0]);
        Assert.Equal("second", calls[1]);
    }
}

/// <summary>
/// 注册上下文测试用契约
/// </summary>
internal interface IOsrContract;

/// <summary>
/// 注册上下文测试用实现
/// </summary>
internal class OsrService : IOsrContract;

/// <summary>
/// 注册上下文测试用拦截器
/// </summary>
internal sealed class OsrProbeInterceptor : IXiHanInterceptor
{
    /// <summary>
    /// 异步拦截，直接放行
    /// </summary>
    /// <param name="invocation">方法调用</param>
    /// <returns>任务</returns>
    public Task InterceptAsync(IXiHanMethodInvocation invocation)
    {
        return invocation.ProceedAsync();
    }
}
