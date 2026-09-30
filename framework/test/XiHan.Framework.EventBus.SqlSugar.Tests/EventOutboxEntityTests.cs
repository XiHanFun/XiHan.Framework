// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Reflection;
using SqlSugar;
using XiHan.Framework.Data.SqlSugar.Initializers;
using XiHan.Framework.EventBus.SqlSugar.Entities;

namespace XiHan.Framework.EventBus.SqlSugar.Tests;

/// <summary>
/// 发件箱实体映射测试
/// </summary>
public class EventOutboxEntityTests
{
    /// <summary>
    /// 表名带 sys 前缀且不含分表变量
    /// </summary>
    [Fact]
    public void 表名带前缀且不含分表变量()
    {
        var table = typeof(SysEventOutbox).GetCustomAttribute<SugarTable>();

        Assert.NotNull(table);
        Assert.Equal("sys_event_outbox", table.TableName);
    }

    /// <summary>
    /// 发件箱不分表
    /// </summary>
    [Fact]
    public void 发件箱不分表()
    {
        Assert.Null(typeof(SysEventOutbox).GetCustomAttribute<SplitTableAttribute>());
    }

    /// <summary>
    /// 主键是事件自身的标识且非自增
    /// </summary>
    [Fact]
    public void 主键是事件标识且非自增()
    {
        var property = typeof(SysEventOutbox).GetProperty(nameof(SysEventOutbox.BasicId));
        var column = property!.GetCustomAttribute<SugarColumn>();

        Assert.Equal(typeof(Guid), property!.PropertyType);
        Assert.NotNull(column);
        Assert.True(column.IsPrimaryKey);
        Assert.False(column.IsIdentity);
    }

    /// <summary>
    /// 事件名长度上限与契约一致
    /// </summary>
    [Fact]
    public void 事件名长度上限与契约一致()
    {
        var property = typeof(SysEventOutbox).GetProperty(nameof(SysEventOutbox.EventName));
        var column = property!.GetCustomAttribute<SugarColumn>();

        Assert.NotNull(column);
        Assert.Equal("Event_Name", column.ColumnName);
        Assert.Equal(256, column.Length);
    }

    /// <summary>
    /// 列名使用帕斯卡下划线
    /// </summary>
    [Theory]
    [InlineData(nameof(SysEventOutbox.EventData), "Event_Data")]
    [InlineData(nameof(SysEventOutbox.CreatedTime), "Created_Time")]
    [InlineData(nameof(SysEventOutbox.ExtraProperties), "Extra_Properties")]
    [InlineData(nameof(SysEventOutbox.Status), "Status")]
    [InlineData(nameof(SysEventOutbox.ClaimToken), "Claim_Token")]
    [InlineData(nameof(SysEventOutbox.ClaimTime), "Claim_Time")]
    public void 列名使用帕斯卡下划线(string propertyName, string expectedColumnName)
    {
        var property = typeof(SysEventOutbox).GetProperty(propertyName);
        var column = property!.GetCustomAttribute<SugarColumn>();

        Assert.NotNull(column);
        Assert.Equal(expectedColumnName, column.ColumnName);
    }

    /// <summary>
    /// 发件箱在模块库也建表
    /// </summary>
    [Fact]
    public void 发件箱在模块库也建表()
    {
        var attribute = typeof(SysEventOutbox).GetCustomAttribute<TableInitializationAttribute>(inherit: true);

        Assert.NotNull(attribute);
        Assert.True(attribute.IncludeModuleConnections);
    }

    /// <summary>
    /// 主键经构造函数传入
    /// </summary>
    [Fact]
    public void 主键经构造函数传入()
    {
        var id = Guid.NewGuid();

        var entity = new SysEventOutbox(id);

        Assert.Equal(id, entity.BasicId);
    }
}
