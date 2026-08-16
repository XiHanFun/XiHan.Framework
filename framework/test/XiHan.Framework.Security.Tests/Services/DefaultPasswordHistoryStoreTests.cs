// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Security.Services;

namespace XiHan.Framework.Security.Tests.Services;

/// <summary>
/// 默认密码历史记录存储的测试
/// </summary>
/// <remarks>
/// 覆盖历史记录上限裁剪、未知用户返回空列表以及取消令牌传播。
/// </remarks>
public class DefaultPasswordHistoryStoreTests
{
    /// <summary>
    /// 记录超过上限的历史后应仅保留最近若干条
    /// </summary>
    [Fact]
    public async Task RecordPassword_TrimsToMaxHistoryCount()
    {
        const long userId = 2_000_001;
        var store = new DefaultPasswordHistoryStore();

        DefaultPasswordHistoryStore.RecordPassword(userId, "hash-1", maxHistoryCount: 3);
        DefaultPasswordHistoryStore.RecordPassword(userId, "hash-2", maxHistoryCount: 3);
        DefaultPasswordHistoryStore.RecordPassword(userId, "hash-3", maxHistoryCount: 3);
        DefaultPasswordHistoryStore.RecordPassword(userId, "hash-4", maxHistoryCount: 3);
        DefaultPasswordHistoryStore.RecordPassword(userId, "hash-5", maxHistoryCount: 3);

        var recent = await store.GetRecentPasswordHashesAsync(userId, 10, TestContext.Current.CancellationToken);

        Assert.Equal(new[] { "hash-3", "hash-4", "hash-5" }, recent);
    }

    /// <summary>
    /// 查询从未记录过历史的用户应返回空列表
    /// </summary>
    [Fact]
    public async Task GetRecentPasswordHashes_UnknownUser_ReturnsEmpty()
    {
        const long userId = 2_000_002;
        var store = new DefaultPasswordHistoryStore();

        var recent = await store.GetRecentPasswordHashesAsync(userId, 5, TestContext.Current.CancellationToken);

        Assert.Empty(recent);
    }

    /// <summary>
    /// 传入已取消的令牌应抛出取消异常
    /// </summary>
    [Fact]
    public async Task GetRecentPasswordHashes_CancelledToken_Throws()
    {
        const long userId = 2_000_003;
        var store = new DefaultPasswordHistoryStore();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => store.GetRecentPasswordHashesAsync(userId, 5, cts.Token));
    }
}
