// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Caching.Distributed;

namespace XiHan.Framework.Caching.Tests;

/// <summary>
/// 分布式缓存的错误隐藏策略测试
/// </summary>
/// <remarks>
/// 缓存后端抖动不应该把业务请求带崩，所以默认是「吞掉异常 + 上报异常通知 + 退化为未命中」；
/// 但显式要求不隐藏时必须原样抛出，否则调用方永远发现不了后端已经挂了。
/// </remarks>
public class DistributedCacheErrorHandlingTests
{
    /// <summary>
    /// 选项默认隐藏错误
    /// </summary>
    [Fact]
    public void HideErrors_DefaultsToTrue()
    {
        Assert.True(new XiHanDistributedCacheOptions().HideErrors);
    }

    /// <summary>
    /// 读取失败且不隐藏错误时原样抛出
    /// </summary>
    [Fact]
    public void Get_WhenBackendFails_ThrowsWhenErrorsNotHidden()
    {
        using var context = new DistributedCacheTestContext(new FailingDistributedCacheStore());
        var cache = context.CreateStringKeyed<SampleCacheItem>();

        var exception = Assert.Throws<InvalidOperationException>(() => cache.Get("k1", hideErrors: false));

        Assert.Equal(FailingDistributedCacheStore.FailureMessage, exception.Message);
        Assert.Empty(context.Notifier.Exceptions);
    }

    /// <summary>
    /// 读取失败且隐藏错误时退化为未命中，并上报异常
    /// </summary>
    [Fact]
    public void Get_WhenBackendFails_ReturnsNullAndNotifiesWhenErrorsHidden()
    {
        using var context = new DistributedCacheTestContext(new FailingDistributedCacheStore());
        var cache = context.CreateStringKeyed<SampleCacheItem>();

        Assert.Null(cache.Get("k1", hideErrors: true));
        Assert.Single(context.Notifier.Exceptions);
    }

    /// <summary>
    /// 未显式指定时按选项里的隐藏错误设置执行
    /// </summary>
    [Fact]
    public void Get_WhenHideErrorsNotSpecified_FollowsOptions()
    {
        var options = new XiHanDistributedCacheOptions { HideErrors = false };
        using var context = new DistributedCacheTestContext(new FailingDistributedCacheStore(), options);
        var cache = context.CreateStringKeyed<SampleCacheItem>();

        Assert.Throws<InvalidOperationException>(() => cache.Get("k1"));
    }

    /// <summary>
    /// 默认选项下读取失败被吞掉
    /// </summary>
    [Fact]
    public void Get_WithDefaultOptions_SwallowsBackendFailure()
    {
        using var context = new DistributedCacheTestContext(new FailingDistributedCacheStore());
        var cache = context.CreateStringKeyed<SampleCacheItem>();

        Assert.Null(cache.Get("k1"));
    }

    /// <summary>
    /// 异步读取失败且隐藏错误时退化为未命中
    /// </summary>
    [Fact]
    public async Task GetAsync_WhenBackendFails_ReturnsNullWhenErrorsHidden()
    {
        var token = TestContext.Current.CancellationToken;
        using var context = new DistributedCacheTestContext(new FailingDistributedCacheStore());
        var cache = context.CreateStringKeyed<SampleCacheItem>();

        Assert.Null(await cache.GetAsync("k1", hideErrors: true, token: token));
        Assert.Single(context.Notifier.Exceptions);
    }

    /// <summary>
    /// 异步读取失败且不隐藏错误时原样抛出
    /// </summary>
    [Fact]
    public async Task GetAsync_WhenBackendFails_ThrowsWhenErrorsNotHidden()
    {
        var token = TestContext.Current.CancellationToken;
        using var context = new DistributedCacheTestContext(new FailingDistributedCacheStore());
        var cache = context.CreateStringKeyed<SampleCacheItem>();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => cache.GetAsync("k1", hideErrors: false, token: token));
    }

    /// <summary>
    /// 写入失败且隐藏错误时静默通过
    /// </summary>
    [Fact]
    public void Set_WhenBackendFails_SwallowsWhenErrorsHidden()
    {
        using var context = new DistributedCacheTestContext(new FailingDistributedCacheStore());
        var cache = context.CreateStringKeyed<SampleCacheItem>();

        cache.Set("k1", new SampleCacheItem { Value = "v1" }, hideErrors: true);

        Assert.Single(context.Notifier.Exceptions);
    }

    /// <summary>
    /// 写入失败且不隐藏错误时原样抛出
    /// </summary>
    [Fact]
    public void Set_WhenBackendFails_ThrowsWhenErrorsNotHidden()
    {
        using var context = new DistributedCacheTestContext(new FailingDistributedCacheStore());
        var cache = context.CreateStringKeyed<SampleCacheItem>();

        Assert.Throws<InvalidOperationException>(
            () => cache.Set("k1", new SampleCacheItem { Value = "v1" }, hideErrors: false));
    }

    /// <summary>
    /// 存在性判断失败且隐藏错误时返回不存在
    /// </summary>
    [Fact]
    public void Exists_WhenBackendFails_ReturnsFalseWhenErrorsHidden()
    {
        using var context = new DistributedCacheTestContext(new FailingDistributedCacheStore());
        var cache = context.CreateStringKeyed<SampleCacheItem>();

        Assert.False(cache.Exists("k1", hideErrors: true));
        Assert.Single(context.Notifier.Exceptions);
    }

    /// <summary>
    /// 刷新失败且不隐藏错误时原样抛出
    /// </summary>
    [Fact]
    public void Refresh_WhenBackendFails_ThrowsWhenErrorsNotHidden()
    {
        using var context = new DistributedCacheTestContext(new FailingDistributedCacheStore());
        var cache = context.CreateStringKeyed<SampleCacheItem>();

        Assert.Throws<InvalidOperationException>(() => cache.Refresh("k1", hideErrors: false));
    }

    /// <summary>
    /// 移除失败且隐藏错误时静默通过
    /// </summary>
    [Fact]
    public async Task RemoveAsync_WhenBackendFails_SwallowsWhenErrorsHidden()
    {
        var token = TestContext.Current.CancellationToken;
        using var context = new DistributedCacheTestContext(new FailingDistributedCacheStore());
        var cache = context.CreateStringKeyed<SampleCacheItem>();

        await cache.RemoveAsync("k1", hideErrors: true, token: token);

        Assert.Single(context.Notifier.Exceptions);
    }

    /// <summary>
    /// 多键读取在后端失败且隐藏错误时，返回与键数量一致的空值集合
    /// </summary>
    /// <remarks>
    /// 契约上返回项数必须与入参键数一致，调用方按下标取值；退化时缺项会让调用方越界或错位。
    /// </remarks>
    [Fact]
    public void GetMany_WhenBackendFails_ReturnsDefaultsAlignedToKeys()
    {
        using var context = new DistributedCacheTestContext(new FailingDistributedCacheStore());
        var cache = context.CreateStringKeyed<SampleCacheItem>();

        var result = cache.GetMany(["a", "b"], hideErrors: true);

        Assert.Equal(["a", "b"], result.Select(pair => pair.Key));
        Assert.All(result, pair => Assert.Null(pair.Value));
        Assert.Single(context.Notifier.Exceptions);
    }
}
