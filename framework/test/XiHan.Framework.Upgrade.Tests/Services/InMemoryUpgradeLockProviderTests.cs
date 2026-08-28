// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Upgrade.Services;

namespace XiHan.Framework.Upgrade.Tests.Services;

/// <summary>
/// 内存升级锁提供者测试
/// </summary>
/// <remarks>
/// 升级锁的唯一职责是「同一资源键同一时刻只有一个持有者」。
/// 覆盖互斥、释放后可重入、过期接管、陈旧令牌不得误删新持有者，以及并发下唯一赢家。
/// </remarks>
public class InMemoryUpgradeLockProviderTests
{
    private const string ResourceKey = "SystemUpgrade";

    /// <summary>
    /// 首次获取成功，令牌暴露资源键与非空锁标识且未释放
    /// </summary>
    [Fact]
    public async Task TryAcquireLockAsync_WhenFree_ReturnsTokenWithIdentity()
    {
        var provider = new InMemoryUpgradeLockProvider();

        var token = await provider.TryAcquireLockAsync(ResourceKey, TimeSpan.FromMinutes(5), "node-a", TestContext.Current.CancellationToken);

        Assert.NotNull(token);
        Assert.Equal(ResourceKey, token!.ResourceKey);
        Assert.False(string.IsNullOrWhiteSpace(token.LockId));
        Assert.False(token.IsReleased);
    }

    /// <summary>
    /// 锁被占用时二次获取返回 null，而不是抛异常或阻塞
    /// </summary>
    [Fact]
    public async Task TryAcquireLockAsync_WhenHeld_ReturnsNull()
    {
        var provider = new InMemoryUpgradeLockProvider();
        var cancellationToken = TestContext.Current.CancellationToken;
        var first = await provider.TryAcquireLockAsync(ResourceKey, TimeSpan.FromMinutes(5), "node-a", cancellationToken);
        Assert.NotNull(first);

        var second = await provider.TryAcquireLockAsync(ResourceKey, TimeSpan.FromMinutes(5), "node-b", cancellationToken);

        Assert.Null(second);
    }

    /// <summary>
    /// 不同资源键互不影响
    /// </summary>
    [Fact]
    public async Task TryAcquireLockAsync_WithDifferentResourceKeys_AreIndependent()
    {
        var provider = new InMemoryUpgradeLockProvider();
        var cancellationToken = TestContext.Current.CancellationToken;

        var host = await provider.TryAcquireLockAsync(ResourceKey, TimeSpan.FromMinutes(5), "node-a", cancellationToken);
        var tenant = await provider.TryAcquireLockAsync($"{ResourceKey}:Tenant_1", TimeSpan.FromMinutes(5), "node-a", cancellationToken);

        Assert.NotNull(host);
        Assert.NotNull(tenant);
        Assert.NotEqual(host!.LockId, tenant!.LockId);
    }

    /// <summary>
    /// 释放后锁可以被重新获取
    /// </summary>
    [Fact]
    public async Task ReleaseAsync_ThenAcquireAgain_Succeeds()
    {
        var provider = new InMemoryUpgradeLockProvider();
        var cancellationToken = TestContext.Current.CancellationToken;
        var first = await provider.TryAcquireLockAsync(ResourceKey, TimeSpan.FromMinutes(5), "node-a", cancellationToken);
        Assert.NotNull(first);

        await first!.ReleaseAsync();

        Assert.True(first.IsReleased);
        var second = await provider.TryAcquireLockAsync(ResourceKey, TimeSpan.FromMinutes(5), "node-b", cancellationToken);
        Assert.NotNull(second);
    }

    /// <summary>
    /// 重复释放是幂等的，不抛异常也不会误删他人持有的锁
    /// </summary>
    [Fact]
    public async Task ReleaseAsync_CalledTwice_IsIdempotent()
    {
        var provider = new InMemoryUpgradeLockProvider();
        var cancellationToken = TestContext.Current.CancellationToken;
        var token = await provider.TryAcquireLockAsync(ResourceKey, TimeSpan.FromMinutes(5), "node-a", cancellationToken);
        Assert.NotNull(token);

        await token!.ReleaseAsync();
        await token.ReleaseAsync();

        Assert.True(token.IsReleased);
    }

    /// <summary>
    /// 异步释放（await using 场景）同样会真正释放锁
    /// </summary>
    [Fact]
    public async Task DisposeAsync_ReleasesLock()
    {
        var provider = new InMemoryUpgradeLockProvider();
        var cancellationToken = TestContext.Current.CancellationToken;
        var token = await provider.TryAcquireLockAsync(ResourceKey, TimeSpan.FromMinutes(5), "node-a", cancellationToken);
        Assert.NotNull(token);

        await token!.DisposeAsync();

        Assert.True(token.IsReleased);
        var second = await provider.TryAcquireLockAsync(ResourceKey, TimeSpan.FromMinutes(5), "node-b", cancellationToken);
        Assert.NotNull(second);
    }

