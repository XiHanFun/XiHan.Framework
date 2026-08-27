// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Threading.Tests;

/// <summary>
/// 空令牌提供者测试
/// </summary>
/// <remarks>
/// 该类型是进程级单例：默认给出空令牌，只有在 Use 作用域内才给出重写令牌。
/// 因为是单例且重写状态挂在异步本地槽上，每个用例都必须把自己开出去的作用域释放干净。
/// </remarks>
public class NullCancellationTokenProviderTests
{
    /// <summary>
    /// 实例是单例
    /// </summary>
    [Fact]
    public void Instance_IsSingleton()
    {
        Assert.Same(NullCancellationTokenProvider.Instance, NullCancellationTokenProvider.Instance);
    }

    /// <summary>
    /// 实例满足令牌提供者契约
    /// </summary>
    [Fact]
    public void Instance_ImplementsCancellationTokenProviderContract()
    {
        Assert.IsAssignableFrom<ICancellationTokenProvider>(NullCancellationTokenProvider.Instance);
        Assert.IsAssignableFrom<CancellationTokenProviderBase>(NullCancellationTokenProvider.Instance);
    }

    /// <summary>
    /// 没有重写时给出不可取消的空令牌
    /// </summary>
    [Fact]
    public void Token_WithoutOverride_IsNone()
    {
        Assert.Equal(CancellationToken.None, NullCancellationTokenProvider.Instance.Token);
        Assert.False(NullCancellationTokenProvider.Instance.Token.CanBeCanceled);
    }

    /// <summary>
    /// Use 作用域内给出重写令牌，释放后还原为空令牌
    /// </summary>
    [Fact]
    public void Use_WithinScope_OverridesTokenAndRestoresOnDispose()
    {
        using var source = new CancellationTokenSource();

        using (NullCancellationTokenProvider.Instance.Use(source.Token))
        {
            Assert.Equal(source.Token, NullCancellationTokenProvider.Instance.Token);
            Assert.True(NullCancellationTokenProvider.Instance.Token.CanBeCanceled);
        }

        Assert.Equal(CancellationToken.None, NullCancellationTokenProvider.Instance.Token);
    }

    /// <summary>
    /// 嵌套 Use 逐层还原
    /// </summary>
    [Fact]
    public void Use_Nested_RestoresOuterTokenOnInnerDispose()
    {
        using var outerSource = new CancellationTokenSource();
        using var innerSource = new CancellationTokenSource();

        using (NullCancellationTokenProvider.Instance.Use(outerSource.Token))
        {
            Assert.Equal(outerSource.Token, NullCancellationTokenProvider.Instance.Token);

            using (NullCancellationTokenProvider.Instance.Use(innerSource.Token))
            {
                Assert.Equal(innerSource.Token, NullCancellationTokenProvider.Instance.Token);
            }

            Assert.Equal(outerSource.Token, NullCancellationTokenProvider.Instance.Token);
        }

        Assert.Equal(CancellationToken.None, NullCancellationTokenProvider.Instance.Token);
    }

    /// <summary>
    /// 重写令牌跨 await 与子任务继续有效
    /// </summary>
    [Fact]
    public async Task Use_AcrossAwait_KeepsOverride()
    {
        using var source = new CancellationTokenSource();

        using (NullCancellationTokenProvider.Instance.Use(source.Token))
        {
            await Task.Yield();
            Assert.Equal(source.Token, NullCancellationTokenProvider.Instance.Token);

            var observedInChild = await Task.Run(
                () => NullCancellationTokenProvider.Instance.Token,
                TestContext.Current.CancellationToken);
            Assert.Equal(source.Token, observedInChild);
        }

        Assert.Equal(CancellationToken.None, NullCancellationTokenProvider.Instance.Token);
    }
}
