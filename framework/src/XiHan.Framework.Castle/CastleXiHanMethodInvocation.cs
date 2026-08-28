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
    private bool _returnValueOverridden;

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
    /// 方法的返回值，可读取也可覆写；目标方法执行前、方法无返回值或返回 null 时为 null
    /// </summary>
    public object? ReturnValue
    {
        get => _invocation.ReturnValue;
        set
        {
            _returnValueOverridden = true;
            _invocation.ReturnValue = value;
        }
    }

    /// <summary>
    /// 最近一次 <see cref="ProceedAsync"/> 取得的目标返回值；<see cref="Task{TResult}"/> 已拆箱为结果本身
    /// </summary>
    /// <remarks>
    /// 供 <see cref="CastleInterceptorAdapter"/> 取真实结果用。它不能在链路跑完后重读
    /// <see cref="ReturnValue"/>：Castle 的返回值槽位那时已被适配器写成本次拦截产出的包装任务，
    /// 目标方法真异步时重读到的就是包装任务自己，await 下去即自己等自己，调用方永久挂死。
    /// </remarks>
    internal object? ProceedResult { get; private set; }

    /// <summary>
    /// 拦截器是否显式覆写过返回值
    /// </summary>
    /// <remarks>
    /// 用来区分「拦截器主动改写了结果」与「没改写，应当采用目标方法的真实结果」，
    /// 二者都可能落在同一个槽位上，只看值分辨不出来。
    /// </remarks>
    internal bool ReturnValueOverridden => _returnValueOverridden;

    /// <summary>
    /// 继续执行被拦截的原方法，返回值为 Task 时等待其完成
    /// </summary>
    /// <remarks>
    /// 结果同时记进 <see cref="ProceedResult"/>（<see cref="Task{TResult}"/> 已拆箱），
    /// 但刻意不改写 Castle 的返回值槽位——那个槽位是 Castle 用来取方法最终返回值的，
    /// 把它改成裸结果值会让直接使用本类型的拦截器路径拿 int 去当 Task&lt;int&gt; 返回，当场类型转换失败。
    /// </remarks>
    public async Task ProceedAsync()
    {
        _proceedInfo.Invoke();

        ProceedResult = _invocation.ReturnValue;

        if (_invocation.ReturnValue is not Task task)
        {
            return;
        }

        await task;

        var resultProperty = ResolveTaskResultProperty(task.GetType());
        ProceedResult = resultProperty?.GetValue(task);
    }

    /// <summary>
    /// 沿类型继承链找出 <see cref="Task{TResult}"/> 的 Result 属性，非泛型 Task 返回 null
    /// </summary>
    /// <remarks>
    /// 必须沿继承链找：async 方法实际返回的是运行时内部的状态机装箱类型，
    /// 它派生自 <see cref="Task{TResult}"/> 而不等于它，直接判泛型定义会漏掉。
    /// </remarks>
    private static PropertyInfo? ResolveTaskResultProperty(Type? taskType)
    {
        while (taskType is not null)
        {
            if (taskType.IsGenericType && taskType.GetGenericTypeDefinition() == typeof(Task<>))
            {
                return taskType.GetProperty(nameof(Task<object>.Result));
            }

            taskType = taskType.BaseType;
        }

        return null;
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
