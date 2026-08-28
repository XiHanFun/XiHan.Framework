// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Bot.Sms.Abstractions;
using XiHan.Framework.Bot.Sms.Stores;

namespace XiHan.Framework.Bot.Sms.Tests.Stores;

/// <summary>
/// <see cref="DefaultSmsConfigStore"/> 默认短信配置存储测试
/// </summary>
/// <remarks>
/// 默认实现是「恒未配置」的兜底占位：短信凭证不进配置文件，应用层必须以数据库实现覆盖。
/// 因此这里要证明它永远返回 null（触发解析器 fail-closed），而不是返回一个半成品配置。
/// </remarks>
public class DefaultSmsConfigStoreTests
{
    /// <summary>
    /// 默认实现恒返回 null，表示未配置
    /// </summary>
    [Fact]
    public async Task GetAsync_Always_ReturnsNull()
    {
        var store = new DefaultSmsConfigStore();

        var config = await store.GetAsync(TestContext.Current.CancellationToken);

        Assert.Null(config);
    }

    /// <summary>
    /// 连续多次调用结果一致，不存在隐藏状态
    /// </summary>
    [Fact]
    public async Task GetAsync_RepeatedCalls_StayNull()
    {
        var store = new DefaultSmsConfigStore();

        Assert.Null(await store.GetAsync(TestContext.Current.CancellationToken));
        Assert.Null(await store.GetAsync(TestContext.Current.CancellationToken));
        Assert.Null(await store.GetAsync(TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// 不传取消令牌时使用默认令牌，同样返回 null
    /// </summary>
    [Fact]
    public async Task GetAsync_WithoutCancellationToken_ReturnsNull()
    {
        var store = new DefaultSmsConfigStore();

        Assert.Null(await store.GetAsync());
    }

    /// <summary>
    /// 默认实现挂在 ISmsConfigStore 抽象上，可直接被解析器消费
    /// </summary>
    [Fact]
    public void Type_ImplementsSmsConfigStoreAbstraction()
    {
        Assert.IsAssignableFrom<ISmsConfigStore>(new DefaultSmsConfigStore());
    }
}
