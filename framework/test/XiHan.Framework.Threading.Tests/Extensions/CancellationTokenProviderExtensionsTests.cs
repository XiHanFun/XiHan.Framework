// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Threading.Extensions;

namespace XiHan.Framework.Threading.Tests.Extensions;

/// <summary>
/// 令牌提供者扩展方法测试
/// </summary>
/// <remarks>
/// 语义是「首选令牌为空令牌则回落到提供者的令牌，否则原样返回首选令牌」。
/// 用固定令牌的手写替身，只验证选择逻辑本身，不牵扯环境作用域。
/// </remarks>
public class CancellationTokenProviderExtensionsTests
{
    /// <summary>
    /// 不传首选令牌时回落到提供者的令牌
    /// </summary>
    [Fact]
    public void FallbackToProvider_WithoutPreferredValue_ReturnsProviderToken()
    {
        using var providerSource = new CancellationTokenSource();
        var provider = new FixedCancellationTokenProvider(providerSource.Token);

        Assert.Equal(providerSource.Token, provider.FallbackToProvider());
    }

    /// <summary>
    /// 首选令牌是默认值时回落到提供者的令牌
    /// </summary>
    [Fact]
    public void FallbackToProvider_WithDefaultPreferredValue_ReturnsProviderToken()
    {
        using var providerSource = new CancellationTokenSource();
        var provider = new FixedCancellationTokenProvider(providerSource.Token);

        Assert.Equal(providerSource.Token, provider.FallbackToProvider(default));
    }

    /// <summary>
    /// 首选令牌是空令牌时回落到提供者的令牌
    /// </summary>
    [Fact]
    public void FallbackToProvider_WithNonePreferredValue_ReturnsProviderToken()
    {
        using var providerSource = new CancellationTokenSource();
        var provider = new FixedCancellationTokenProvider(providerSource.Token);

        Assert.Equal(providerSource.Token, provider.FallbackToProvider(CancellationToken.None));
    }

    /// <summary>
    /// 首选令牌有效时原样返回首选令牌，不回落到提供者
    /// </summary>
    [Fact]
    public void FallbackToProvider_WithRealPreferredValue_ReturnsPreferredValue()
    {
        using var providerSource = new CancellationTokenSource();
        using var preferredSource = new CancellationTokenSource();
        var provider = new FixedCancellationTokenProvider(providerSource.Token);

        var result = provider.FallbackToProvider(preferredSource.Token);

        Assert.Equal(preferredSource.Token, result);
        Assert.NotEqual(providerSource.Token, result);
    }

    /// <summary>
    /// 提供者与首选都是空令牌时结果仍是空令牌
    /// </summary>
    [Fact]
    public void FallbackToProvider_WhenBothAreNone_ReturnsNone()
    {
        var provider = new FixedCancellationTokenProvider(CancellationToken.None);

        var result = provider.FallbackToProvider();

        Assert.Equal(CancellationToken.None, result);
        Assert.False(result.CanBeCanceled);
    }

    /// <summary>
    /// 已取消的首选令牌原样返回，取消状态不丢失
    /// </summary>
    [Fact]
    public void FallbackToProvider_WithCancelledPreferredValue_KeepsCancelledState()
    {
        using var providerSource = new CancellationTokenSource();
        using var preferredSource = new CancellationTokenSource();
        preferredSource.Cancel();
        var provider = new FixedCancellationTokenProvider(providerSource.Token);

        var result = provider.FallbackToProvider(preferredSource.Token);

        Assert.Equal(preferredSource.Token, result);
        Assert.True(result.IsCancellationRequested);
    }

    /// <summary>
    /// 提供者给出已取消的令牌时，回落结果同样处于取消状态
    /// </summary>
    [Fact]
    public void FallbackToProvider_WhenProviderTokenIsCancelled_ReturnsCancelledToken()
    {
        using var providerSource = new CancellationTokenSource();
        providerSource.Cancel();
        var provider = new FixedCancellationTokenProvider(providerSource.Token);

        var result = provider.FallbackToProvider();

        Assert.Equal(providerSource.Token, result);
        Assert.True(result.IsCancellationRequested);
    }

    /// <summary>
    /// 固定令牌的令牌提供者替身
    /// </summary>
    private sealed class FixedCancellationTokenProvider : ICancellationTokenProvider
    {
        /// <summary>
        /// 构造函数
        /// </summary>
        public FixedCancellationTokenProvider(CancellationToken token)
        {
            Token = token;
        }

        /// <summary>
        /// 令牌
        /// </summary>
        public CancellationToken Token { get; }

        /// <summary>
        /// 使用，替身不实现重写语义，只返回一个空操作的释放句柄
        /// </summary>
        public IDisposable Use(CancellationToken cancellationToken)
        {
            return new NoopDisposable();
        }

        /// <summary>
        /// 空操作释放句柄
        /// </summary>
        private sealed class NoopDisposable : IDisposable
        {
            /// <summary>
            /// 释放
            /// </summary>
            public void Dispose()
            {
            }
        }
    }
}
