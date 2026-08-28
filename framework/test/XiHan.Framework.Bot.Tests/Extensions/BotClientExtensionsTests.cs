// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Bot.Extensions;
using XiHan.Framework.Bot.Tests.Fakes;

namespace XiHan.Framework.Bot.Tests.Extensions;

/// <summary>
/// <see cref="BotClientExtensions"/> 测试
/// </summary>
/// <remarks>
/// Alert() 必须每次返回全新的构建器：构建器持有可变的消息与渠道状态，
/// 一旦复用同一个实例，两处告警会互相污染标题与提及列表。
/// </remarks>
public class BotClientExtensionsTests
{
    /// <summary>
    /// 返回可用的告警构建器
    /// </summary>
    [Fact]
    public void Alert_ReturnsBuilder()
    {
        var client = new FakeBotClient();

        Assert.NotNull(client.Alert());
    }

    /// <summary>
    /// 每次调用都返回独立实例，状态互不影响
    /// </summary>
    [Fact]
    public async Task Alert_ReturnsIndependentBuilders()
    {
        var client = new FakeBotClient();

        var first = client.Alert();
        var second = client.Alert();

        Assert.NotSame(first, second);

        first.Title("第一条");
        await second.Title("第二条").Content("c").SendAsync(TestContext.Current.CancellationToken);

        Assert.Equal("第二条", client.LastMessage!.Title);
    }

    /// <summary>
    /// 构建器绑定到调用扩展方法的那个客户端
    /// </summary>
    [Fact]
    public async Task Alert_BindsToSourceClient()
    {
        var first = new FakeBotClient();
        var second = new FakeBotClient();

        await first.Alert().Content("c").SendAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1, first.SendCount);
        Assert.Equal(0, second.SendCount);
    }
}
