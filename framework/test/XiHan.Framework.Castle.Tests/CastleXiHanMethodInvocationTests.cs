// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Castle.DynamicProxy;
using XiHan.Framework.Castle.Tests.TestDoubles;

namespace XiHan.Framework.Castle.Tests;

/// <summary>
/// Castle 方法调用适配器测试
/// </summary>
/// <remarks>
/// 全部用真实的 Castle 接口代理产生 <c>IInvocation</c>，再手工包成 <c>CastleXiHanMethodInvocation</c>，
/// 这样断言的是适配器在真实代理语义下的表现，而不是自造的假 IInvocation。
/// 本文件里的样例方法要么是同步方法，要么是同步完成的异步方法，
/// 因此 <c>ProceedAsync()</c> 一定同步完成，不存在阻塞测试线程的风险。
/// </remarks>
public class CastleXiHanMethodInvocationTests
{
    private static readonly ProxyGenerator Generator = new();

    /// <summary>
    /// Arguments 原样暴露本次调用的实参
    /// </summary>
    [Fact]
    public void Arguments_ForSyncMethod_ExposesCallArguments()
    {
        object[] captured = [];
        var proxy = CreateProbedProxy<ISyncSampleService>(new SyncSampleService(), invocation =>
        {
            captured = invocation.Arguments;
            Proceed(invocation);
        });

        proxy.Concat("ab", 2);

        Assert.Equal(2, captured.Length);
        Assert.Equal("ab", (string)captured[0]);
        Assert.Equal(2, (int)captured[1]);
    }

    /// <summary>
    /// ArgumentsDictionary 以方法签名的形参名为键
    /// </summary>
    [Fact]
    public void ArgumentsDictionary_ForSyncMethod_IsKeyedByParameterName()
    {
        IReadOnlyDictionary<string, object> captured = new Dictionary<string, object>();
        var proxy = CreateProbedProxy<ISyncSampleService>(new SyncSampleService(), invocation =>
        {
            captured = invocation.ArgumentsDictionary;
            Proceed(invocation);
        });

        proxy.Concat("ab", 2);

        Assert.Equal(2, captured.Count);
        Assert.True(captured.ContainsKey("text"));
        Assert.True(captured.ContainsKey("count"));
        Assert.Equal("ab", (string)captured["text"]);
        Assert.Equal(2, (int)captured["count"]);
    }

    /// <summary>
    /// ArgumentsDictionary 多次访问命中同一份缓存
    /// </summary>
    /// <remarks>
    /// 参数字典按 Lazy 构建，重复访问必须复用同一实例，否则每个拦截器都会重算一遍签名。
    /// </remarks>
    [Fact]
    public void ArgumentsDictionary_AccessedTwice_ReturnsSameInstance()
    {
        IReadOnlyDictionary<string, object> first = new Dictionary<string, object>();
        IReadOnlyDictionary<string, object> second = new Dictionary<string, object>();
        var proxy = CreateProbedProxy<ISyncSampleService>(new SyncSampleService(), invocation =>
        {
            first = invocation.ArgumentsDictionary;
            second = invocation.ArgumentsDictionary;
            Proceed(invocation);
        });

        proxy.Concat("ab", 2);

        Assert.Same(first, second);
    }

    /// <summary>
    /// 无参方法的参数字典为空
    /// </summary>
    [Fact]
    public void ArgumentsDictionary_ForMethodWithoutParameters_IsEmpty()
    {
        IReadOnlyDictionary<string, object> captured = new Dictionary<string, object> { ["占位"] = 1 };
        var proxy = CreateProbedProxy<IAsyncSampleService>(new AsyncSampleService(), invocation =>
        {
            captured = invocation.ArgumentsDictionary;
            Proceed(invocation);
        });

        _ = proxy.MarkAsync();

        Assert.Empty(captured);
    }

    /// <summary>
    /// 非泛型方法的泛型参数为空数组而非 null
    /// </summary>
    [Fact]
    public void GenericArguments_ForNonGenericMethod_IsEmptyArray()
    {
        Type[] captured = [typeof(object)];
        var proxy = CreateProbedProxy<ISyncSampleService>(new SyncSampleService(), invocation =>
        {
            captured = invocation.GenericArguments;
            Proceed(invocation);
        });

        proxy.Append("x");

        Assert.Empty(captured);
    }

    /// <summary>
    /// 泛型方法暴露闭合后的泛型实参
    /// </summary>
    [Fact]
    public void GenericArguments_ForGenericMethod_ContainsClosedTypeArgument()
    {
        Type[] captured = [];
        var proxy = CreateProbedProxy<ISyncSampleService>(new SyncSampleService(), invocation =>
        {
            captured = invocation.GenericArguments;
            Proceed(invocation);
        });

        var result = proxy.Describe(42);

        Assert.Equal("Int32", result);
        Assert.Equal(1, captured.Length);
        Assert.Equal(typeof(int), captured[0]);
    }

    /// <summary>
    /// TargetObject 取的是被代理的目标实例而非代理本身
    /// </summary>
    [Fact]
    public void TargetObject_ForProxyWithTarget_IsTargetInstance()
    {
        var target = new SyncSampleService();
        object captured = new();
        var proxy = CreateProbedProxy<ISyncSampleService>(target, invocation =>
        {
            captured = invocation.TargetObject;
            Proceed(invocation);
        });

        proxy.Append("x");

        Assert.Same(target, captured);
        Assert.NotSame(proxy, captured);
    }

