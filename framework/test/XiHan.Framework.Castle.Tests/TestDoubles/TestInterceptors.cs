// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Core.DynamicProxy;

namespace XiHan.Framework.Castle.Tests.TestDoubles;

/// <summary>
/// 把拦截逻辑交给委托的拦截器
/// </summary>
/// <remarks>
/// 本仓禁止引入 Moq/NSubstitute，用它来手写各种一次性拦截行为。
/// </remarks>
public sealed class DelegatingInterceptor : IXiHanInterceptor
{
    private readonly Func<IXiHanMethodInvocation, Task> _handler;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="handler">拦截处理委托</param>
    public DelegatingInterceptor(Func<IXiHanMethodInvocation, Task> handler)
    {
        _handler = handler;
    }

    /// <summary>
    /// 异步拦截
    /// </summary>
    /// <param name="invocation">方法调用</param>
    /// <returns>任务</returns>
    public Task InterceptAsync(IXiHanMethodInvocation invocation)
    {
        return _handler(invocation);
    }
}

/// <summary>
/// 在目标方法前后各记一条轨迹的拦截器
/// </summary>
public sealed class RecordingInterceptor : XiHanInterceptor
{
    private readonly CallLog _log;
    private readonly string _name;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="log">调用轨迹记录器</param>
    /// <param name="name">拦截器名称，用于区分链上不同层</param>
    public RecordingInterceptor(CallLog log, string name)
    {
        _log = log;
        _name = name;
    }

    /// <summary>
    /// 异步拦截
    /// </summary>
    /// <param name="invocation">方法调用</param>
    /// <returns>任务</returns>
    public override async Task InterceptAsync(IXiHanMethodInvocation invocation)
    {
        _log.Add($"{_name}:进入:{invocation.Method.Name}");
        await invocation.ProceedAsync();
        _log.Add($"{_name}:离开:{invocation.Method.Name}");
    }
}

/// <summary>
/// 由容器构造的日志拦截器
/// </summary>
public sealed class LoggingInterceptor : XiHanInterceptor
{
    private readonly CallLog _log;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="log">调用轨迹记录器</param>
    public LoggingInterceptor(CallLog log)
    {
        _log = log;
    }

    /// <summary>
    /// 异步拦截
    /// </summary>
    /// <param name="invocation">方法调用</param>
    /// <returns>任务</returns>
    public override async Task InterceptAsync(IXiHanMethodInvocation invocation)
    {
        _log.Add($"日志:{invocation.Method.Name}");
        await invocation.ProceedAsync();
    }
}

/// <summary>
/// 由容器构造的审计拦截器，用于验证多拦截器的先后次序
/// </summary>
public sealed class AuditInterceptor : XiHanInterceptor
{
    private readonly CallLog _log;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="log">调用轨迹记录器</param>
    public AuditInterceptor(CallLog log)
    {
        _log = log;
    }

    /// <summary>
    /// 异步拦截
    /// </summary>
    /// <param name="invocation">方法调用</param>
    /// <returns>任务</returns>
    public override async Task InterceptAsync(IXiHanMethodInvocation invocation)
    {
        _log.Add($"审计:{invocation.Method.Name}");
        await invocation.ProceedAsync();
    }
}
