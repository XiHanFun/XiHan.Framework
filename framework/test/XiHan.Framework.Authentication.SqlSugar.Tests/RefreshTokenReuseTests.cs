// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Authentication.SqlSugar.Entities;
using XiHan.Framework.Authentication.SqlSugar.Options;
using XiHan.Framework.Authentication.SqlSugar.RefreshTokens;
using XiHan.Framework.Authentication.SqlSugar.Tests.Fakes;

namespace XiHan.Framework.Authentication.SqlSugar.Tests;

/// <summary>
/// 刷新令牌重用检测与过期清理测试
/// </summary>
public class RefreshTokenReuseTests
{
    private const string Subject = "1001";

    /// <summary>
    /// 已撤销令牌再次出现时撤销同一主体的全部令牌
    /// </summary>
    [Fact]
    public void 已撤销令牌再次出现时撤销同一主体的全部令牌()
    {
        using var context = NewContext();
        var store = context.CreateRefreshTokenStore();
        store.Save("token-1", Subject, InOneDay(context));
        store.Save("token-2", Subject, InOneDay(context));
        store.Remove("token-1");

        Assert.False(store.Validate("token-1", Subject));

        Assert.False(store.Validate("token-2", Subject));
    }

    /// <summary>
    /// 校验未知令牌不撤销任何令牌
    /// </summary>
    [Fact]
    public void 校验未知令牌不撤销任何令牌()
    {
        using var context = NewContext();
        var store = context.CreateRefreshTokenStore();
        store.Save("token-1", Subject, InOneDay(context));

        Assert.False(store.Validate("unknown", Subject));

        Assert.True(store.Validate("token-1", Subject));
    }

    /// <summary>
    /// 级联撤销不波及其他主体
    /// </summary>
    [Fact]
    public void 级联撤销不波及其他主体()
    {
        using var context = NewContext();
        var store = context.CreateRefreshTokenStore();
        store.Save("token-1", Subject, InOneDay(context));
        store.Save("other", "2002", InOneDay(context));
        store.Remove("token-1");

        store.Validate("token-1", Subject);

        Assert.True(store.Validate("other", "2002"));
    }

    /// <summary>
    /// 级联撤销不波及其他租户的同一主体
    /// </summary>
    [Fact]
    public void 级联撤销不波及其他租户的同一主体()
    {
        using var context = NewContext();
        var store = context.CreateRefreshTokenStore();
        context.Tenant.Id = 7;
        store.Save("tenant-token", Subject, InOneDay(context));
        context.Tenant.Id = null;
        store.Save("token-1", Subject, InOneDay(context));
        store.Remove("token-1");

        store.Validate("token-1", Subject);

        Assert.True(store.Validate("tenant-token", Subject));
    }

    /// <summary>
    /// 宽限期内重复使用只拒绝不级联
    /// </summary>
    [Fact]
    public void 宽限期内重复使用只拒绝不级联()
    {
        using var context = NewContext();
        var store = context.CreateRefreshTokenStore(new XiHanAuthenticationSqlSugarOptions
        {
            RefreshTokenReuseGracePeriod = TimeSpan.FromMinutes(1)
        });
        store.Save("token-1", Subject, InOneDay(context));
        store.Save("token-2", Subject, InOneDay(context));
        store.Remove("token-1");
        context.Clock.Advance(TimeSpan.FromSeconds(30));

        Assert.False(store.Validate("token-1", Subject));

        Assert.True(store.Validate("token-2", Subject));
    }

    /// <summary>
    /// 超过宽限期后重复使用触发级联
    /// </summary>
    [Fact]
    public void 超过宽限期后重复使用触发级联()
    {
        using var context = NewContext();
        var store = context.CreateRefreshTokenStore(new XiHanAuthenticationSqlSugarOptions
        {
            RefreshTokenReuseGracePeriod = TimeSpan.FromMinutes(1)
        });
        store.Save("token-1", Subject, InOneDay(context));
        store.Save("token-2", Subject, InOneDay(context));
        store.Remove("token-1");
        context.Clock.Advance(TimeSpan.FromMinutes(2));

        Assert.False(store.Validate("token-1", Subject));

        Assert.False(store.Validate("token-2", Subject));
    }

