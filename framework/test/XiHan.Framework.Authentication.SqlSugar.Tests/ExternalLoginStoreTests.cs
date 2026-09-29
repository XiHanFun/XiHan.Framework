// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Authentication.SqlSugar.ExternalLogins;
using XiHan.Framework.Authentication.SqlSugar.Tests.Fakes;
using XiHan.Framework.Authentication.OAuth;
using XiHan.Framework.Authentication.SqlSugar.Entities;

namespace XiHan.Framework.Authentication.SqlSugar.Tests;

/// <summary>
/// 第三方登录存储测试
/// </summary>
public class ExternalLoginStoreTests
{
    /// <summary>
    /// 绑定后可按提供商账号查到用户
    /// </summary>
    [Fact]
    public async Task 绑定后可按提供商账号查到用户()
    {
        using var context = NewContext();

        await context.CreateExternalLoginStore().CreateAsync(1001, NewInfo("github", "gh-1"));

        Assert.Equal(1001L, await context.CreateExternalLoginStore().FindUserIdAsync("github", "gh-1"));
    }

    /// <summary>
    /// 按实体类型解析客户端
    /// </summary>
    [Fact]
    public async Task 按实体类型解析客户端()
    {
        using var context = NewContext();

        await context.CreateExternalLoginStore().FindUserIdAsync("github", "gh-1");

        Assert.Contains(typeof(SysAuthExternalLogin), context.Resolver.RequestedEntityTypes);
    }

    /// <summary>
    /// 未绑定或参数空白时返回空
    /// </summary>
    [Fact]
    public async Task 未绑定或参数空白时返回空()
    {
        using var context = NewContext();
        var store = context.CreateExternalLoginStore();

        Assert.Null(await store.FindUserIdAsync("github", "none"));
        Assert.Null(await store.FindUserIdAsync(" ", "gh-1"));
        Assert.Null(await store.FindUserIdAsync("github", " "));
    }

    /// <summary>
    /// 提供商名称不区分大小写
    /// </summary>
    [Fact]
    public async Task 提供商名称不区分大小写()
    {
        using var context = NewContext();

        await context.CreateExternalLoginStore().CreateAsync(1001, NewInfo("GitHub", "gh-1"));

        Assert.Equal(1001L, await context.CreateExternalLoginStore().FindUserIdAsync("github", "gh-1"));
        var row = Assert.Single(context.Client.Queryable<SysAuthExternalLogin>().ToList());
        Assert.Equal("github", row.Provider);
    }

    /// <summary>
    /// 提供商用户标识区分大小写
    /// </summary>
    [Fact]
    public async Task 提供商用户标识区分大小写()
    {
        using var context = NewContext();

        await context.CreateExternalLoginStore().CreateAsync(1001, NewInfo("weixin", "OpenId-AbC"));

        Assert.Null(await context.CreateExternalLoginStore().FindUserIdAsync("weixin", "openid-abc"));
        Assert.Equal(1001L, await context.CreateExternalLoginStore().FindUserIdAsync("weixin", "OpenId-AbC"));
    }

    /// <summary>
    /// 未指定租户时使用当前租户
    /// </summary>
    [Fact]
    public async Task 未指定租户时使用当前租户()
    {
        using var context = NewContext();
        context.Tenant.Id = 5;
        await context.CreateExternalLoginStore().CreateAsync(1001, NewInfo("github", "gh-1"));

        context.Tenant.Id = null;
        var inPlatform = await context.CreateExternalLoginStore().FindUserIdAsync("github", "gh-1");
        var explicitTenant = await context.CreateExternalLoginStore().FindUserIdAsync("github", "gh-1", 5);
        context.Tenant.Id = 5;
        var inTenant = await context.CreateExternalLoginStore().FindUserIdAsync("github", "gh-1");

        Assert.Null(inPlatform);
        Assert.Equal(1001L, explicitTenant);
        Assert.Equal(1001L, inTenant);
        var row = Assert.Single(context.Client.Queryable<SysAuthExternalLogin>().ToList());
        Assert.Equal(5L, row.TenantId);
    }

    /// <summary>
    /// 显式租户优先于当前租户
    /// </summary>
    [Fact]
    public async Task 显式租户优先于当前租户()
    {
        using var context = NewContext();
        context.Tenant.Id = 5;

        await context.CreateExternalLoginStore().CreateAsync(1001, NewInfo("github", "gh-1"), 7);

        Assert.Null(await context.CreateExternalLoginStore().FindUserIdAsync("github", "gh-1"));
        Assert.Equal(1001L, await context.CreateExternalLoginStore().FindUserIdAsync("github", "gh-1", 7));
    }

    /// <summary>
    /// 同一用户重复绑定是幂等的
    /// </summary>
    [Fact]
    public async Task 同一用户重复绑定是幂等的()
    {
        using var context = NewContext();

        await context.CreateExternalLoginStore().CreateAsync(1001, NewInfo("github", "gh-1"));
        await context.CreateExternalLoginStore().CreateAsync(1001, NewInfo("github", "gh-1"));

        Assert.Equal(1, await context.Client.Queryable<SysAuthExternalLogin>().CountAsync());
    }

    /// <summary>
    /// 已绑定到其他用户时拒绝改绑
    /// </summary>
    [Fact]
    public async Task 已绑定到其他用户时拒绝改绑()
    {
        using var context = NewContext();
        await context.CreateExternalLoginStore().CreateAsync(1001, NewInfo("github", "gh-1"));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => context.CreateExternalLoginStore().CreateAsync(2002, NewInfo("github", "gh-1")));

