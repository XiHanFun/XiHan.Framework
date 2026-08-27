// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Bot.Telegram.Abstractions;
using XiHan.Framework.Bot.Telegram.Stores;

namespace XiHan.Framework.Bot.Telegram.Tests.Stores;

/// <summary>
/// <see cref="NoOpTelegramMessageAuditStore"/> 空操作出站审计存储测试
/// </summary>
/// <remarks>
/// 默认审计实现必须绝对无害：审计失败不能反过来影响消息发送主流程，
/// 因此它连入参都不校验，传什么都直接返回完成。
/// </remarks>
public class NoOpTelegramMessageAuditStoreTests
{
    /// <summary>
    /// 追加记录直接返回完成
    /// </summary>
    [Fact]
    public async Task AppendAsync_CompletesImmediately()
    {
        var store = new NoOpTelegramMessageAuditStore();

        await store.AppendAsync(new TelegramMessageAuditRecord { BotName = "main-bot" }, TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// 记录为 null 也不抛异常
    /// </summary>
    [Fact]
    public async Task AppendAsync_WhenRecordNull_DoesNotThrow()
    {
        var store = new NoOpTelegramMessageAuditStore();

        await store.AppendAsync(null!, TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// 令牌已取消时同样不抛异常
    /// </summary>
    [Fact]
    public async Task AppendAsync_WhenTokenCanceled_DoesNotThrow()
    {
        var store = new NoOpTelegramMessageAuditStore();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await store.AppendAsync(new TelegramMessageAuditRecord(), cts.Token);
    }

    /// <summary>
    /// 不传取消令牌时同样工作
    /// </summary>
    [Fact]
    public async Task AppendAsync_WithoutCancellationToken_Works()
    {
        var store = new NoOpTelegramMessageAuditStore();

        await store.AppendAsync(new TelegramMessageAuditRecord());
    }

    /// <summary>
    /// 默认实现挂在 ITelegramMessageAuditStore 抽象上，可被数据库/队列实现整体替换
    /// </summary>
    [Fact]
    public void Type_ImplementsAuditStoreAbstraction()
    {
        Assert.IsAssignableFrom<ITelegramMessageAuditStore>(new NoOpTelegramMessageAuditStore());
    }
}
