// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Threading.Extensions;

namespace XiHan.Framework.Threading.Tests;

/// <summary>
/// 令牌提供者基类测试
/// </summary>
/// <remarks>
/// 基类是抽象的，用例内定义最小具体子类，只把抽象的令牌属性实现为「有重写取重写，无重写取空令牌」，
/// 并额外暴露基类的重写读取结果供断言。
/// 重点锁定 Use 的还原语义：嵌套进入取最内层，逐层释放逐层还原，跨 await 继续有效，并行分支互不串。
/// 重写上下文键是被外部依赖的协议常量，必须锁死字面量。
/// </remarks>
public class CancellationTokenProviderBaseTests
{
    /// <summary>
    /// 重写上下文键是对外协议常量，不允许漂移
    /// </summary>
    [Fact]
    public void CancellationTokenOverrideContextKey_IsStable()
    {
        Assert.Equal(
            "XiHan.Framework.Threading.CancellationToken.Override",
            CancellationTokenProviderBase.CancellationTokenOverrideContextKey);
    }

    /// <summary>
    /// 提供者满足令牌提供者契约
    /// </summary>
    [Fact]
    public void Provider_ImplementsCancellationTokenProviderContract()
    {
        var provider = new TestCancellationTokenProvider();

        Assert.IsAssignableFrom<ICancellationTokenProvider>(provider);
        Assert.IsAssignableFrom<CancellationTokenProviderBase>(provider);
    }

    /// <summary>
    /// 没有重写时重写值为空，令牌回落到空令牌
    /// </summary>
    [Fact]
    public void Token_WithoutOverride_IsNone()
    {
        var provider = new TestCancellationTokenProvider();

        Assert.Null(provider.CurrentOverride);
        Assert.Equal(CancellationToken.None, provider.Token);
    }

    /// <summary>
    /// Use 作用域内取到重写令牌，释放后还原为空令牌
    /// </summary>
    [Fact]
    public void Use_WithinScope_OverridesTokenAndRestoresOnDispose()
    {
        var provider = new TestCancellationTokenProvider();
        using var source = new CancellationTokenSource();

        using (var scope = provider.Use(source.Token))
        {
            Assert.NotNull(scope);
            Assert.NotNull(provider.CurrentOverride);
            Assert.Equal(source.Token, provider.Token);
        }

        Assert.Null(provider.CurrentOverride);
        Assert.Equal(CancellationToken.None, provider.Token);
    }

    /// <summary>
    /// 嵌套 Use 取最内层，内层释放后还原到外层
    /// </summary>
    [Fact]
    public void Use_Nested_RestoresOuterTokenOnInnerDispose()
    {
        var provider = new TestCancellationTokenProvider();
        using var outerSource = new CancellationTokenSource();
        using var innerSource = new CancellationTokenSource();

        using (provider.Use(outerSource.Token))
        {
            Assert.Equal(outerSource.Token, provider.Token);

            using (provider.Use(innerSource.Token))
            {
                Assert.Equal(innerSource.Token, provider.Token);
            }

            Assert.Equal(outerSource.Token, provider.Token);
        }

        Assert.Equal(CancellationToken.None, provider.Token);
    }

    /// <summary>
    /// 重写令牌跨 await 与子任务继续有效
    /// </summary>
    [Fact]
    public async Task Use_AcrossAwait_KeepsOverride()
    {
        var provider = new TestCancellationTokenProvider();
        using var source = new CancellationTokenSource();

        using (provider.Use(source.Token))
        {
            await Task.Yield();
            Assert.Equal(source.Token, provider.Token);

            var observedInChild = await Task.Run(() => provider.Token, TestContext.Current.CancellationToken);
            Assert.Equal(source.Token, observedInChild);
        }

        Assert.Equal(CancellationToken.None, provider.Token);
    }

    /// <summary>
    /// 并行分支各自的重写互不干扰，也不回灌到父流
    /// </summary>
    [Fact]
    public async Task Use_InParallelTasks_IsIsolatedPerBranch()
    {
        var provider = new TestCancellationTokenProvider();
        var sources = Enumerable.Range(0, 6).Select(_ => new CancellationTokenSource()).ToArray();

        try
        {
            var observed = await Task.WhenAll(sources.Select(source => Task.Run(async () =>
            {
                using (provider.Use(source.Token))
                {
                    await Task.Yield();
                    return provider.Token;
                }
            }, TestContext.Current.CancellationToken)));

            for (var index = 0; index < sources.Length; index++)
            {
                Assert.Equal(sources[index].Token, observed[index]);
            }

            Assert.Equal(CancellationToken.None, provider.Token);
        }
        finally
        {
            foreach (var source in sources)
            {
                source.Dispose();
            }
        }
    }

    /// <summary>
    /// 重写为已取消的令牌时，作用域内暴露取消状态，释放后不再取消
    /// </summary>
    [Fact]
    public void Use_WithCancelledToken_ExposesCancelledStateOnlyInsideScope()
    {
        var provider = new TestCancellationTokenProvider();
        using var source = new CancellationTokenSource();
        source.Cancel();

        using (provider.Use(source.Token))
        {
            Assert.True(provider.Token.IsCancellationRequested);
        }

        Assert.False(provider.Token.IsCancellationRequested);
    }

    /// <summary>
    /// 回落扩展在 Use 作用域内取到重写令牌，作用域外取到空令牌
    /// </summary>
    [Fact]
    public void FallbackToProvider_WithinUseScope_ReturnsOverriddenToken()
    {
        var provider = new TestCancellationTokenProvider();
        using var source = new CancellationTokenSource();

        using (provider.Use(source.Token))
        {
            Assert.Equal(source.Token, provider.FallbackToProvider());
        }

        Assert.Equal(CancellationToken.None, provider.FallbackToProvider());
    }

    /// <summary>
    /// 令牌提供者基类的最小具体实现，额外把基类的重写读取结果暴露出来供断言
    /// </summary>
    private sealed class TestCancellationTokenProvider : CancellationTokenProviderBase
    {
        /// <summary>
        /// 构造函数
        /// </summary>
        public TestCancellationTokenProvider()
            : base(new AmbientDataContextAmbientScopeProvider<CancellationTokenOverride>(new AsyncLocalAmbientDataContext()))
        {
        }

        /// <summary>
        /// 令牌
        /// </summary>
        public override CancellationToken Token => OverrideValue?.CancellationToken ?? CancellationToken.None;

        /// <summary>
        /// 当前重写值
        /// </summary>
        public CancellationTokenOverride? CurrentOverride => OverrideValue;
    }
}
