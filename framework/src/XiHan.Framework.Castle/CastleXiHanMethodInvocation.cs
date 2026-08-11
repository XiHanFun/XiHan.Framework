// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Castle.DynamicProxy;
using System.Reflection;
using XiHan.Framework.Core.DynamicProxy;

namespace XiHan.Framework.Castle;

/// <summary>
/// Castle 动态代理方法调用适配器，将 Castle 的 IInvocation 适配为框架的 IXiHanMethodInvocation
/// </summary>
public class CastleXiHanMethodInvocation : IXiHanMethodInvocation
{
    private readonly IInvocation _invocation;
    private readonly IInvocationProceedInfo _proceedInfo;
    private readonly Lazy<IReadOnlyDictionary<string, object>> _lazyArgsDictionary;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="invocation"></param>
    /// <param name="proceedInfo"></param>
    public CastleXiHanMethodInvocation(IInvocation invocation, IInvocationProceedInfo proceedInfo)
    {
        _invocation = invocation;
        _proceedInfo = proceedInfo;
        _lazyArgsDictionary = new Lazy<IReadOnlyDictionary<string, object>>(BuildArgumentsDictionary);
    }

    /// <summary>
    /// 本次调用传入的参数数组
    /// </summary>
    public object[] Arguments => _invocation.Arguments!;

    /// <summary>
    /// 以参数名为键的参数字典，首次访问时按方法签名构建
    /// </summary>
    public IReadOnlyDictionary<string, object> ArgumentsDictionary => _lazyArgsDictionary.Value;

    /// <summary>
    /// 本次调用的泛型参数，无泛型参数时为空数组
    /// </summary>
    public Type[] GenericArguments => _invocation.GenericArguments ?? [];

    /// <summary>
    /// 被调用的目标对象，无目标实例时取代理对象
    /// </summary>
    public object TargetObject => _invocation.InvocationTarget ?? _invocation.Proxy;

    /// <summary>
    /// 被调用的方法，优先取目标类型上的实现方法
    /// </summary>
    public MethodInfo Method => _invocation.MethodInvocationTarget ?? _invocation.Method;

    /// <summary>
    /// 方法的返回值，可读取也可覆写
    /// </summary>
    public object ReturnValue
    {
        get => _invocation.ReturnValue!;
        set => _invocation.ReturnValue = value;
    }

    /// <summary>
    /// 继续执行被拦截的原方法，返回值为 Task 时等待其完成
    /// </summary>
    public async Task ProceedAsync()
    {
        _proceedInfo.Invoke();

        if (_invocation.ReturnValue is Task task)
        {
            await task;
        }
    }

    private IReadOnlyDictionary<string, object> BuildArgumentsDictionary()
    {
        var parameters = Method.GetParameters();
        var dict = new Dictionary<string, object>(parameters.Length);

        for (var i = 0; i < parameters.Length; i++)
        {
            dict[parameters[i].Name!] = Arguments[i];
        }

        return dict;
    }
}