        Assert.Equal(1001L, await context.CreateExternalLoginStore().FindUserIdAsync("github", "gh-1"));
    }

    /// <summary>
    /// 移除只删除该用户在该提供商下的绑定
    /// </summary>
    [Fact]
    public async Task 移除只删除该用户在该提供商下的绑定()
    {
        using var context = NewContext();
        var store = context.CreateExternalLoginStore();
        await store.CreateAsync(1001, NewInfo("github", "gh-1"));
        await store.CreateAsync(1001, NewInfo("gitee", "ge-1"));
        await store.CreateAsync(2002, NewInfo("github", "gh-2"));

        await context.CreateExternalLoginStore().RemoveAsync(1001, "GitHub");

        var reader = context.CreateExternalLoginStore();
        Assert.Null(await reader.FindUserIdAsync("github", "gh-1"));
        Assert.Equal(1001L, await reader.FindUserIdAsync("gitee", "ge-1"));
        Assert.Equal(2002L, await reader.FindUserIdAsync("github", "gh-2"));
    }

    /// <summary>
    /// 参数校验
    /// </summary>
    [Fact]
    public async Task 参数校验()
    {
        using var context = NewContext();
        var store = context.CreateExternalLoginStore();

        await Assert.ThrowsAsync<ArgumentNullException>(() => store.CreateAsync(1001, null!));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => store.CreateAsync(0, NewInfo("github", "gh-1")));
        await Assert.ThrowsAsync<ArgumentException>(() => store.CreateAsync(1001, NewInfo(" ", "gh-1")));
        await Assert.ThrowsAsync<ArgumentException>(() => store.CreateAsync(1001, NewInfo("github", " ")));
        await Assert.ThrowsAsync<ArgumentException>(() => store.RemoveAsync(1001, " "));
    }

    /// <summary>
    /// 展示信息超长时截断
    /// </summary>
    [Fact]
    public async Task 展示信息超长时截断()
    {
        using var context = NewContext();
        var info = NewInfo("github", "gh-1");
        info.DisplayName = new string('名', 300);
        info.AvatarUrl = $"https://example.com/{new string('a', 3000)}";

        await context.CreateExternalLoginStore().CreateAsync(1001, info);

        var row = Assert.Single(context.Client.Queryable<SysAuthExternalLogin>().ToList());
        Assert.Equal(256, row.DisplayName?.Length);
        Assert.Equal(2048, row.AvatarUrl?.Length);
        Assert.Equal("gh-1", row.ProviderKey);
    }

    /// <summary>
    /// 插入撞到唯一索引且已绑定其他用户时抛出契约异常
    /// </summary>
    [Fact]
    public async Task 插入撞到唯一索引且已绑定其他用户时抛出契约异常()
    {
        using var context = NewContext();
        var inserted = false;
        context.Client.Aop.OnLogExecuting = (sql, _) =>
        {
            if (inserted || !sql.TrimStart().StartsWith("INSERT", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            inserted = true;
            using var other = context.CreateClient(autoClose: true);
            var rival = new SqlSugarExternalLoginStore(new StubClientResolver(other), context.Tenant, context.IdGenerator, context.Clock);
            rival.CreateAsync(2002, NewInfo("github", "gh-1")).GetAwaiter().GetResult();
        };

        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => context.CreateExternalLoginStore().CreateAsync(1001, NewInfo("github", "gh-1")));

        Assert.Contains("已绑定到其他用户", error.Message);
        Assert.True(inserted);
    }

    /// <summary>
    /// 插入失败后重查也失败时，抛出同时包含插入异常与重查异常的聚合异常
    /// </summary>
    [Fact]
    public async Task 插入失败后重查也失败时抛出包含两个异常的聚合异常()
    {
        using var context = NewContext();
        var insertFailed = false;
        context.Client.Aop.OnLogExecuting = (sql, _) =>
        {
            if (sql.TrimStart().StartsWith("INSERT", StringComparison.OrdinalIgnoreCase))
            {
                insertFailed = true;
                throw new InvalidOperationException("insert-boom");
            }

            if (insertFailed && sql.TrimStart().StartsWith("SELECT", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("requery-boom");
            }
        };

        var exception = await Assert.ThrowsAsync<AggregateException>(() => context.CreateExternalLoginStore().CreateAsync(1001, NewInfo("github", "gh-1")));

        var messages = Flatten(exception).Select(item => item.Message).ToList();
        Assert.Contains(messages, message => message.Contains("insert-boom", StringComparison.Ordinal));
        Assert.Contains(messages, message => message.Contains("requery-boom", StringComparison.Ordinal));
    }

    private static IEnumerable<Exception> Flatten(Exception exception)
    {
        yield return exception;

        if (exception is AggregateException aggregate)
        {
            foreach (var inner in aggregate.InnerExceptions.SelectMany(Flatten))
            {
                yield return inner;
            }
        }
        else if (exception.InnerException is not null)
        {
            foreach (var inner in Flatten(exception.InnerException))
            {
                yield return inner;
            }
        }
    }

    private static AuthenticationTestContext NewContext()
    {
        return new AuthenticationTestContext(typeof(SysAuthExternalLogin));
    }

    private static ExternalLoginInfo NewInfo(string provider, string providerKey)
    {
        return new ExternalLoginInfo
        {
            Provider = provider,
            ProviderKey = providerKey,
            DisplayName = "Alice",
            Email = "alice@example.com"
        };
    }
}
