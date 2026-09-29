// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Reflection;
using SqlSugar;
using XiHan.Framework.Security.SqlSugar.Entities;

namespace XiHan.Framework.Security.SqlSugar.Tests;

/// <summary>
/// 密码历史实体映射测试
/// </summary>
public class EntityMappingTests
{
    /// <summary>
    /// 表名符合约定
    /// </summary>
    [Fact]
    public void 表名符合约定()
    {
        var table = typeof(SysPasswordHistory).GetCustomAttribute<SugarTable>();

        Assert.NotNull(table);
        Assert.Equal("sys_password_history", table.TableName);
    }

    /// <summary>
    /// 用户标识列名使用帕斯卡下划线
    /// </summary>
    [Fact]
    public void 用户标识列名使用帕斯卡下划线()
    {
        var property = typeof(SysPasswordHistory).GetProperty(nameof(SysPasswordHistory.UserId));
        var column = property!.GetCustomAttribute<SugarColumn>();

        Assert.NotNull(column);
        Assert.Equal("User_Id", column.ColumnName);
        Assert.False(column.IsNullable);
    }

    /// <summary>
    /// 密码哈希列非空且有足够列宽
    /// </summary>
    [Fact]
    public void 密码哈希列非空且有足够列宽()
    {
        var property = typeof(SysPasswordHistory).GetProperty(nameof(SysPasswordHistory.PasswordHash));
        var column = property!.GetCustomAttribute<SugarColumn>();

        Assert.NotNull(column);
        Assert.Equal("Password_Hash", column.ColumnName);
        Assert.False(column.IsNullable);
        Assert.True(column.Length >= 512);
    }

    /// <summary>
    /// 主键经构造函数传入
    /// </summary>
    [Fact]
    public void 主键经构造函数传入()
    {
        var entity = new SysPasswordHistory(1001L);

        Assert.Equal(1001L, entity.BasicId);
    }
}
