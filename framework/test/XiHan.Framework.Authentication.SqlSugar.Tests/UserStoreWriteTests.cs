// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Authentication.SqlSugar.Entities;
using XiHan.Framework.Authentication.SqlSugar.Tests.Fakes;
using XiHan.Framework.Authentication.SqlSugar.Users;
using XiHan.Framework.Authentication.Users;

namespace XiHan.Framework.Authentication.SqlSugar.Tests;

/// <summary>
/// 用户存储写入测试
/// </summary>
public class UserStoreWriteTests
{
    private static readonly DateTime FutureLockoutEnd = new(2099, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime PastLockoutEnd = new(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    /// <summary>
    /// 更新用户信息不改写密码哈希
    /// </summary>
    [Fact]
    public async Task 更新用户信息不改写密码哈希()
    {
        using var context = new AuthenticationTestContext();
        var userId = await context.CreateUserStore().AddUserAsync(NewUser("alice"));

        var editor = context.CreateUserStore();
        var stale = await editor.GetUserByIdAsync(userId);
        Assert.NotNull(stale);
        await context.CreateUserStore().UpdatePasswordAsync(userId, "new-hash");

        stale.Email = "alice@example.com";
        await editor.UpdateUserAsync(stale);

        var stored = await context.CreateUserStore().GetUserByIdAsync(userId);
        Assert.NotNull(stored);
        Assert.Equal("new-hash", stored.PasswordHash);
        Assert.Equal("alice@example.com", stored.Email);
    }

    /// <summary>
    /// 更新用户信息写入其余字段
    /// </summary>
    [Fact]
    public async Task 更新用户信息写入其余字段()
    {
        using var context = new AuthenticationTestContext();
        var userId = await context.CreateUserStore().AddUserAsync(NewUser("alice"));
        var store = context.CreateUserStore();
        var user = await store.GetUserByIdAsync(userId);
        Assert.NotNull(user);

        var lastLoginTime = new DateTime(2026, 9, 28, 1, 2, 3, DateTimeKind.Utc);
        user.Username = "Alice2";
        user.Email = "alice@example.com";
        user.PhoneNumber = "13800000000";
        user.TwoFactorEnabled = true;
        user.TwoFactorSecret = "JBSWY3DPEHPK3PXP";
        user.RecoveryCodes = ["hash-1", "hash-2"];
        user.IsActive = false;
        user.LastLoginTime = lastLoginTime;
        await store.UpdateUserAsync(user);

        var stored = await context.CreateUserStore().GetUserByUsernameAsync("alice2");
        Assert.NotNull(stored);
        Assert.Equal(userId, stored.UserId);
        Assert.Equal("Alice2", stored.Username);
        Assert.Equal("alice@example.com", stored.Email);
        Assert.Equal("13800000000", stored.PhoneNumber);
        Assert.True(stored.TwoFactorEnabled);
        Assert.Equal("JBSWY3DPEHPK3PXP", stored.TwoFactorSecret);
        string[] expectedCodes = ["hash-1", "hash-2"];
        Assert.Equal(expectedCodes, stored.RecoveryCodes);
        Assert.False(stored.IsActive);
        Assert.NotNull(stored.LastLoginTime);
        Assert.Equal(lastLoginTime, stored.LastLoginTime.Value);
        Assert.Equal(DateTimeKind.Utc, stored.LastLoginTime.Value.Kind);
    }

    /// <summary>
    /// 更新用户信息不改写其他请求写入的失败次数与锁定
    /// </summary>
    [Fact]
    public async Task 更新用户信息不改写其他请求写入的失败次数与锁定()
    {
        using var context = new AuthenticationTestContext();
        var userId = await context.CreateUserStore().AddUserAsync(NewUser("alice"));

        var editor = context.CreateUserStore();
        var stale = await editor.GetUserByIdAsync(userId);
        Assert.NotNull(stale);
        var other = context.CreateUserStore();
        await other.IncrementFailedLoginAttemptsAsync("alice");
        await other.SetLockoutEndAsync("alice", FutureLockoutEnd);

        stale.Email = "alice@example.com";
        await editor.UpdateUserAsync(stale);

        var stored = await context.CreateUserStore().GetUserByIdAsync(userId);
        Assert.NotNull(stored);
        Assert.Equal("alice@example.com", stored.Email);
        Assert.Equal(1, stored.FailedLoginAttempts);
        Assert.True(stored.IsLocked);
        Assert.Equal(FutureLockoutEnd, stored.LockoutEnd);
    }

    /// <summary>
    /// 更新用户信息可写入空值
    /// </summary>
    [Fact]
    public async Task 更新用户信息可写入空值()
    {
        using var context = new AuthenticationTestContext();
        var seed = NewUser("alice");
        seed.Email = "alice@example.com";
        seed.PhoneNumber = "13800000000";
        seed.TwoFactorSecret = "JBSWY3DPEHPK3PXP";
        seed.LastLoginTime = new DateTime(2026, 9, 28, 1, 2, 3, DateTimeKind.Utc);
        var userId = await context.CreateUserStore().AddUserAsync(seed);
        var store = context.CreateUserStore();
        var user = await store.GetUserByIdAsync(userId);
        Assert.NotNull(user);

        user.Email = null;
        user.PhoneNumber = null;
        user.TwoFactorSecret = null;
        user.LastLoginTime = null;
        await store.UpdateUserAsync(user);

        var stored = await context.CreateUserStore().GetUserByIdAsync(userId);
        Assert.NotNull(stored);
        Assert.Null(stored.Email);
        Assert.Null(stored.PhoneNumber);
        Assert.Null(stored.TwoFactorSecret);
        Assert.Null(stored.LastLoginTime);
    }

    /// <summary>
    /// 更新不存在的用户抛出
    /// </summary>
    [Fact]
    public async Task 更新不存在的用户抛出()
    {
        using var context = new AuthenticationTestContext();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => context.CreateUserStore().UpdateUserAsync(new UserInfo { UserId = "999", Username = "ghost" }));
    }

    /// <summary>
    /// 更新非数字用户标识抛出参数异常
    /// </summary>
    [Fact]
    public async Task 更新非数字用户标识抛出参数异常()
    {
        using var context = new AuthenticationTestContext();

        await Assert.ThrowsAsync<ArgumentException>(
            () => context.CreateUserStore().UpdateUserAsync(new UserInfo { UserId = "abc", Username = "ghost" }));
    }

    /// <summary>
    /// 更新其他租户的用户视为不存在
    /// </summary>
    [Fact]
    public async Task 更新其他租户的用户视为不存在()
    {
        using var context = new AuthenticationTestContext();
        context.Tenant.Id = 1;
        var userId = await context.CreateUserStore().AddUserAsync(NewUser("alice"));

        context.Tenant.Id = 2;

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => context.CreateUserStore().UpdateUserAsync(new UserInfo { UserId = userId, Username = "alice" }));
    }

