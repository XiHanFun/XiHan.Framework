// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Reflection;
using XiHan.Framework.Core.Collections;
using XiHan.Framework.Core.DependencyInjection;
using XiHan.Framework.Core.DynamicProxy;

namespace XiHan.Framework.Caching.Tests;

/// <summary>
/// 方法调用上下文替身
/// </summary>
/// <remarks>
/// 模拟动态代理链的行为：<see cref="ProceedAsync"/> 把目标方法的产出写回 <see cref="ReturnValue"/>，
/// 异步方法写回的是 Task，与真实代理一致，便于验证拦截器的解包逻辑。
/// </remarks>
internal sealed class FakeMethodInvocation : IXiHanMethodInvocation
{
    private readonly Func<object?>? _proceedResultFactory;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="method">被调用方法</param>
    /// <param name="arguments">实参</param>
    /// <param name="proceedResultFactory">继续执行时产出的返回值</param>
    public FakeMethodInvocation(MethodInfo method, object[] arguments, Func<object?>? proceedResultFactory = null)
    {
        Method = method;
        Arguments = arguments;
        _proceedResultFactory = proceedResultFactory;

        var parameters = method.GetParameters();
        var dictionary = new Dictionary<string, object>(StringComparer.Ordinal);
        for (var i = 0; i < parameters.Length && i < arguments.Length; i++)
        {
            dictionary[parameters[i].Name!] = arguments[i];
        }

        ArgumentsDictionary = dictionary;
    }

    /// <summary>
    /// 实参
    /// </summary>
    public object[] Arguments { get; }

    /// <summary>
    /// 实参字典
    /// </summary>
    public IReadOnlyDictionary<string, object> ArgumentsDictionary { get; }

    /// <summary>
    /// 泛型实参
    /// </summary>
    public Type[] GenericArguments => [];

    /// <summary>
    /// 目标对象
    /// </summary>
    public object TargetObject { get; } = new();

    /// <summary>
    /// 被调用方法
    /// </summary>
    public MethodInfo Method { get; }

    /// <summary>
    /// 返回值
    /// </summary>
    public object? ReturnValue { get; set; }

    /// <summary>
    /// 继续执行的次数
    /// </summary>
    public int ProceedCount { get; private set; }

    /// <summary>
    /// 继续执行目标方法
    /// </summary>
    /// <returns>异步任务</returns>
    public Task ProceedAsync()
    {
        ProceedCount++;
        ReturnValue = _proceedResultFactory?.Invoke();

        return Task.CompletedTask;
    }
}

/// <summary>
/// 服务注册上下文替身
/// </summary>
internal sealed class FakeServiceRegistredContext : IOnServiceRegistredContext
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="implementationType">实现类型</param>
    public FakeServiceRegistredContext(Type implementationType)
    {
        ImplementationType = implementationType;
    }

    /// <summary>
    /// 拦截器列表
    /// </summary>
    public ITypeList<IXiHanInterceptor> Interceptors { get; } = new TypeList<IXiHanInterceptor>();

    /// <summary>
    /// 实现类型
    /// </summary>
    public Type ImplementationType { get; }
}
