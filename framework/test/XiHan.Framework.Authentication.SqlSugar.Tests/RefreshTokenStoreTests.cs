// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Authentication.SqlSugar.Entities;
using XiHan.Framework.Authentication.SqlSugar.RefreshTokens;

namespace XiHan.Framework.Authentication.SqlSugar.Tests;

/// <summary>
/// 刷新令牌存储的保存、校验与移除测试
/// </summary>
public class RefreshTokenStoreTests
{
    private const string Subject = "1001";

    /// <summary>
    /// 保存后校验通过
    /// </summary>
    [Fact]
    public void 保存后校验通过()
    {
        using var context = NewContext();
        var store = context.CreateRefreshTokenStore();

        store.Save("token-1", Subject, InOneDay(context));

        Assert.True(store.Validate("token-1", Subject));
    }

    /// <summary>
    /// 数据库只保存令牌哈希
    /// </summary>
    [Fact]
    public void 数据库只保存令牌哈希()
    {
        using var context = NewContext();

        context.CreateRefreshTokenStore().Save("token-1", Subject, InOneDay(context));

        var row = Assert.Single(context.Client.Queryable<SysAuthRefreshToken>().ToList());
        Assert.Equal(RefreshTokenHasher.Hash("token-1"), row.TokenHash);
        Assert.Equal(64, row.TokenHash.Length);
        Assert.NotEqual("token-1", row.TokenHash);
        Assert.Equal(Subject, row.Subject);
        Assert.Null(row.RevokedTime);
    }

    /// <summary>
    /// 按实体类型解析客户端
    /// </summary>
    [Fact]
    public void 按实体类型解析客户端()
    {
        using var context = NewContext();

        context.CreateRefreshTokenStore().Save("token-1", Subject, InOneDay(context));

        Assert.Contains(typeof(SysAuthRefreshToken), context.Resolver.RequestedEntityTypes);
    }

    /// <summary>
    /// 记录签发时的租户
    /// </summary>
    [Fact]
    public void 记录签发时的租户()
    {
        using var context = NewContext();
        context.Tenant.Id = 5;

        context.CreateRefreshTokenStore().Save("token-1", Subject, InOneDay(context));

        var row = Assert.Single(context.Client.Queryable<SysAuthRefreshToken>().ToList());
        Assert.Equal(5L, row.TenantId);
    }

    /// <summary>
    /// 主体不符时校验失败
    /// </summary>
    [Fact]
    public void 主体不符时校验失败()
    {
        using var context = NewContext();
        var store = context.CreateRefreshTokenStore();
        store.Save("token-1", Subject, InOneDay(context));

        Assert.False(store.Validate("token-1", "2002"));
        Assert.True(store.Validate("token-1", Subject));
    }

    /// <summary>
    /// 未传主体时跳过绑定校验
    /// </summary>
    [Fact]
    public void 未传主体时跳过绑定校验()
    {
        using var context = NewContext();
        var store = context.CreateRefreshTokenStore();
        store.Save("token-1", Subject, InOneDay(context));

        Assert.True(store.Validate("token-1"));
        Assert.True(store.Validate("token-1", " "));
    }

    /// <summary>
    /// 未知令牌校验失败
    /// </summary>
    [Fact]
    public void 未知令牌校验失败()
    {
        using var context = NewContext();

        Assert.False(context.CreateRefreshTokenStore().Validate("unknown", Subject));
    }

    /// <summary>
    /// 空白令牌不保存也不通过校验
    /// </summary>
    [Fact]
    public void 空白令牌不保存也不通过校验()
    {
        using var context = NewContext();
        var store = context.CreateRefreshTokenStore();

        store.Save(" ", Subject, InOneDay(context));
        store.Remove(" ");

        Assert.False(store.Validate(" ", Subject));
        Assert.Equal(0, context.Client.Queryable<SysAuthRefreshToken>().Count());
    }

    /// <summary>
    /// 过期后校验失败
    /// </summary>
    [Fact]
    public void 过期后校验失败()
    {
        using var context = NewContext();
        var store = context.CreateRefreshTokenStore();
        store.Save("token-1", Subject, context.Clock.GetUtcNow().UtcDateTime.AddHours(1));

        context.Clock.Advance(TimeSpan.FromHours(2));

        Assert.False(store.Validate("token-1", Subject));
    }

