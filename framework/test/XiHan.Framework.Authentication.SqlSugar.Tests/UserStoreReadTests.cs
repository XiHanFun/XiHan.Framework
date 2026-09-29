// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Authentication.SqlSugar.Entities;
using XiHan.Framework.Authentication.Users;

namespace XiHan.Framework.Authentication.SqlSugar.Tests;

/// <summary>
/// 用户存储读取与添加测试
/// </summary>
public class UserStoreReadTests
{
    /// <summary>
    /// 按用户名查找不区分大小写
    /// </summary>
    [Fact]
    public async Task 按用户名查找不区分大小写()
    {
        using var context = new AuthenticationTestContext();
        await context.CreateUserStore().AddUserAsync(NewUser("Alice"));

        var found = await context.CreateUserStore().GetUserByUsernameAsync("ALICE");

        Assert.NotNull(found);
        Assert.Equal("Alice", found.Username);
    }

    /// <summary>
    /// 按实体类型解析客户端
    /// </summary>
    [Fact]
    public async Task 按实体类型解析客户端()
    {
        using var context = new AuthenticationTestContext();

        await context.CreateUserStore().GetUserByUsernameAsync("alice");

        Assert.Contains(typeof(SysAuthUser), context.Resolver.RequestedEntityTypes);
    }

    /// <summary>
    /// 按用户标识查找
    /// </summary>
    [Fact]
    public async Task 按用户标识查找()
    {
        using var context = new AuthenticationTestContext();
        var userId = await context.CreateUserStore().AddUserAsync(NewUser("alice"));

        var found = await context.CreateUserStore().GetUserByIdAsync(userId);

        Assert.NotNull(found);
        Assert.Equal(userId, found.UserId);
        Assert.Equal("alice", found.Username);
    }

    /// <summary>
    /// 非正整数的用户标识返回空
    /// </summary>
    [Fact]
    public async Task 非正整数的用户标识返回空()
    {
        using var context = new AuthenticationTestContext();
        var store = context.CreateUserStore();

        Assert.Null(await store.GetUserByIdAsync("abc"));
        Assert.Null(await store.GetUserByIdAsync(""));
        Assert.Null(await store.GetUserByIdAsync("-1"));
        Assert.Null(await store.GetUserByIdAsync("0"));
    }

    /// <summary>
    /// 空白用户名返回空
    /// </summary>
    [Fact]
    public async Task 空白用户名返回空()
    {
        using var context = new AuthenticationTestContext();

        Assert.Null(await context.CreateUserStore().GetUserByUsernameAsync(" "));
    }

    /// <summary>
    /// 不同租户的同名用户互不可见
    /// </summary>
    [Fact]
    public async Task 不同租户的同名用户互不可见()
    {
        using var context = new AuthenticationTestContext();
        context.Tenant.Id = 1;
        await context.CreateUserStore().AddUserAsync(NewUser("alice", "a1@example.com"));
        context.Tenant.Id = 2;
        await context.CreateUserStore().AddUserAsync(NewUser("alice", "a2@example.com"));

        context.Tenant.Id = 1;
        var inFirst = await context.CreateUserStore().GetUserByUsernameAsync("alice");
        context.Tenant.Id = 2;
        var inSecond = await context.CreateUserStore().GetUserByUsernameAsync("alice");

        Assert.Equal("a1@example.com", inFirst?.Email);
        Assert.Equal("a2@example.com", inSecond?.Email);
    }

    /// <summary>
    /// 平台上下文看不到租户用户
    /// </summary>
    [Fact]
    public async Task 平台上下文看不到租户用户()
    {
        using var context = new AuthenticationTestContext();
        context.Tenant.Id = 1;
        await context.CreateUserStore().AddUserAsync(NewUser("alice"));

        context.Tenant.Id = null;

        Assert.Null(await context.CreateUserStore().GetUserByUsernameAsync("alice"));
    }

    /// <summary>
    /// 租户上下文看不到平台用户
    /// </summary>
    [Fact]
    public async Task 租户上下文看不到平台用户()
    {
        using var context = new AuthenticationTestContext();
        await context.CreateUserStore().AddUserAsync(NewUser("admin"));

        context.Tenant.Id = 1;

        Assert.Null(await context.CreateUserStore().GetUserByUsernameAsync("admin"));
    }

