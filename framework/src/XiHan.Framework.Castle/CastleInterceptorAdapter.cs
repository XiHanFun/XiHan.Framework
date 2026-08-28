// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Castle.DynamicProxy;
using System.Reflection;
using XiHan.Framework.Core.DynamicProxy;

namespace XiHan.Framework.Castle;

/// <summary>
/// Castle 拦截器适配器，将框架的 IXiHanInterceptor 链适配为 Castle 的 IInterceptor
/// </summary>
public class CastleInterceptorAdapter : IInterceptor
{
    private static readonly MethodInfo HandleAsyncWithResultMethodInfo =
        typeof(CastleInterceptorAdapter).GetMethod(nameof(HandleAsyncWithResult), BindingFlags.NonPublic | BindingFlags.Static)!;

    private readonly IXiHanInterceptor[] _interceptors;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="interceptors"></param>
    public CastleInterceptorAdapter(IXiHanInterceptor[] interceptors)
    {
        _interceptors = interceptors;
    }

    /// <summary>
    /// 拦截方法调用，按返回类型（void、Task、Task&lt;T&gt; 或同步返回值）分派执行拦截器链
    /// </summary>
    /// <param name="invocation">Castle 方法调用上下文</param>
    public void Intercept(IInvocation invocation)
    {
        var proceedInfo = invocation.CaptureProceedInfo();
        var methodInvocation = new CastleXiHanMethodInvocation(invocation, proceedInfo);

        var returnType = invocation.Method.ReturnType;

        if (returnType == typeof(void))
        {
            ExecuteInterceptorChain(methodInvocation);
        }
        else if (returnType == typeof(Task))
        {
            invocation.ReturnValue = ExecuteInterceptorChainAsync(methodInvocation);
        }
        else if (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>))
        {
            var resultType = returnType.GetGenericArguments()[0];
            var method = HandleAsyncWithResultMethodInfo.MakeGenericMethod(resultType);
            invocation.ReturnValue = method.Invoke(null, [this, methodInvocation]);
        }
        else
        {
            ExecuteInterceptorChain(methodInvocation);
        }
    }

    private static async Task<TResult> HandleAsyncWithResult<TResult>(
        CastleInterceptorAdapter adapter,
        CastleXiHanMethodInvocation invocation)
    {
        await adapter.ExecuteRecursiveAsync(invocation, 0);

        // 取值口径：拦截器显式覆写过就用覆写值，否则用 ProceedAsync 记下的目标真实结果。
        // 这里绝不能直接重读 invocation.ReturnValue —— Intercept 已经把本方法产出的包装任务
        // 写进了 Castle 的返回值槽位。目标方法同步完成时本方法早已跑完，读到的还是目标任务，
        // 侥幸正确；目标方法一旦真异步（体内有 await），本方法会先在挂起处把自己写进槽位，
        // 恢复后读到的就是自己，await 下去即自己等自己，调用方永久挂死。
        var value = invocation.ReturnValueOverridden ? invocation.ReturnValue : invocation.ProceedResult;

        return value switch
        {
            Task<TResult> taskWithResult => await taskWithResult,
            TResult result => result,
            _ => default!
        };
    }

    private void ExecuteInterceptorChain(CastleXiHanMethodInvocation invocation)
    {
        ExecuteInterceptorChainAsync(invocation).GetAwaiter().GetResult();
    }

    private async Task ExecuteInterceptorChainAsync(CastleXiHanMethodInvocation invocation)
    {
        await ExecuteRecursiveAsync(invocation, 0);
    }

    private async Task ExecuteRecursiveAsync(CastleXiHanMethodInvocation invocation, int currentIndex)
    {
        if (currentIndex >= _interceptors.Length)
        {
            await invocation.ProceedAsync();
            return;
        }

        var wrapped = new ChainedMethodInvocation(
            invocation,
            () => ExecuteRecursiveAsync(invocation, currentIndex + 1));

        await _interceptors[currentIndex].InterceptAsync(wrapped);
    }

    /// <summary>
    /// 链式方法调用包装器
    /// </summary>
    private sealed class ChainedMethodInvocation : IXiHanMethodInvocation
    {
        private readonly CastleXiHanMethodInvocation _inner;
        private readonly Func<Task> _proceed;

        public ChainedMethodInvocation(CastleXiHanMethodInvocation inner, Func<Task> proceed)
        {
            _inner = inner;
            _proceed = proceed;
        }

        public object[] Arguments => _inner.Arguments;
        public IReadOnlyDictionary<string, object> ArgumentsDictionary => _inner.ArgumentsDictionary;
        public Type[] GenericArguments => _inner.GenericArguments;
        public object TargetObject => _inner.TargetObject;
        public MethodInfo Method => _inner.Method;

        public object? ReturnValue
        {
            get => _inner.ReturnValue;
            set => _inner.ReturnValue = value;
        }

        public Task ProceedAsync() => _proceed();
    }
}
