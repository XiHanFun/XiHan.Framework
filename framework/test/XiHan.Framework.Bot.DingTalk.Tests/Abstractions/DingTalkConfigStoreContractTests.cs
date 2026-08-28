// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Bot.DingTalk.Abstractions;
using XiHan.Framework.Bot.DingTalk.Options;
using XiHan.Framework.Bot.DingTalk.Tests.Fakes;

namespace XiHan.Framework.Bot.DingTalk.Tests.Abstractions;

/// <summary>
/// 钉钉配置存储接口契约测试
/// </summary>
/// <remarks>
/// 这个接口是留给应用层覆盖的扩展点（典型场景是把配置搬到数据库），
/// 所以它的形状本身就是对外契约：只有一个异步读取方法、取消令牌可省略、返回值允许为 null 表示未配置。
/// 形状一旦变动，所有下游实现会同时编译失败，因此在这里显式钉住。
/// </remarks>
public class DingTalkConfigStoreContractTests
{
    /// <summary>
    /// 接口只暴露一个可选取消令牌的异步读取方法
    /// </summary>
    [Fact]
    public void Interface_ExposesSingleAsyncGetMethod()
    {
        var methods = typeof(IDingTalkConfigStore).GetMethods();

        var method = Assert.Single(methods);

        Assert.Equal(nameof(IDingTalkConfigStore.GetAsync), method.Name);
        Assert.Equal(typeof(Task<DingTalkOptions>), method.ReturnType);

        var parameter = Assert.Single(method.GetParameters());

        Assert.Equal(typeof(CancellationToken), parameter.ParameterType);
        Assert.True(parameter.HasDefaultValue);
    }

    /// <summary>
    /// 省略取消令牌调用时使用默认令牌，且实现可以返回 null 表示未配置
    /// </summary>
    [Fact]
    public async Task GetAsync_WithoutCancellationToken_UsesNoneAndAllowsNullResult()
    {
        var store = new FakeDingTalkConfigStore(null);

        var options = await ((IDingTalkConfigStore)store).GetAsync();

        Assert.Null(options);
        Assert.Equal(1, store.GetCallCount);
        Assert.Equal(CancellationToken.None, store.LastCancellationToken);
    }
}
