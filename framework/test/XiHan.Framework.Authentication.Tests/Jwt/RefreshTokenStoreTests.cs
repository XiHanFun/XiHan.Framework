// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Reflection;
using XiHan.Framework.Authentication.Jwt;

namespace XiHan.Framework.Authentication.Tests.Jwt;

/// <summary>
/// 刷新令牌存储测试
/// </summary>
public class RefreshTokenStoreTests
{
    [Fact]
    public async Task TryConsume_ConcurrentCalls_OnlyOneConsumesToken()
    {
        var store = new DefaultRefreshTokenStore();
        store.Save("refresh", "user-1", DateTime.UtcNow.AddMinutes(1));

        var results = await Task.WhenAll(Enumerable.Range(0, 32)
            .Select(_ => Task.Run(() => store.TryConsume("refresh", "user-1"))));

        Assert.Single(results, result => result);
    }

    [Fact]
    public void TryConsume_WrongSubject_DoesNotConsumeToken()
    {
        var store = new DefaultRefreshTokenStore();
        store.Save("refresh", "user-1", DateTime.UtcNow.AddMinutes(1));

        Assert.False(store.TryConsume("refresh", "user-2"));
        Assert.True(store.TryConsume("refresh", "user-1"));
        Assert.False(store.TryConsume("refresh", "user-1"));
    }

    [Fact]
    public void TryConsume_ExpiredToken_ReturnsFalse()
    {
        var store = new DefaultRefreshTokenStore();
        store.Save("refresh", "user-1", DateTime.UtcNow.AddMinutes(-1));

        Assert.False(store.TryConsume("refresh", "user-1"));
    }

    /// <summary>
    /// 进程内存储会在后续写入时批量清理已过期且不再访问的令牌
    /// </summary>
    [Fact]
    public void DefaultStore_AfterWriteThreshold_RemovesExpiredEntries()
    {
        var store = new DefaultRefreshTokenStore();
        store.Save("expired", "user-1", DateTime.UtcNow.AddMilliseconds(20));
        Thread.Sleep(100);

        for (var index = 0; index < 255; index++)
        {
            store.Save($"active-{index}", "user-1", DateTime.UtcNow.AddDays(1));
        }

        Assert.False(store.Validate("expired", "user-1"));
        Assert.Equal(255, GetTokenCount(store));
    }

    private static int GetTokenCount(DefaultRefreshTokenStore store)
    {
        var field = typeof(DefaultRefreshTokenStore).GetField("_tokens", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var tokens = field.GetValue(store)!;
        return (int)tokens.GetType().GetProperty("Count")!.GetValue(tokens)!;
    }
}
