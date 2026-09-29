// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text.Json;
using XiHan.Framework.Authentication.SqlSugar.Entities;
using XiHan.Framework.Authentication.SqlSugar.Mapping;
using XiHan.Framework.Authentication.Users;

namespace XiHan.Framework.Authentication.SqlSugar.Tests;

/// <summary>
/// 用户映射测试
/// </summary>
public class AuthUserMapperTests
{
    /// <summary>
    /// 用户名规范化为大写
    /// </summary>
    [Fact]
    public void 用户名规范化为大写()
    {
        Assert.Equal("ALICE", AuthUserMapper.NormalizeUserName("Alice"));
    }

    /// <summary>
    /// 往返保留全部字段
    /// </summary>
    [Fact]
    public void 往返保留全部字段()
    {
        var lockoutEnd = new DateTime(2026, 9, 28, 8, 0, 0, DateTimeKind.Utc);
        var lastLoginTime = new DateTime(2026, 9, 27, 8, 0, 0, DateTimeKind.Utc);
        var passwordChangedTime = new DateTime(2026, 9, 26, 8, 0, 0, DateTimeKind.Utc);
        var user = new UserInfo
        {
            UserId = "ignored",
            Username = "Alice",
            PasswordHash = "1:1000:SHA256:c2FsdA==:aGFzaA==",
            Email = "alice@example.com",
            PhoneNumber = "13800000000",
            TwoFactorEnabled = true,
            TwoFactorSecret = "JBSWY3DPEHPK3PXP",
            RecoveryCodes = ["hash-1", "hash-2"],
            IsLocked = true,
            LockoutEnd = lockoutEnd,
            FailedLoginAttempts = 3,
            LastLoginTime = lastLoginTime,
            PasswordChangedTime = passwordChangedTime,
            IsActive = false
        };

        var entity = AuthUserMapper.ToEntity(user, 42L, 7L);
        var restored = AuthUserMapper.ToUserInfo(entity);

        Assert.Equal(42L, entity.BasicId);
        Assert.Equal(7L, entity.TenantId);
        Assert.Equal("ALICE", entity.NormalizedUserName);
        Assert.Equal("42", restored.UserId);
        Assert.Equal("Alice", restored.Username);
        Assert.Equal("1:1000:SHA256:c2FsdA==:aGFzaA==", restored.PasswordHash);
        Assert.Equal("alice@example.com", restored.Email);
        Assert.Equal("13800000000", restored.PhoneNumber);
        Assert.True(restored.TwoFactorEnabled);
        Assert.Equal("JBSWY3DPEHPK3PXP", restored.TwoFactorSecret);
        string[] expectedCodes = ["hash-1", "hash-2"];
        Assert.Equal(expectedCodes, restored.RecoveryCodes);
        Assert.True(restored.IsLocked);
        Assert.Equal(lockoutEnd, restored.LockoutEnd);
        Assert.Equal(3, restored.FailedLoginAttempts);
        Assert.Equal(lastLoginTime, restored.LastLoginTime);
        Assert.Equal(passwordChangedTime, restored.PasswordChangedTime);
        Assert.False(restored.IsActive);
        Assert.Null(restored.AdditionalData);
    }

    /// <summary>
    /// 本地时间写入前换算为 UTC
    /// </summary>
    [Fact]
    public void 本地时间写入前换算为UTC()
    {
        var local = new DateTime(2026, 9, 28, 8, 0, 0, DateTimeKind.Local);

        var entity = AuthUserMapper.ToEntity(new UserInfo { Username = "alice", LockoutEnd = local }, 1L, 0L);

        Assert.NotNull(entity.LockoutEnd);
        Assert.Equal(local.ToUniversalTime(), entity.LockoutEnd.Value);
        Assert.Equal(DateTimeKind.Utc, entity.LockoutEnd.Value.Kind);
    }

    /// <summary>
    /// 未指定时区的时间按 UTC 处理
    /// </summary>
    [Fact]
    public void 未指定时区的时间按UTC处理()
    {
        var unspecified = new DateTime(2026, 9, 28, 8, 0, 0, DateTimeKind.Unspecified);

        var entity = AuthUserMapper.ToEntity(new UserInfo { Username = "alice", LockoutEnd = unspecified }, 1L, 0L);

        Assert.NotNull(entity.LockoutEnd);
        Assert.Equal(unspecified.Ticks, entity.LockoutEnd.Value.Ticks);
        Assert.Equal(DateTimeKind.Utc, entity.LockoutEnd.Value.Kind);
    }

    /// <summary>
    /// 读出的时间标记为 UTC 且不平移
    /// </summary>
    [Fact]
    public void 读出的时间标记为UTC且不平移()
    {
        var stored = new DateTime(2026, 9, 28, 8, 0, 0, DateTimeKind.Unspecified);
        var entity = new SysAuthUser(1L)
        {
            UserName = "alice",
            NormalizedUserName = "ALICE",
            LockoutEnd = stored
        };

        var restored = AuthUserMapper.ToUserInfo(entity);

        Assert.NotNull(restored.LockoutEnd);
        Assert.Equal(stored.Ticks, restored.LockoutEnd.Value.Ticks);
        Assert.Equal(DateTimeKind.Utc, restored.LockoutEnd.Value.Kind);
    }

    /// <summary>
    /// 空恢复码序列化为空数组
    /// </summary>
    [Fact]
    public void 空恢复码序列化为空数组()
    {
        Assert.Equal("[]", AuthUserMapper.SerializeRecoveryCodes([]));
        Assert.Equal("[]", AuthUserMapper.SerializeRecoveryCodes(null));
        Assert.Empty(AuthUserMapper.DeserializeRecoveryCodes(null));
        Assert.Empty(AuthUserMapper.DeserializeRecoveryCodes(" "));
    }

    /// <summary>
    /// 附加数据往返后值为 JsonElement
    /// </summary>
    [Fact]
    public void 附加数据往返后值为JsonElement()
    {
        var json = AuthUserMapper.SerializeAdditionalData(new Dictionary<string, object> { ["source"] = "import" });

        var restored = AuthUserMapper.DeserializeAdditionalData(json);

        Assert.NotNull(restored);
        var value = Assert.IsType<JsonElement>(restored["source"]);
        Assert.Equal("import", value.GetString());
    }

    /// <summary>
    /// 空附加数据不写入
    /// </summary>
    [Fact]
    public void 空附加数据不写入()
    {
        Assert.Null(AuthUserMapper.SerializeAdditionalData(null));
        Assert.Null(AuthUserMapper.DeserializeAdditionalData(null));
    }
}