    /// <summary>
    /// Method 取的是目标类型上的实现方法而非接口声明
    /// </summary>
    [Fact]
    public void Method_ForProxyWithTarget_IsImplementationMethod()
    {
        var declaringType = typeof(object);
        var methodName = string.Empty;
        var proxy = CreateProbedProxy<ISyncSampleService>(new SyncSampleService(), invocation =>
        {
            declaringType = invocation.Method.DeclaringType!;
            methodName = invocation.Method.Name;
            Proceed(invocation);
        });

        proxy.Append("x");

        Assert.Equal(typeof(SyncSampleService), declaringType);
        Assert.Equal(nameof(ISyncSampleService.Append), methodName);
    }

    /// <summary>
    /// 目标方法执行前返回值为 null，执行后为目标方法的结果
    /// </summary>
    [Fact]
    public void ReturnValue_BeforeAndAfterProceed_ReflectsTargetExecution()
    {
        object? before = "占位";
        object? after = null;
        var proxy = CreateProbedProxy<ISyncSampleService>(new SyncSampleService(), invocation =>
        {
            before = invocation.ReturnValue;
            Proceed(invocation);
            after = invocation.ReturnValue;
        });

        var result = proxy.Concat("ab", 2);

        Assert.Null(before);
        Assert.Equal("abab", (string)after!);
        Assert.Equal("abab", result);
    }

    /// <summary>
    /// 覆写返回值后调用方拿到的是被覆写的值
    /// </summary>
    [Fact]
    public void ReturnValue_OverwrittenAfterProceed_IsReturnedToCaller()
    {
        var proxy = CreateProbedProxy<ISyncSampleService>(new SyncSampleService(), invocation =>
        {
            Proceed(invocation);
            invocation.ReturnValue = "改写:" + invocation.ReturnValue;
        });

        Assert.Equal("改写:abab", proxy.Concat("ab", 2));
    }

    /// <summary>
    /// 不调用 ProceedAsync 时目标方法不执行
    /// </summary>
    [Fact]
    public void ProceedAsync_NotCalled_TargetMethodIsNotExecuted()
    {
        var target = new SyncSampleService();
        var proxy = CreateProbedProxy<ISyncSampleService>(target, invocation => invocation.ReturnValue = "短路");

        var result = proxy.Concat("ab", 2);

        Assert.Equal("短路", result);
        Assert.Equal(0, target.ConcatCallCount);
    }

    /// <summary>
    /// 对同步完成的 Task 方法，ProceedAsync 返回的任务同步完成且保留目标任务作为返回值
    /// </summary>
    [Fact]
    public async Task ProceedAsync_ForCompletedTaskMethod_CompletesAndKeepsTargetTask()
    {
        var target = new AsyncSampleService();
        var proceedCompleted = false;
        object? returnValue = null;
        var proxy = CreateProbedProxy<IAsyncSampleService>(target, invocation =>
        {
            var proceed = invocation.ProceedAsync();
            proceedCompleted = proceed.IsCompletedSuccessfully;
            returnValue = invocation.ReturnValue;
        });

        var task = proxy.SumAsync(2, 3);

        Assert.True(proceedCompleted);
        Assert.True(target.SumCalled);

        var targetTask = (Task<int>)returnValue!;
        Assert.Same(targetTask, task);
        Assert.Equal(5, await targetTask);
    }

    /// <summary>
    /// 目标方法同步抛出的异常原样透传给调用方
    /// </summary>
    [Fact]
    public void ProceedAsync_WhenTargetThrows_PropagatesOriginalException()
    {
        var proxy = CreateProbedProxy<ISyncSampleService>(new SyncSampleService(), Proceed);

        var exception = Assert.Throws<InvalidOperationException>(() => proxy.Fail());

        Assert.Equal(SyncSampleService.FailureMessage, exception.Message);
    }

    /// <summary>
    /// 以探针包装目标对象，探针内部自行构造被测的方法调用适配器
    /// </summary>
    /// <typeparam name="TService">服务接口</typeparam>
    /// <param name="target">目标实例</param>
    /// <param name="probe">探针</param>
    /// <returns>接口代理</returns>
    private static TService CreateProbedProxy<TService>(TService target, Action<CastleXiHanMethodInvocation> probe)
        where TService : class
    {
        return Generator.CreateInterfaceProxyWithTarget(target, new ProbeInterceptor(probe));
    }

    /// <summary>
    /// 同步驱动 ProceedAsync
    /// </summary>
    /// <param name="invocation">方法调用</param>
    private static void Proceed(CastleXiHanMethodInvocation invocation)
    {
        invocation.ProceedAsync().GetAwaiter().GetResult();
    }

    /// <summary>
    /// 把 Castle 的 IInvocation 包成被测适配器后交给探针
    /// </summary>
    private sealed class ProbeInterceptor : IInterceptor
    {
        private readonly Action<CastleXiHanMethodInvocation> _probe;

        public ProbeInterceptor(Action<CastleXiHanMethodInvocation> probe)
        {
            _probe = probe;
        }

        public void Intercept(IInvocation invocation)
        {
            _probe(new CastleXiHanMethodInvocation(invocation, invocation.CaptureProceedInfo()));
        }
    }
}
