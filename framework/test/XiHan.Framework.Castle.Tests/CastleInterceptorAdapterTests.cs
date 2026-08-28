// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Castle.DynamicProxy;
using XiHan.Framework.Castle.Tests.TestDoubles;
using XiHan.Framework.Core.DynamicProxy;

namespace XiHan.Framework.Castle.Tests;

/// <summary>
/// Castle 拦截器适配器测试
/// </summary>
/// <remarks>
/// 适配器按返回类型分四条分派路径：void、Task、Task&lt;T&gt;、其它同步返回值。
/// 每条路径都用真实 Castle 接口代理跑通，重点验证参数与返回值的双向传递、
/// 拦截器链的先后次序、短路语义与异常透传（异常必须保持原类型，不能被包成 AggregateException）。
/// </remarks>
public class CastleInterceptorAdapterTests
{
    private static readonly ProxyGenerator Generator = new();

    /// <summary>
    /// 无拦截器时代理等价于直通目标方法
    /// </summary>
    [Fact]
    public void Intercept_WithoutInterceptors_ReturnsTargetResult()
    {
        var target = new SyncSampleService();
        var proxy = CreateProxy<ISyncSampleService>(target);

        var result = proxy.Concat("ab", 2);

        Assert.Equal("abab", result);
        Assert.Equal(1, target.ConcatCallCount);
    }

    /// <summary>
    /// 同步返回值方法：拦截器能读到实参，也能读到目标方法的返回值
    /// </summary>
    [Fact]
    public void Intercept_SyncMethod_ExposesArgumentsAndReturnValue()
    {
        var log = new CallLog();
        var proxy = CreateProxy<ISyncSampleService>(new SyncSampleService(), new DelegatingInterceptor(async invocation =>
        {
            log.Add($"前:{invocation.Arguments[0]}:{invocation.Arguments[1]}");
            await invocation.ProceedAsync();
            log.Add($"后:{invocation.ReturnValue}");
        }));

        var result = proxy.Concat("ab", 2);

        Assert.Equal("abab", result);
        Assert.Equal(2, log.Entries.Count);
        Assert.Equal("前:ab:2", log.Entries[0]);
        Assert.Equal("后:abab", log.Entries[1]);
    }

    /// <summary>
    /// 同步返回值方法：ProceedAsync 之后覆写返回值对调用方生效
    /// </summary>
    [Fact]
    public void Intercept_SyncMethod_InterceptorOverridesReturnValue()
    {
        var proxy = CreateProxy<ISyncSampleService>(new SyncSampleService(), new DelegatingInterceptor(async invocation =>
        {
            await invocation.ProceedAsync();
            invocation.ReturnValue = "改写:" + invocation.ReturnValue;
        }));

        Assert.Equal("改写:abab", proxy.Concat("ab", 2));
    }

    /// <summary>
    /// 同步返回值方法：拦截器不放行时目标方法不执行
    /// </summary>
    [Fact]
    public void Intercept_SyncMethod_InterceptorSkipsProceed_ShortCircuits()
    {
        var target = new SyncSampleService();
        var proxy = CreateProxy<ISyncSampleService>(target, new DelegatingInterceptor(invocation =>
        {
            invocation.ReturnValue = "短路";
            return Task.CompletedTask;
        }));

        Assert.Equal("短路", proxy.Concat("ab", 2));
        Assert.Equal(0, target.ConcatCallCount);
    }

    /// <summary>
    /// void 方法：拦截器前后逻辑与目标方法都被执行
    /// </summary>
    [Fact]
    public void Intercept_VoidMethod_RunsChainAndTarget()
    {
        var target = new SyncSampleService();
        var log = new CallLog();
        var proxy = CreateProxy<ISyncSampleService>(target, new RecordingInterceptor(log, "记录"));

        proxy.Append("x");

        var item = Assert.Single(target.Appended);
        Assert.Equal("x", item);
        Assert.Equal(2, log.Entries.Count);
        Assert.Equal("记录:进入:Append", log.Entries[0]);
        Assert.Equal("记录:离开:Append", log.Entries[1]);
    }

    /// <summary>
    /// 多个拦截器按注册顺序形成洋葱链
    /// </summary>
    /// <remarks>
    /// 先注册的在外层：进入时顺序执行，离开时逆序执行。顺序错了会让缓存、事务这类拦截器语义整体失效。
    /// </remarks>
    [Fact]
    public void Intercept_MultipleInterceptors_RunAsOnionInRegistrationOrder()
    {
        var log = new CallLog();
        var proxy = CreateProxy<ISyncSampleService>(
            new SyncSampleService(),
            new RecordingInterceptor(log, "外"),
            new RecordingInterceptor(log, "内"));

        proxy.Append("x");

        Assert.Equal(4, log.Entries.Count);
        Assert.Equal("外:进入:Append", log.Entries[0]);
        Assert.Equal("内:进入:Append", log.Entries[1]);
        Assert.Equal("内:离开:Append", log.Entries[2]);
        Assert.Equal("外:离开:Append", log.Entries[3]);
    }

