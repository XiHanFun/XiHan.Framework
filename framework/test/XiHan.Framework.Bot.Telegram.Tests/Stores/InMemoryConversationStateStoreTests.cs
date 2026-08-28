// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Bot.Telegram.Abstractions;
using XiHan.Framework.Bot.Telegram.Stores;

namespace XiHan.Framework.Bot.Telegram.Tests.Stores;

/// <summary>
/// <see cref="InMemoryConversationStateStore"/> 进程内会话状态存储测试
/// </summary>
/// <remarks>
/// 状态键是「机器人 + 会话 + 用户」三元组：同一个群里两个用户各自的多步流程不能互相踩，
/// 同一个用户在两个机器人里的流程也不能串。过期条目必须在读取时被清掉并按无状态返回，
/// 否则用户会被永久困在一个早就该失效的步骤里。
/// </remarks>
public class InMemoryConversationStateStoreTests
{
    /// <summary>
    /// 没有设置过状态时读到 null
    /// </summary>
    [Fact]
    public async Task GetAsync_WhenNoState_ReturnsNull()
    {
        var store = new InMemoryConversationStateStore();

        Assert.Null(await store.GetAsync("main-bot", 100, 200, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// 设置后可原样读回同一个状态对象
    /// </summary>
    [Fact]
    public async Task SetAsync_ThenGetAsync_ReturnsSameState()
    {
        var store = new InMemoryConversationStateStore();
        var state = new ConversationState { Step = "awaiting_amount", Payload = """{"orderId":"A-1"}""" };

        await store.SetAsync("main-bot", 100, 200, state, TimeSpan.FromMinutes(5), TestContext.Current.CancellationToken);

        var loaded = await store.GetAsync("main-bot", 100, 200, TestContext.Current.CancellationToken);

        Assert.Same(state, loaded);
    }

    /// <summary>
    /// 状态为空时抛参数空异常
    /// </summary>
    [Fact]
    public async Task SetAsync_WhenStateNull_Throws()
    {
        var store = new InMemoryConversationStateStore();

        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await store.SetAsync("main-bot", 100, 200, null!, TimeSpan.FromMinutes(5), TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// 再次设置覆盖已有状态
    /// </summary>
    [Fact]
    public async Task SetAsync_OverwritesExistingState()
    {
        var store = new InMemoryConversationStateStore();
        await store.SetAsync("main-bot", 100, 200, new ConversationState { Step = "step-1" }, TimeSpan.FromMinutes(5), TestContext.Current.CancellationToken);

        var second = new ConversationState { Step = "step-2" };
        await store.SetAsync("main-bot", 100, 200, second, TimeSpan.FromMinutes(5), TestContext.Current.CancellationToken);

        var loaded = await store.GetAsync("main-bot", 100, 200, TestContext.Current.CancellationToken);

        Assert.Same(second, loaded);
        Assert.Equal("step-2", loaded!.Step);
    }

    /// <summary>
    /// 清除后读到 null
    /// </summary>
    [Fact]
    public async Task RemoveAsync_ClearsState()
    {
        var store = new InMemoryConversationStateStore();
        await store.SetAsync("main-bot", 100, 200, new ConversationState { Step = "step-1" }, TimeSpan.FromMinutes(5), TestContext.Current.CancellationToken);

        await store.RemoveAsync("main-bot", 100, 200, TestContext.Current.CancellationToken);

        Assert.Null(await store.GetAsync("main-bot", 100, 200, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// 清除不存在的状态是空操作，不抛异常
    /// </summary>
    [Fact]
    public async Task RemoveAsync_WhenNoState_IsNoOp()
    {
        var store = new InMemoryConversationStateStore();

        await store.RemoveAsync("main-bot", 100, 200, TestContext.Current.CancellationToken);

        Assert.Null(await store.GetAsync("main-bot", 100, 200, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// 不同机器人的同一会话与用户互不影响
    /// </summary>
    [Fact]
    public async Task States_AreIsolatedByBotName()
    {
        var store = new InMemoryConversationStateStore();
        await store.SetAsync("bot-a", 100, 200, new ConversationState { Step = "a" }, TimeSpan.FromMinutes(5), TestContext.Current.CancellationToken);

        Assert.Null(await store.GetAsync("bot-b", 100, 200, TestContext.Current.CancellationToken));
        Assert.Equal("a", (await store.GetAsync("bot-a", 100, 200, TestContext.Current.CancellationToken))!.Step);
    }

    /// <summary>
    /// 同一会话内不同用户的状态互不影响（群聊多人同时走流程）
    /// </summary>
    [Fact]
    public async Task States_AreIsolatedByUserId()
    {
        var store = new InMemoryConversationStateStore();
        await store.SetAsync("main-bot", 100, 200, new ConversationState { Step = "user-200" }, TimeSpan.FromMinutes(5), TestContext.Current.CancellationToken);
        await store.SetAsync("main-bot", 100, 300, new ConversationState { Step = "user-300" }, TimeSpan.FromMinutes(5), TestContext.Current.CancellationToken);

        Assert.Equal("user-200", (await store.GetAsync("main-bot", 100, 200, TestContext.Current.CancellationToken))!.Step);
        Assert.Equal("user-300", (await store.GetAsync("main-bot", 100, 300, TestContext.Current.CancellationToken))!.Step);
    }

    /// <summary>
    /// 同一用户在不同会话中的状态互不影响
    /// </summary>
    [Fact]
    public async Task States_AreIsolatedByChatId()
    {
        var store = new InMemoryConversationStateStore();
        await store.SetAsync("main-bot", 100, 200, new ConversationState { Step = "chat-100" }, TimeSpan.FromMinutes(5), TestContext.Current.CancellationToken);
        await store.SetAsync("main-bot", 101, 200, new ConversationState { Step = "chat-101" }, TimeSpan.FromMinutes(5), TestContext.Current.CancellationToken);

        Assert.Equal("chat-100", (await store.GetAsync("main-bot", 100, 200, TestContext.Current.CancellationToken))!.Step);
        Assert.Equal("chat-101", (await store.GetAsync("main-bot", 101, 200, TestContext.Current.CancellationToken))!.Step);
    }

    /// <summary>
    /// 存活时长非正数时取默认 10 分钟，状态立刻可读（而不是设置完就过期）
    /// </summary>
    /// <param name="ttlSeconds">存活秒数</param>
    [Theory]
    [InlineData(0)]
    [InlineData(-60)]
    public async Task SetAsync_WhenTtlNotPositive_UsesDefaultTenMinutes(int ttlSeconds)
    {
        var store = new InMemoryConversationStateStore();

        await store.SetAsync("main-bot", 100, 200, new ConversationState { Step = "step-1" }, TimeSpan.FromSeconds(ttlSeconds), TestContext.Current.CancellationToken);

        Assert.NotNull(await store.GetAsync("main-bot", 100, 200, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// 状态过期后读取返回 null，并且过期条目被顺手清掉
    /// </summary>
    [Fact]
    public async Task GetAsync_WhenExpired_ReturnsNullAndEvictsEntry()
    {
        var store = new InMemoryConversationStateStore();
        await store.SetAsync("main-bot", 100, 200, new ConversationState { Step = "step-1" }, TimeSpan.FromMilliseconds(1), TestContext.Current.CancellationToken);

        await Task.Delay(TimeSpan.FromMilliseconds(120), TestContext.Current.CancellationToken);

        Assert.Null(await store.GetAsync("main-bot", 100, 200, TestContext.Current.CancellationToken));
        Assert.Null(await store.GetAsync("main-bot", 100, 200, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// 过期后重新设置可以恢复使用
    /// </summary>
    [Fact]
    public async Task SetAsync_AfterExpiration_RestoresState()
    {
        var store = new InMemoryConversationStateStore();
        await store.SetAsync("main-bot", 100, 200, new ConversationState { Step = "old" }, TimeSpan.FromMilliseconds(1), TestContext.Current.CancellationToken);
        await Task.Delay(TimeSpan.FromMilliseconds(120), TestContext.Current.CancellationToken);
        Assert.Null(await store.GetAsync("main-bot", 100, 200, TestContext.Current.CancellationToken));

        await store.SetAsync("main-bot", 100, 200, new ConversationState { Step = "new" }, TimeSpan.FromMinutes(5), TestContext.Current.CancellationToken);

        Assert.Equal("new", (await store.GetAsync("main-bot", 100, 200, TestContext.Current.CancellationToken))!.Step);
    }

    /// <summary>
    /// 默认实现挂在 IConversationStateStore 抽象上，可被分布式实现整体替换
    /// </summary>
    [Fact]
    public void Type_ImplementsConversationStateStoreAbstraction()
    {
        Assert.IsAssignableFrom<IConversationStateStore>(new InMemoryConversationStateStore());
    }

    /// <summary>
    /// 不传取消令牌时按默认令牌工作
    /// </summary>
    [Fact]
    public async Task Api_WithoutCancellationToken_Works()
    {
        var store = new InMemoryConversationStateStore();

        await store.SetAsync("main-bot", 100, 200, new ConversationState { Step = "step-1" }, TimeSpan.FromMinutes(5));
        Assert.NotNull(await store.GetAsync("main-bot", 100, 200));
        await store.RemoveAsync("main-bot", 100, 200);
        Assert.Null(await store.GetAsync("main-bot", 100, 200));
    }
}