    /// <summary>
    /// 过期时间已过的保存不写入
    /// </summary>
    [Fact]
    public void 过期时间已过的保存不写入()
    {
        using var context = NewContext();

        context.CreateRefreshTokenStore().Save("token-1", Subject, context.Clock.GetUtcNow().UtcDateTime.AddMinutes(-1));

        Assert.Equal(0, context.Client.Queryable<SysAuthRefreshToken>().Count());
    }

    /// <summary>
    /// 移除是标记而非删除
    /// </summary>
    [Fact]
    public void 移除是标记而非删除()
    {
        using var context = NewContext();
        var store = context.CreateRefreshTokenStore();
        store.Save("token-1", Subject, InOneDay(context));

        store.Remove("token-1");

        Assert.False(store.Validate("token-1", Subject));
        var row = Assert.Single(context.Client.Queryable<SysAuthRefreshToken>().ToList());
        Assert.NotNull(row.RevokedTime);
    }

    /// <summary>
    /// 重复移除同一令牌抛出
    /// </summary>
    [Fact]
    public void 重复移除同一令牌抛出()
    {
        using var context = NewContext();
        var store = context.CreateRefreshTokenStore();
        store.Save("token-1", Subject, InOneDay(context));
        store.Remove("token-1");

        Assert.Throws<InvalidOperationException>(() => store.Remove("token-1"));
    }

    /// <summary>
    /// 过期时间已过的保存遇到已撤销令牌不抛出
    /// </summary>
    [Fact]
    public void 过期时间已过的保存遇到已撤销令牌不抛出()
    {
        using var context = NewContext();
        var store = context.CreateRefreshTokenStore();
        store.Save("token-1", Subject, InOneDay(context));
        store.Remove("token-1");

        var error = Record.Exception(
            () => store.Save("token-1", Subject, context.Clock.GetUtcNow().UtcDateTime.AddMinutes(-1)));

        Assert.Null(error);
    }

    /// <summary>
    /// 移除未知令牌不抛出
    /// </summary>
    [Fact]
    public void 移除未知令牌不抛出()
    {
        using var context = NewContext();

        context.CreateRefreshTokenStore().Remove("unknown");

        Assert.Equal(0, context.Client.Queryable<SysAuthRefreshToken>().Count());
    }

    /// <summary>
    /// 条件更新前令牌被其他连接撤销时抛出
    /// </summary>
    [Fact]
    public void 条件更新前令牌被其他连接撤销时抛出()
    {
        using var context = NewContext();
        var store = context.CreateRefreshTokenStore();
        store.Save("token-1", Subject, InOneDay(context));
        var tokenHash = RefreshTokenHasher.Hash("token-1");
        var revoked = false;
        context.Client.Aop.OnLogExecuting = (sql, _) =>
        {
            if (revoked || !sql.TrimStart().StartsWith("UPDATE", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            revoked = true;
            using var other = context.CreateClient(autoClose: true);
            other.Updateable<SysAuthRefreshToken>()
                .SetColumns(item => new SysAuthRefreshToken { RevokedTime = DateTime.UtcNow })
                .Where(item => item.TokenHash == tokenHash)
                .ExecuteCommand();
        };

        Assert.Throws<InvalidOperationException>(() => store.Remove("token-1"));
        Assert.True(revoked);
    }

    /// <summary>
    /// 重复保存同一令牌抛出
    /// </summary>
    [Fact]
    public void 重复保存同一令牌抛出()
    {
        using var context = NewContext();
        var store = context.CreateRefreshTokenStore();
        store.Save("token-1", Subject, InOneDay(context));

        Assert.ThrowsAny<Exception>(() => store.Save("token-1", Subject, InOneDay(context)));
    }

    /// <summary>
    /// 本地时间的过期时间换算为 UTC
    /// </summary>
    [Fact]
    public void 本地时间的过期时间换算为UTC()
    {
        using var context = NewContext();
        var local = new DateTime(2099, 1, 1, 8, 0, 0, DateTimeKind.Local);

        context.CreateRefreshTokenStore().Save("token-1", Subject, local);

        var row = Assert.Single(context.Client.Queryable<SysAuthRefreshToken>().ToList());
        Assert.Equal(local.ToUniversalTime(), row.ExpiresAt);
    }

    private static AuthenticationTestContext NewContext()
    {
        return new AuthenticationTestContext(typeof(SysAuthRefreshToken));
    }

    private static DateTime InOneDay(AuthenticationTestContext context)
    {
        return context.Clock.GetUtcNow().UtcDateTime.AddDays(1);
    }
}