    /// <summary>
    /// 泛型方法：拦截器能读到闭合后的泛型实参
    /// </summary>
    [Fact]
    public void Intercept_GenericMethod_ExposesClosedGenericArguments()
    {
        Type[] captured = [];
        var proxy = CreateProxy<ISyncSampleService>(new SyncSampleService(), new DelegatingInterceptor(async invocation =>
        {
            captured = invocation.GenericArguments;
            await invocation.ProceedAsync();
        }));

        var result = proxy.Describe(42);

        Assert.Equal("Int32", result);
        var item = Assert.Single(captured);
        Assert.Equal(typeof(int), item);
    }

    /// <summary>
    /// 拦截器拿到的目标对象是被代理的真实实例
    /// </summary>
    [Fact]
    public void Intercept_ExposesTargetObjectToInterceptor()
    {
        var target = new SyncSampleService();
        object captured = new();
        var proxy = CreateProxy<ISyncSampleService>(target, new DelegatingInterceptor(async invocation =>
        {
            captured = invocation.TargetObject;
            await invocation.ProceedAsync();
        }));

        proxy.Append("x");

        Assert.Same(target, captured);
    }

    /// <summary>
    /// Task 方法：拦截器在 ProceedAsync 之后恢复时，目标方法体已经跑完
    /// </summary>
    [Fact]
    public async Task Intercept_TaskMethod_AwaitsTargetBeforeInterceptorResumes()
    {
        var target = new AsyncSampleService();
        var completedWhenResumed = false;
        var proxy = CreateProxy<IAsyncSampleService>(target, new DelegatingInterceptor(async invocation =>
        {
            await invocation.ProceedAsync();
            completedWhenResumed = target.DelayCompleted;
        }));

        await proxy.DelayAsync();

        Assert.True(completedWhenResumed);
        Assert.True(target.DelayCompleted);
    }

    /// <summary>
    /// Task 方法：拦截器不放行时目标方法不执行且调用方正常结束
    /// </summary>
    [Fact]
    public async Task Intercept_TaskMethod_InterceptorSkipsProceed_TargetNotInvoked()
    {
        var target = new AsyncSampleService();
        var proxy = CreateProxy<IAsyncSampleService>(target, new DelegatingInterceptor(_ => Task.CompletedTask));

        await proxy.MarkAsync();

        Assert.False(target.MarkCalled);
    }

    /// <summary>
    /// Task&lt;T&gt; 方法：返回值原样透出，拦截器链正常执行
    /// </summary>
    [Fact]
    public async Task Intercept_TaskOfTMethod_ReturnsTargetResult()
    {
        var target = new AsyncSampleService();
        var log = new CallLog();
        var proxy = CreateProxy<IAsyncSampleService>(target, new RecordingInterceptor(log, "记录"));

        var result = await proxy.DoubleAsync(3, TestContext.Current.CancellationToken);

        Assert.Equal(6, result);
        Assert.True(target.DoubleCompleted);
        Assert.Equal(2, log.Entries.Count);
        Assert.Equal("记录:进入:DoubleAsync", log.Entries[0]);
        Assert.Equal("记录:离开:DoubleAsync", log.Entries[1]);
    }

    /// <summary>
    /// Task&lt;T&gt; 方法：带取消令牌的实参进入参数字典
    /// </summary>
    [Fact]
    public async Task Intercept_TaskOfTMethod_ExposesArgumentsDictionary()
    {
        IReadOnlyDictionary<string, object> captured = new Dictionary<string, object>();
        var proxy = CreateProxy<IAsyncSampleService>(new AsyncSampleService(), new DelegatingInterceptor(async invocation =>
        {
            captured = invocation.ArgumentsDictionary;
            await invocation.ProceedAsync();
        }));

        await proxy.DoubleAsync(5, TestContext.Current.CancellationToken);

        Assert.Equal(2, captured.Count);
        Assert.True(captured.ContainsKey("value"));
        Assert.True(captured.ContainsKey("cancellationToken"));
        Assert.Equal(5, (int)captured["value"]);
    }

    /// <summary>
    /// Task&lt;T&gt; 方法：ProceedAsync 之后覆写返回值对调用方生效
    /// </summary>
    /// <remarks>
    /// 覆写时写入的是拆箱后的结果值而不是 Task，适配器要能识别这两种形态。
    /// </remarks>
    [Fact]
    public async Task Intercept_TaskOfTMethod_InterceptorOverridesResult()
    {
        var proxy = CreateProxy<IAsyncSampleService>(new AsyncSampleService(), new DelegatingInterceptor(async invocation =>
        {
            await invocation.ProceedAsync();
            invocation.ReturnValue = 99;
        }));

        var result = await proxy.SumAsync(2, 3);

        Assert.Equal(99, result);
    }

    /// <summary>
    /// Task&lt;T&gt; 方法：拦截器不放行但写了返回值时，返回拦截器给的值
    /// </summary>
    [Fact]
    public async Task Intercept_TaskOfTMethod_InterceptorSkipsProceedWithValue_ReturnsThatValue()
    {
        var target = new AsyncSampleService();
        var proxy = CreateProxy<IAsyncSampleService>(target, new DelegatingInterceptor(invocation =>
        {
            invocation.ReturnValue = 7;
            return Task.CompletedTask;
        }));

        var result = await proxy.SumAsync(2, 3);

        Assert.Equal(7, result);
        Assert.False(target.SumCalled);
    }