    /// <summary>
    /// 已过期的锁可以被其它节点接管，新令牌的锁标识与旧的不同
    /// </summary>
    [Fact]
    public async Task TryAcquireLockAsync_WhenExistingLockExpired_TakesOver()
    {
        var provider = new InMemoryUpgradeLockProvider();
        var cancellationToken = TestContext.Current.CancellationToken;
        var expired = await provider.TryAcquireLockAsync(ResourceKey, TimeSpan.FromMilliseconds(1), "node-a", cancellationToken);
        Assert.NotNull(expired);

        await Task.Delay(80, cancellationToken);

        var current = await provider.TryAcquireLockAsync(ResourceKey, TimeSpan.FromMinutes(5), "node-b", cancellationToken);

        Assert.NotNull(current);
        Assert.NotEqual(expired!.LockId, current!.LockId);
    }

    /// <summary>
    /// 陈旧令牌释放时不得把新持有者的锁一起清掉
    /// </summary>
    [Fact]
    public async Task ReleaseAsync_FromStaleToken_DoesNotReleaseCurrentHolder()
    {
        var provider = new InMemoryUpgradeLockProvider();
        var cancellationToken = TestContext.Current.CancellationToken;
        var stale = await provider.TryAcquireLockAsync(ResourceKey, TimeSpan.FromMilliseconds(1), "node-a", cancellationToken);
        Assert.NotNull(stale);
        await Task.Delay(80, cancellationToken);
        var current = await provider.TryAcquireLockAsync(ResourceKey, TimeSpan.FromMinutes(5), "node-b", cancellationToken);
        Assert.NotNull(current);

        await stale!.ReleaseAsync();

        var third = await provider.TryAcquireLockAsync(ResourceKey, TimeSpan.FromMinutes(5), "node-c", cancellationToken);
        Assert.Null(third);
    }

    /// <summary>
    /// 并发抢锁时有且只有一个赢家
    /// </summary>
    [Fact]
    public async Task TryAcquireLockAsync_UnderConcurrency_HasExactlyOneWinner()
    {
        var provider = new InMemoryUpgradeLockProvider();
        var cancellationToken = TestContext.Current.CancellationToken;

        var tasks = Enumerable.Range(0, 32)
            .Select(index => Task.Run(() => provider.TryAcquireLockAsync(ResourceKey, TimeSpan.FromMinutes(5), $"node-{index}", cancellationToken), cancellationToken))
            .ToArray();
        var tokens = await Task.WhenAll(tasks);

        Assert.Equal(1, tokens.Count(token => token is not null));
    }

    /// <summary>
    /// 资源键为空或空白时抛参数异常
    /// </summary>
    /// <param name="resourceKey">资源键</param>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task TryAcquireLockAsync_WhenResourceKeyBlank_ThrowsArgumentException(string resourceKey)
    {
        var provider = new InMemoryUpgradeLockProvider();

        var exception = await Assert.ThrowsAsync<ArgumentException>(
            async () => await provider.TryAcquireLockAsync(resourceKey, TimeSpan.FromMinutes(5), "node-a", TestContext.Current.CancellationToken));

        Assert.Equal("resourceKey", exception.ParamName);
    }

    /// <summary>
    /// 过期时间不大于零时抛越界异常
    /// </summary>
    [Fact]
    public async Task TryAcquireLockAsync_WhenExpiryNotPositive_ThrowsArgumentOutOfRange()
    {
        var provider = new InMemoryUpgradeLockProvider();
        var cancellationToken = TestContext.Current.CancellationToken;

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            async () => await provider.TryAcquireLockAsync(ResourceKey, TimeSpan.Zero, "node-a", cancellationToken));
        var exception = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            async () => await provider.TryAcquireLockAsync(ResourceKey, TimeSpan.FromSeconds(-1), "node-a", cancellationToken));

        Assert.Equal("expiry", exception.ParamName);
    }

    /// <summary>
    /// 取消令牌已取消时立即抛出，不占用锁
    /// </summary>
    [Fact]
    public async Task TryAcquireLockAsync_WhenTokenCancelled_ThrowsAndKeepsLockFree()
    {
        var provider = new InMemoryUpgradeLockProvider();
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await provider.TryAcquireLockAsync(ResourceKey, TimeSpan.FromMinutes(5), "node-a", cancellationTokenSource.Token));

        var token = await provider.TryAcquireLockAsync(ResourceKey, TimeSpan.FromMinutes(5), "node-b", TestContext.Current.CancellationToken);
        Assert.NotNull(token);
    }
}