    /// <summary>
    /// 按标识查找不跨租户
    /// </summary>
    [Fact]
    public async Task 按标识查找不跨租户()
    {
        using var context = new AuthenticationTestContext();
        context.Tenant.Id = 1;
        var userId = await context.CreateUserStore().AddUserAsync(NewUser("alice"));

        context.Tenant.Id = 2;

        Assert.Null(await context.CreateUserStore().GetUserByIdAsync(userId));
    }

    /// <summary>
    /// 同一作用域内重复查找返回同一实例
    /// </summary>
    [Fact]
    public async Task 同一作用域内重复查找返回同一实例()
    {
        using var context = new AuthenticationTestContext();
        await context.CreateUserStore().AddUserAsync(NewUser("alice"));
        var store = context.CreateUserStore();

        var byName = await store.GetUserByUsernameAsync("alice");
        Assert.NotNull(byName);
        var byId = await store.GetUserByIdAsync(byName.UserId);

        Assert.Same(byName, byId);
    }

    /// <summary>
    /// 不同作用域返回不同实例
    /// </summary>
    [Fact]
    public async Task 不同作用域返回不同实例()
    {
        using var context = new AuthenticationTestContext();
        await context.CreateUserStore().AddUserAsync(NewUser("alice"));

        var first = await context.CreateUserStore().GetUserByUsernameAsync("alice");
        var second = await context.CreateUserStore().GetUserByUsernameAsync("alice");

        Assert.NotSame(first, second);
    }

    /// <summary>
    /// 添加用户生成雪花标识并回写
    /// </summary>
    [Fact]
    public async Task 添加用户生成雪花标识并回写()
    {
        using var context = new AuthenticationTestContext();
        var user = NewUser("alice");

        var userId = await context.CreateUserStore().AddUserAsync(user);

        Assert.True(long.Parse(userId) > 0);
        Assert.Equal(userId, user.UserId);
    }

    /// <summary>
    /// 添加用户沿用调用方给定的数字标识
    /// </summary>
    [Fact]
    public async Task 添加用户沿用调用方给定的数字标识()
    {
        using var context = new AuthenticationTestContext();
        var user = NewUser("alice");
        user.UserId = "123456789";

        var userId = await context.CreateUserStore().AddUserAsync(user);

        Assert.Equal("123456789", userId);
        Assert.NotNull(await context.CreateUserStore().GetUserByIdAsync("123456789"));
    }

    /// <summary>
    /// 添加非数字标识的用户抛出参数异常
    /// </summary>
    [Fact]
    public async Task 添加非数字标识的用户抛出参数异常()
    {
        using var context = new AuthenticationTestContext();
        var user = NewUser("alice");
        user.UserId = "alice-id";

        await Assert.ThrowsAsync<ArgumentException>(() => context.CreateUserStore().AddUserAsync(user));
    }

    /// <summary>
    /// 添加空白用户名抛出参数异常
    /// </summary>
    [Fact]
    public async Task 添加空白用户名抛出参数异常()
    {
        using var context = new AuthenticationTestContext();

        await Assert.ThrowsAsync<ArgumentException>(() => context.CreateUserStore().AddUserAsync(NewUser(" ")));
    }

    /// <summary>
    /// 用户名仅大小写不同也视为重复
    /// </summary>
    [Fact]
    public async Task 用户名仅大小写不同也视为重复()
    {
        using var context = new AuthenticationTestContext();
        await context.CreateUserStore().AddUserAsync(NewUser("Alice"));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => context.CreateUserStore().AddUserAsync(NewUser("alice")));
    }

    /// <summary>
    /// 添加用户原样保存密码哈希
    /// </summary>
    [Fact]
    public async Task 添加用户原样保存密码哈希()
    {
        using var context = new AuthenticationTestContext();
        var user = NewUser("alice");
        user.PasswordHash = "1:1000:SHA256:c2FsdA==:aGFzaA==";

        await context.CreateUserStore().AddUserAsync(user);

        var entity = context.Client.Queryable<SysAuthUser>().First();
        Assert.Equal("1:1000:SHA256:c2FsdA==:aGFzaA==", entity.PasswordHash);
        Assert.Equal("alice", entity.UserName);
        Assert.Equal("ALICE", entity.NormalizedUserName);
    }

    private static UserInfo NewUser(string username, string? email = null)
    {
        return new UserInfo
        {
            Username = username,
            PasswordHash = "hash",
            Email = email,
            IsActive = true
        };
    }
}
