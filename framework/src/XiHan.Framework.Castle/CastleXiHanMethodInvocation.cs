#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:CastleXiHanMethodInvocation
// Guid:a1b2c3d4-e5f6-7890-abcd-ef1234567890
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/04/05 05:30:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

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

    /// <inheritdoc />
    public object[] Arguments => _invocation.Arguments!;

    /// <inheritdoc />
    public IReadOnlyDictionary<string, object> ArgumentsDictionary => _lazyArgsDictionary.Value;

    /// <inheritdoc />
    public Type[] GenericArguments => _invocation.GenericArguments ?? [];

    /// <inheritdoc />
    public object TargetObject => _invocation.InvocationTarget ?? _invocation.Proxy;

    /// <inheritdoc />
    public MethodInfo Method => _invocation.MethodInvocationTarget ?? _invocation.Method;

    /// <inheritdoc />
    public object ReturnValue
    {
        get => _invocation.ReturnValue!;
        set => _invocation.ReturnValue = value;
    }

    /// <inheritdoc />
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