    /// <summary>
    /// 关闭重用检测后只拒绝不级联
    /// </summary>
    [Fact]
    public void 关闭重用检测后只拒绝不级联()
    {
        using var context = NewContext();
        var store = context.CreateRefreshTokenStore(new XiHanAuthenticationSqlSugarOptions
        {
            RefreshTokenReuseDetection = false
        });
        store.Save("token-1", Subject, InOneDay(context));
        store.Save("token-2", Subject, InOneDay(context));
        store.Remove("token-1");

        Assert.False(store.Validate("token-1", Subject));

        Assert.True(store.Validate("token-2", Subject));
    }

    /// <summary>
    /// 主体为空的令牌重复使用只拒绝
    /// </summary>
    [Fact]
    public void 主体为空的令牌重复使用只拒绝()
    {
        using var context = NewContext();
        var store = context.CreateRefreshTokenStore();
        store.Save("anonymous", null, InOneDay(context));
        store.Save("token-2", Subject, InOneDay(context));
        store.Remove("anonymous");

        Assert.False(store.Validate("anonymous"));

        Assert.True(store.Validate("token-2", Subject));
    }

    /// <summary>
    /// 级联撤销不随调用方事务回滚
    /// </summary>
    [Fact]
    public void 级联撤销不随调用方事务回滚()
    {
        using var context = NewContext();
        context.Client.Ado.ExecuteCommand("PRAGMA journal_mode=WAL;");
        var store = context.CreateRefreshTokenStore();
        store.Save("token-1", Subject, InOneDay(context));
        store.Save("token-2", Subject, InOneDay(context));
        store.Remove("token-1");

        using var transactional = context.CreateClient(autoClose: false);
        var transactionalStore = context.CreateRefreshTokenStore(resolver: new StubClientResolver(transactional));

        transactional.Ado.ExecuteCommand("BEGIN DEFERRED;");
        var reused = transactionalStore.Validate("token-1", Subject);
        transactional.Ado.ExecuteCommand("ROLLBACK;");

        Assert.False(reused);
        Assert.False(store.Validate("token-2", Subject));
    }

    /// <summary>
    /// 保存达到清理频率时删除已过期记录
    /// </summary>
    [Fact]
    public void 保存达到清理频率时删除已过期记录()
    {
        using var context = NewContext();
        var store = context.CreateRefreshTokenStore(new XiHanAuthenticationSqlSugarOptions
        {
            RefreshTokenCleanupFrequency = 1
        });
        store.Save("expired", Subject, context.Clock.GetUtcNow().UtcDateTime.AddHours(1));
        context.Clock.Advance(TimeSpan.FromHours(2));

        store.Save("fresh", Subject, InOneDay(context));

        Assert.Equal(0, CountByToken(context, "expired"));
        Assert.Equal(1, CountByToken(context, "fresh"));
    }

    /// <summary>
    /// 清理保留已撤销但未过期的记录
    /// </summary>
    [Fact]
    public void 清理保留已撤销但未过期的记录()
    {
        using var context = NewContext();
        var store = context.CreateRefreshTokenStore(new XiHanAuthenticationSqlSugarOptions
        {
            RefreshTokenCleanupFrequency = 1
        });
        store.Save("revoked", Subject, InOneDay(context));
        store.Remove("revoked");

        store.Save("fresh", Subject, InOneDay(context));

        Assert.Equal(1, CountByToken(context, "revoked"));
    }

    /// <summary>
    /// 清理频率为零时不清理
    /// </summary>
    [Fact]
    public void 清理频率为零时不清理()
    {
        using var context = NewContext();
        var store = context.CreateRefreshTokenStore(new XiHanAuthenticationSqlSugarOptions
        {
            RefreshTokenCleanupFrequency = 0
        });
        store.Save("expired", Subject, context.Clock.GetUtcNow().UtcDateTime.AddHours(1));
        context.Clock.Advance(TimeSpan.FromHours(2));

        store.Save("fresh", Subject, InOneDay(context));

        Assert.Equal(1, CountByToken(context, "expired"));
    }

    private static AuthenticationTestContext NewContext()
    {
        return new AuthenticationTestContext(typeof(SysAuthRefreshToken));
    }

    private static DateTime InOneDay(AuthenticationTestContext context)
    {
        return context.Clock.GetUtcNow().UtcDateTime.AddDays(1);
    }

    private static int CountByToken(AuthenticationTestContext context, string token)
    {
        var tokenHash = RefreshTokenHasher.Hash(token);

        return context.Client.Queryable<SysAuthRefreshToken>()
            .Where(item => item.TokenHash == tokenHash)
            .Count();
    }
}