    /// <summary>
    /// Task&lt;T&gt; 方法：拦截器既不放行也不写返回值时，回落到结果类型的默认值
    /// </summary>
    [Fact]
    public async Task Intercept_TaskOfTMethod_InterceptorSkipsProceedWithoutValue_ReturnsDefault()
    {
        var target = new AsyncSampleService();
        var proxy = CreateProxy<IAsyncSampleService>(target, new DelegatingInterceptor(_ => Task.CompletedTask));

        var result = await proxy.SumAsync(2, 3);

        Assert.Equal(0, result);
        Assert.False(target.SumCalled);
    }

    /// <summary>
    /// ValueTask&lt;T&gt; 方法：走同步返回值分支，结果原样透出
    /// </summary>
    /// <remarks>
    /// 适配器没有为 ValueTask 单独分派，这里锁住"至少不能破坏结果传递"这一条底线契约。
    /// </remarks>
    [Fact]
    public async Task Intercept_ValueTaskMethod_ReturnsTargetResult()
    {
        var log = new CallLog();
        var proxy = CreateProxy<IAsyncSampleService>(new AsyncSampleService(), new RecordingInterceptor(log, "记录"));

        var result = await proxy.TripleAsync(4);

        Assert.Equal(12, result);
        Assert.Equal(2, log.Entries.Count);
    }

    /// <summary>
    /// 同步方法：目标抛出的异常保持原类型与原消息
    /// </summary>
    [Fact]
    public void Intercept_SyncMethod_TargetThrows_PropagatesOriginalException()
    {
        var log = new CallLog();
        var proxy = CreateProxy<ISyncSampleService>(new SyncSampleService(), new RecordingInterceptor(log, "记录"));

        var exception = Assert.Throws<InvalidOperationException>(() => proxy.Fail());

        Assert.Equal(SyncSampleService.FailureMessage, exception.Message);

        // 异常打断了链，"离开"那一条不应该被记下来
        var item = Assert.Single(log.Entries);
        Assert.Equal("记录:进入:Fail", item);
    }

    /// <summary>
    /// Task 方法：目标抛出的异常保持原类型与原消息
    /// </summary>
    [Fact]
    public async Task Intercept_TaskMethod_TargetThrows_PropagatesOriginalException()
    {
        var proxy = CreateProxy<IAsyncSampleService>(new AsyncSampleService(), new RecordingInterceptor(new CallLog(), "记录"));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => proxy.FailVoidAsync());

        Assert.Equal(AsyncSampleService.FailureMessage, exception.Message);
    }

    /// <summary>
    /// Task&lt;T&gt; 方法：目标抛出的异常保持原类型与原消息
    /// </summary>
    [Fact]
    public async Task Intercept_TaskOfTMethod_TargetThrows_PropagatesOriginalException()
    {
        var proxy = CreateProxy<IAsyncSampleService>(new AsyncSampleService(), new RecordingInterceptor(new CallLog(), "记录"));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => proxy.FailAsync());

        Assert.Equal(AsyncSampleService.FailureMessage, exception.Message);
    }

    /// <summary>
    /// 拦截器自身抛异常时原样透传，且目标方法不执行
    /// </summary>
    [Fact]
    public void Intercept_InterceptorThrows_PropagatesAndTargetNotInvoked()
    {
        var target = new SyncSampleService();
        var proxy = CreateProxy<ISyncSampleService>(target, new DelegatingInterceptor(_ => throw new InvalidOperationException("拦截器失败")));

        var exception = Assert.Throws<InvalidOperationException>(() => proxy.Concat("ab", 2));

        Assert.Equal("拦截器失败", exception.Message);
        Assert.Equal(0, target.ConcatCallCount);
    }

    /// <summary>
    /// 外层拦截器能捕获内层链抛出的异常并改写为正常返回值
    /// </summary>
    [Fact]
    public void Intercept_OuterInterceptorCatchesInnerException_CanRecover()
    {
        var proxy = CreateProxy<ISyncSampleService>(
            new SyncSampleService(),
            new DelegatingInterceptor(async invocation =>
            {
                try
                {
                    await invocation.ProceedAsync();
                }
                catch (InvalidOperationException)
                {
                    invocation.ReturnValue = -1;
                }
            }));

        Assert.Equal(-1, proxy.Fail());
    }

    /// <summary>
    /// 用被测适配器包装目标对象，生成接口代理
    /// </summary>
    /// <typeparam name="TService">服务接口</typeparam>
    /// <param name="target">目标实例</param>
    /// <param name="interceptors">拦截器链，按数组顺序由外到内</param>
    /// <returns>接口代理</returns>
    private static TService CreateProxy<TService>(TService target, params IXiHanInterceptor[] interceptors)
        where TService : class
    {
        return Generator.CreateInterfaceProxyWithTarget(target, new CastleInterceptorAdapter(interceptors));
    }
}
