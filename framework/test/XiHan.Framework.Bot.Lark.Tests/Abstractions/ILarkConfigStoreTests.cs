// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Bot.Lark.Abstractions;
using XiHan.Framework.Bot.Lark.Options;
using XiHan.Framework.Bot.Lark.Tests.Fakes;

namespace XiHan.Framework.Bot.Lark.Tests.Abstractions;

/// <summary>
/// 飞书配置存储接口契约测试
/// </summary>
/// <remarks>
/// 这个接口是留给应用层覆盖的扩展点（典型场景是把配置搬到数据库），所以它的形状本身就是对外契约：
/// 只有一个异步读取方法、取消令牌可省略、返回值允许为 null 表示未配置。
/// 形状一旦变动，所有下游实现会同时编译失败，因此在这里显式钉住。
/// </remarks>
public class ILarkConfigStoreTests
{
    /// <summary>
    /// 接口只暴露一个带可选取消令牌的异步读取方法
    /// </summary>
    [Fact]
    public void Interface_Always_ExposesSingleAsyncGetMethod()
    {
        var method = Assert.Single(typeof(ILarkConfigStore).GetMethods());

        Assert.Equal(nameof(ILarkConfigStore.GetAsync), method.Name);
        Assert.Equal(typeof(Task<LarkOptions>), method.ReturnType);

        var parameter = Assert.Single(method.GetParameters());

        Assert.Equal(typeof(CancellationToken), parameter.ParameterType);
        Assert.True(parameter.HasDefaultValue);
    }

    /// <summary>
    /// 省略取消令牌调用时使用 None，且实现可以返回 null 表示未配置
    /// </summary>
    [Fact]
    public async Task GetAsync_WithoutCancellationToken_UsesNoneAndAllowsNullResult()
    {
        var store = new FakeLarkConfigStore(null);

        var options = await ((ILarkConfigStore)store).GetAsync();

        Assert.Null(options);
        Assert.Equal(1, store.GetCallCount);
        Assert.Equal(CancellationToken.None, store.LastCancellationToken);
    }

    /// <summary>
    /// 实现返回的配置原样透出，不被接口层加工
    /// </summary>
    [Fact]
    public async Task GetAsync_WhenImplementationReturnsOptions_PassesThroughUnchanged()
    {
        var configured = new LarkOptions
        {
            AccessToken = "abc-token",
            Secret = "sign-secret"
        };
        var store = new FakeLarkConfigStore(configured);

        var options = await ((ILarkConfigStore)store).GetAsync(TestContext.Current.CancellationToken);

        Assert.Same(configured, options);
    }
}
