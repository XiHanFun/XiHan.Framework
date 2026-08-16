// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Options;
using XiHan.Framework.Security.Password;
using XiHan.Framework.Security.Services;

namespace XiHan.Framework.Security.Tests.Services;

/// <summary>
/// 密码历史复用检查的测试
/// </summary>
/// <remarks>
/// 通过 <see cref="DefaultPasswordHistoryStore"/> 记录历史哈希后，验证新密码与最近 N 次历史是否重复。
/// </remarks>
public class PasswordReuseTests
{
    /// <summary>
    /// 新密码与已记录的历史密码一致时应判定为重复
    /// </summary>
    [Fact]
    public async Task IsPasswordReused_RecordedPassword_ReturnsTrue()
    {
        const long userId = 1_000_001;
        const string password = "Secret@123";
        var hasher = CreateHasher();
        var service = CreateService(hasher);

        DefaultPasswordHistoryStore.RecordPassword(userId, hasher.HashPassword(password), maxHistoryCount: 10);

        var reused = await service.IsPasswordReusedAsync(password, userId, 5, TestContext.Current.CancellationToken);

        Assert.True(reused);
    }

    /// <summary>
    /// 新密码与历史密码不同时应判定为非重复
    /// </summary>
    [Fact]
    public async Task IsPasswordReused_UnrelatedPassword_ReturnsFalse()
    {
        const long userId = 1_000_002;
        var hasher = CreateHasher();
        var service = CreateService(hasher);

        DefaultPasswordHistoryStore.RecordPassword(userId, hasher.HashPassword("Old@123"), maxHistoryCount: 10);

        var reused = await service.IsPasswordReusedAsync("Other@456", userId, 5, TestContext.Current.CancellationToken);

        Assert.False(reused);
    }

    /// <summary>
    /// 仅最近 N 次历史内的密码会被判定为重复
    /// </summary>
    [Fact]
    public async Task IsPasswordReused_RespectsHistoryWindow()
    {
        const long userId = 1_000_003;
        var hasher = CreateHasher();
        var service = CreateService(hasher);

        DefaultPasswordHistoryStore.RecordPassword(userId, hasher.HashPassword("First@1"), maxHistoryCount: 10);
        DefaultPasswordHistoryStore.RecordPassword(userId, hasher.HashPassword("Second@2"), maxHistoryCount: 10);
        DefaultPasswordHistoryStore.RecordPassword(userId, hasher.HashPassword("Third@3"), maxHistoryCount: 10);

        Assert.False(await service.IsPasswordReusedAsync("First@1", userId, 2, TestContext.Current.CancellationToken));
        Assert.True(await service.IsPasswordReusedAsync("Second@2", userId, 2, TestContext.Current.CancellationToken));
        Assert.True(await service.IsPasswordReusedAsync("Third@3", userId, 2, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// 空密码或非正历史数量时应判定为非重复
    /// </summary>
    /// <param name="password">新密码明文</param>
    /// <param name="historyCount">历史记录数</param>
    [Theory]
    [InlineData("", 5)]
    [InlineData("Whatever@123", 0)]
    [InlineData("Whatever@123", -1)]
    public async Task IsPasswordReused_EmptyPasswordOrNonPositiveCount_ReturnsFalse(string password, int historyCount)
    {
        const long userId = 1_000_004;
        var hasher = CreateHasher();
        var service = CreateService(hasher);

        var reused = await service.IsPasswordReusedAsync(password, userId, historyCount, TestContext.Current.CancellationToken);

        Assert.False(reused);
    }

    /// <summary>
    /// 传入已取消的令牌应抛出取消异常
    /// </summary>
    [Fact]
    public async Task IsPasswordReused_CancelledToken_Throws()
    {
        const long userId = 1_000_005;
        var hasher = CreateHasher();
        var service = CreateService(hasher);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => service.IsPasswordReusedAsync("Secret@123", userId, 5, cts.Token));
    }

    /// <summary>
    /// 创建密码哈希服务
    /// </summary>
    /// <returns>密码哈希服务实例</returns>
    private static PasswordHasher CreateHasher()
    {
        return new PasswordHasher(Options.Create(new PasswordHasherOptions { Iterations = 1000 }));
    }

    /// <summary>
    /// 创建密码策略服务
    /// </summary>
    /// <param name="hasher">密码哈希服务</param>
    /// <returns>密码策略服务实例</returns>
    private static PasswordPolicyService CreateService(PasswordHasher hasher)
    {
        return new PasswordPolicyService(
            Options.Create(new PasswordPolicyOptions()),
            hasher,
            new DefaultPasswordHistoryStore());
    }
}
