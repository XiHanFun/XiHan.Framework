// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Reflection;
using SqlSugar;
using XiHan.Framework.Authentication.SqlSugar.Entities;

namespace XiHan.Framework.Authentication.SqlSugar.Tests;

/// <summary>
/// 用户实体测试
/// </summary>
public class AuthUserEntityTests
{
    /// <summary>
    /// 表名为认证用户表
    /// </summary>
    [Fact]
    public void 表名为认证用户表()
    {
        var table = typeof(SysAuthUser).GetCustomAttribute<SugarTable>();

        Assert.NotNull(table);
        Assert.Equal("sys_auth_user", table.TableName);
    }

    /// <summary>
    /// 用户名唯一索引覆盖租户与规范化用户名
    /// </summary>
    [Fact]
    public void 用户名唯一索引覆盖租户与规范化用户名()
    {
        var index = Assert.Single(typeof(SysAuthUser).GetCustomAttributes<SugarIndexAttribute>());
        string[] expected = [nameof(SysAuthUser.TenantId), nameof(SysAuthUser.NormalizedUserName)];

        Assert.True(index.IsUnique);
        Assert.Equal(expected, index.IndexFields.Keys.ToArray());
    }

    /// <summary>
    /// 密码哈希列非空且长度为 512
    /// </summary>
    [Fact]
    public void 密码哈希列非空且长度为512()
    {
        var column = typeof(SysAuthUser)
            .GetProperty(nameof(SysAuthUser.PasswordHash))!
            .GetCustomAttribute<SugarColumn>();

        Assert.NotNull(column);
        Assert.Equal("Password_Hash", column.ColumnName);
        Assert.Equal(512, column.Length);
        Assert.False(column.IsNullable);
    }

    /// <summary>
    /// 同租户重复的规范化用户名被唯一索引拒绝
    /// </summary>
    [Fact]
    public void 同租户重复的规范化用户名被唯一索引拒绝()
    {
        using var context = new AuthenticationTestContext();

        context.Client.Insertable(NewEntity(1, 0, "alice")).ExecuteCommand();

        Assert.ThrowsAny<Exception>(() => context.Client.Insertable(NewEntity(2, 0, "Alice")).ExecuteCommand());
    }

    /// <summary>
    /// 不同租户可以使用相同用户名
    /// </summary>
    [Fact]
    public void 不同租户可以使用相同用户名()
    {
        using var context = new AuthenticationTestContext();

        context.Client.Insertable(NewEntity(1, 1, "alice")).ExecuteCommand();
        context.Client.Insertable(NewEntity(2, 2, "alice")).ExecuteCommand();

        Assert.Equal(2, context.Client.Queryable<SysAuthUser>().Count());
    }

    private static SysAuthUser NewEntity(long id, long tenantId, string userName)
    {
        return new SysAuthUser(id)
        {
            TenantId = tenantId,
            UserName = userName,
            NormalizedUserName = userName.ToUpperInvariant(),
            PasswordHash = "hash",
            IsActive = true
        };
    }
}