    /// <summary>
    /// 更新密码原样写入哈希
    /// </summary>
    [Fact]
    public async Task 更新密码原样写入哈希()
    {
        using var context = new AuthenticationTestContext();
        var userId = await context.CreateUserStore().AddUserAsync(NewUser("alice"));

        await context.CreateUserStore().UpdatePasswordAsync(userId, "1:1000:SHA256:c2FsdA==:aGFzaA==");

        var entity = context.Client.Queryable<SysAuthUser>().First();
        Assert.Equal("1:1000:SHA256:c2FsdA==:aGFzaA==", entity.PasswordHash);
    }

    /// <summary>
    /// 更新密码的参数校验
    /// </summary>
    [Fact]
    public async Task 更新密码的参数校验()
    {
        using var context = new AuthenticationTestContext();
        var userId = await context.CreateUserStore().AddUserAsync(NewUser("alice"));
        var store = context.CreateUserStore();

        await Assert.ThrowsAsync<ArgumentException>(() => store.UpdatePasswordAsync(userId, " "));
        await Assert.ThrowsAsync<ArgumentException>(() => store.UpdatePasswordAsync(" ", "hash"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.UpdatePasswordAsync("999", "hash"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.UpdatePasswordAsync("abc", "hash"));
    }

    /// <summary>
    /// 失败次数在数据库侧累加
    /// </summary>
    [Fact]
    public async Task 失败次数在数据库侧累加()
    {
        using var context = new AuthenticationTestContext();
        await context.CreateUserStore().AddUserAsync(NewUser("alice"));
        var first = context.CreateUserStore();
        var second = context.CreateUserStore();
        await first.GetUserByUsernameAsync("alice");

        await second.IncrementFailedLoginAttemptsAsync("alice");
        await first.IncrementFailedLoginAttemptsAsync("alice");

        Assert.Equal(2, await context.CreateUserStore().GetFailedLoginAttemptsAsync("alice"));
    }

    /// <summary>
    /// 重置失败次数
    /// </summary>
    [Fact]
    public async Task 重置失败次数()
    {
        using var context = new AuthenticationTestContext();
        await context.CreateUserStore().AddUserAsync(NewUser("alice"));
        var store = context.CreateUserStore();
        await store.IncrementFailedLoginAttemptsAsync("alice");
        await store.IncrementFailedLoginAttemptsAsync("alice");

        await store.ResetFailedLoginAttemptsAsync("alice");

        Assert.Equal(0, await context.CreateUserStore().GetFailedLoginAttemptsAsync("alice"));
    }

    /// <summary>
    /// 写入方法同步更新已加载的实例
    /// </summary>
    [Fact]
    public async Task 写入方法同步更新已加载的实例()
    {
        using var context = new AuthenticationTestContext();
        await context.CreateUserStore().AddUserAsync(NewUser("alice"));
        var store = context.CreateUserStore();
        var user = await store.GetUserByUsernameAsync("alice");
        Assert.NotNull(user);

        await store.UpdatePasswordAsync(user.UserId, "new-hash");
        await store.IncrementFailedLoginAttemptsAsync("alice");
        await store.SetLockoutEndAsync("alice", FutureLockoutEnd);

        Assert.Equal("new-hash", user.PasswordHash);
        Assert.Equal(1, user.FailedLoginAttempts);
        Assert.True(user.IsLocked);
        Assert.Equal(FutureLockoutEnd, user.LockoutEnd);
    }

    /// <summary>
    /// 锁定结束时间在未来时标记为锁定
    /// </summary>
    [Fact]
    public async Task 锁定结束时间在未来时标记为锁定()
    {
        using var context = new AuthenticationTestContext();
        await context.CreateUserStore().AddUserAsync(NewUser("alice"));

        await context.CreateUserStore().SetLockoutEndAsync("alice", FutureLockoutEnd);

        var store = context.CreateUserStore();
        var lockoutEnd = await store.GetLockoutEndAsync("alice");
        var user = await store.GetUserByUsernameAsync("alice");
        Assert.NotNull(lockoutEnd);
        Assert.Equal(FutureLockoutEnd, lockoutEnd.Value);
        Assert.Equal(DateTimeKind.Utc, lockoutEnd.Value.Kind);
        Assert.True(user?.IsLocked);
    }

    /// <summary>
    /// 锁定结束时间已过去时不标记为锁定
    /// </summary>
    [Fact]
    public async Task 锁定结束时间已过去时不标记为锁定()
    {
        using var context = new AuthenticationTestContext();
        await context.CreateUserStore().AddUserAsync(NewUser("alice"));

        await context.CreateUserStore().SetLockoutEndAsync("alice", PastLockoutEnd);

        var user = await context.CreateUserStore().GetUserByUsernameAsync("alice");
        Assert.False(user?.IsLocked);
        Assert.Equal(PastLockoutEnd, user?.LockoutEnd);
    }

    /// <summary>
    /// 清除锁定结束时间
    /// </summary>
    [Fact]
    public async Task 清除锁定结束时间()
    {
        using var context = new AuthenticationTestContext();
        await context.CreateUserStore().AddUserAsync(NewUser("alice"));
        await context.CreateUserStore().SetLockoutEndAsync("alice", FutureLockoutEnd);

        await context.CreateUserStore().SetLockoutEndAsync("alice", null);

        var store = context.CreateUserStore();
        Assert.Null(await store.GetLockoutEndAsync("alice"));
        Assert.False((await store.GetUserByUsernameAsync("alice"))?.IsLocked);
    }

    /// <summary>
    /// 计数操作不跨租户
    /// </summary>
    [Fact]
    public async Task 计数操作不跨租户()
    {
        using var context = new AuthenticationTestContext();
        context.Tenant.Id = 1;
        await context.CreateUserStore().AddUserAsync(NewUser("alice"));
        context.Tenant.Id = 2;
        await context.CreateUserStore().AddUserAsync(NewUser("alice"));

        await context.CreateUserStore().IncrementFailedLoginAttemptsAsync("alice");

        Assert.Equal(1, await context.CreateUserStore().GetFailedLoginAttemptsAsync("alice"));
        context.Tenant.Id = 1;
        Assert.Equal(0, await context.CreateUserStore().GetFailedLoginAttemptsAsync("alice"));
    }

    /// <summary>
    /// 不存在的用户名计数操作静默忽略
    /// </summary>
    [Fact]
    public async Task 不存在的用户名计数操作静默忽略()
    {
        using var context = new AuthenticationTestContext();
        var store = context.CreateUserStore();

        await store.IncrementFailedLoginAttemptsAsync("ghost");
        await store.ResetFailedLoginAttemptsAsync("ghost");
        await store.SetLockoutEndAsync("ghost", FutureLockoutEnd);

        Assert.Equal(0, await store.GetFailedLoginAttemptsAsync("ghost"));
        Assert.Null(await store.GetLockoutEndAsync("ghost"));
        Assert.Equal(0, await context.Client.Queryable<SysAuthUser>().CountAsync());
    }

    /// <summary>
    /// 插入撞到唯一索引且同名用户已存在时抛出契约异常
    /// </summary>
    [Fact]
    public async Task 插入撞到唯一索引且同名用户已存在时抛出契约异常()
    {
        using var context = new AuthenticationTestContext();
        var inserted = false;
        context.Client.Aop.OnLogExecuting = (sql, _) =>
        {
            if (inserted || !sql.TrimStart().StartsWith("INSERT", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            inserted = true;
            using var other = context.CreateClient(autoClose: true);
            var rival = new SqlSugarUserStore(new StubClientResolver(other), context.Tenant, context.IdGenerator, context.Clock);
            rival.AddUserAsync(NewUser("alice")).GetAwaiter().GetResult();
        };

        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => context.CreateUserStore().AddUserAsync(NewUser("alice")));

        Assert.Contains("alice", error.Message);
        Assert.True(inserted);
    }

    /// <summary>
    /// 插入失败后重查也失败时，抛出同时包含插入异常与重查异常的聚合异常
    /// </summary>
    [Fact]
    public async Task 插入失败后重查也失败时抛出包含两个异常的聚合异常()
    {
        using var context = new AuthenticationTestContext();
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

        var exception = await Assert.ThrowsAsync<AggregateException>(() => context.CreateUserStore().AddUserAsync(NewUser("alice")));

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

    private static UserInfo NewUser(string username)
    {
        return new UserInfo
        {
            Username = username,
            PasswordHash = "hash",
            IsActive = true
        };
    }
}
