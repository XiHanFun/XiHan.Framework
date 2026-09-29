// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Reflection;
using SqlSugar;
using XiHan.Framework.Authentication.SqlSugar.Entities;
using XiHan.Framework.Authentication.SqlSugar.Options;
using XiHan.Framework.Authentication.SqlSugar.RefreshTokens;

namespace XiHan.Framework.Authentication.SqlSugar.Tests;

/// <summary>
/// 刷新令牌实体、哈希与配置测试
/// </summary>
public class RefreshTokenEntityTests
{
    /// <summary>
    /// 表名为刷新令牌表
    /// </summary>
    [Fact]
    public void 表名为刷新令牌表()
    {
        var table = typeof(SysAuthRefreshToken).GetCustomAttribute<SugarTable>();

        Assert.NotNull(table);
        Assert.Equal("sys_auth_refresh_token", table.TableName);
    }

    /// <summary>
    /// 令牌哈希唯一且另有主体与过期时间索引
    /// </summary>
    [Fact]
    public void 令牌哈希唯一且另有主体与过期时间索引()
    {
        var indexes = typeof(SysAuthRefreshToken).GetCustomAttributes<SugarIndexAttribute>().ToList();
        var unique = Assert.Single(indexes, item => item.IsUnique);
        string[] expected = [nameof(SysAuthRefreshToken.TokenHash)];

        Assert.Equal(3, indexes.Count);
        Assert.Equal(expected, unique.IndexFields.Keys.ToArray());
    }

    /// <summary>
    /// 令牌哈希列长度为 64 且非空
    /// </summary>
    [Fact]
    public void 令牌哈希列长度为64且非空()
    {
        var column = typeof(SysAuthRefreshToken)
            .GetProperty(nameof(SysAuthRefreshToken.TokenHash))!
            .GetCustomAttribute<SugarColumn>();

        Assert.NotNull(column);
        Assert.Equal("Token_Hash", column.ColumnName);
        Assert.Equal(64, column.Length);
        Assert.False(column.IsNullable);
    }

    /// <summary>
    /// 同一令牌哈希被唯一索引拒绝
    /// </summary>
    [Fact]
    public void 同一令牌哈希被唯一索引拒绝()
    {
        using var context = new AuthenticationTestContext(typeof(SysAuthRefreshToken));
        var tokenHash = RefreshTokenHasher.Hash("token-1");

        context.Client.Insertable(NewEntity(1, tokenHash)).ExecuteCommand();

        Assert.ThrowsAny<Exception>(() => context.Client.Insertable(NewEntity(2, tokenHash)).ExecuteCommand());
    }

    /// <summary>
    /// 哈希为 SHA-256 大写十六进制
    /// </summary>
    [Fact]
    public void 哈希为SHA256大写十六进制()
    {
        Assert.Equal(
            "BA7816BF8F01CFEA414140DE5DAE2223B00361A396177A9CB410FF61F20015AD",
            RefreshTokenHasher.Hash("abc"));
    }

    /// <summary>
    /// 不同令牌的哈希不同
    /// </summary>
    [Fact]
    public void 不同令牌的哈希不同()
    {
        Assert.NotEqual(RefreshTokenHasher.Hash("token-1"), RefreshTokenHasher.Hash("token-2"));
    }

    /// <summary>
    /// 配置默认值与配置节名
    /// </summary>
    [Fact]
    public void 配置默认值与配置节名()
    {
        var options = new XiHanAuthenticationSqlSugarOptions();

        Assert.Equal("XiHan:Authentication:SqlSugar", XiHanAuthenticationSqlSugarOptions.SectionName);
        Assert.True(options.RefreshTokenReuseDetection);
        Assert.Equal(TimeSpan.Zero, options.RefreshTokenReuseGracePeriod);
        Assert.Equal(256, options.RefreshTokenCleanupFrequency);
    }

    private static SysAuthRefreshToken NewEntity(long id, string tokenHash)
    {
        return new SysAuthRefreshToken(id)
        {
            TokenHash = tokenHash,
            Subject = "1001",
            ExpiresAt = new DateTime(2099, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            CreatedTime = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        };
    }
}
