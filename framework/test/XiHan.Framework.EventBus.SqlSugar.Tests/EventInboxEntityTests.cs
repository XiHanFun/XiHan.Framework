// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Reflection;
using SqlSugar;
using XiHan.Framework.Data.SqlSugar.Initializers;
using XiHan.Framework.EventBus.SqlSugar.Entities;

namespace XiHan.Framework.EventBus.SqlSugar.Tests;

/// <summary>
/// 收件箱实体映射测试
/// </summary>
public class EventInboxEntityTests
{
    /// <summary>
    /// 表名带 sys 前缀且不分表
    /// </summary>
    [Fact]
    public void 表名带前缀且不分表()
    {
        var table = typeof(SysEventInbox).GetCustomAttribute<SugarTable>();

        Assert.NotNull(table);
        Assert.Equal("sys_event_inbox", table.TableName);
        Assert.Null(typeof(SysEventInbox).GetCustomAttribute<SplitTableAttribute>());
    }

    /// <summary>
    /// 主键是事件自身的标识且非自增
    /// </summary>
    [Fact]
    public void 主键是事件标识且非自增()
    {
        var property = typeof(SysEventInbox).GetProperty(nameof(SysEventInbox.BasicId));
        var column = property!.GetCustomAttribute<SugarColumn>();

        Assert.Equal(typeof(Guid), property!.PropertyType);
        Assert.NotNull(column);
        Assert.True(column.IsPrimaryKey);
        Assert.False(column.IsIdentity);
    }

    /// <summary>
    /// 列名使用帕斯卡下划线
    /// </summary>
    /// <param name="propertyName">属性名</param>
    /// <param name="columnName">预期列名</param>
    [Theory]
    [InlineData(nameof(SysEventInbox.MessageId), "Message_Id")]
    [InlineData(nameof(SysEventInbox.DedupKey), "Dedup_Key")]
    [InlineData(nameof(SysEventInbox.EventName), "Event_Name")]
    [InlineData(nameof(SysEventInbox.EventData), "Event_Data")]
    [InlineData(nameof(SysEventInbox.CreatedTime), "Created_Time")]
    [InlineData(nameof(SysEventInbox.ExtraProperties), "Extra_Properties")]
    [InlineData(nameof(SysEventInbox.Status), "Status")]
    [InlineData(nameof(SysEventInbox.RetryCount), "Retry_Count")]
    [InlineData(nameof(SysEventInbox.NextRetryTime), "Next_Retry_Time")]
    [InlineData(nameof(SysEventInbox.ClaimToken), "Claim_Token")]
    [InlineData(nameof(SysEventInbox.ClaimTime), "Claim_Time")]
    [InlineData(nameof(SysEventInbox.HandledTime), "Handled_Time")]
    public void 列名使用帕斯卡下划线(string propertyName, string columnName)
    {
        var column = typeof(SysEventInbox).GetProperty(propertyName)!.GetCustomAttribute<SugarColumn>();

        Assert.NotNull(column);
        Assert.Equal(columnName, column.ColumnName);
    }

    /// <summary>
    /// 消息标识可空而去重键非空，两者长度一致
    /// </summary>
    [Fact]
    public void 消息标识可空而去重键非空()
    {
        var messageId = typeof(SysEventInbox).GetProperty(nameof(SysEventInbox.MessageId))!.GetCustomAttribute<SugarColumn>();
        var dedupKey = typeof(SysEventInbox).GetProperty(nameof(SysEventInbox.DedupKey))!.GetCustomAttribute<SugarColumn>();

        Assert.NotNull(messageId);
        Assert.NotNull(dedupKey);
        Assert.True(messageId.IsNullable);
        Assert.False(dedupKey.IsNullable);
        Assert.Equal(256, messageId.Length);
        Assert.Equal(256, dedupKey.Length);
    }

    /// <summary>
    /// 事件名长度上限与契约一致
    /// </summary>
    [Fact]
    public void 事件名长度上限与契约一致()
    {
        var column = typeof(SysEventInbox).GetProperty(nameof(SysEventInbox.EventName))!.GetCustomAttribute<SugarColumn>();

        Assert.NotNull(column);
        Assert.Equal(256, column.Length);
    }

    /// <summary>
    /// 收件箱只在平台库建表
    /// </summary>
    [Fact]
    public void 收件箱只在平台库建表()
    {
        var attribute = typeof(SysEventInbox).GetCustomAttribute<TableInitializationAttribute>(inherit: true);

        Assert.NotNull(attribute);
        Assert.True(attribute.Enabled);
        Assert.False(attribute.IncludeModuleConnections);
        Assert.Equal(DbInitializationTarget.Platform, attribute.Target);
    }

    /// <summary>
    /// 去重键带唯一索引
    /// </summary>
    [Fact]
    public void 去重键带唯一索引()
    {
        var index = typeof(SysEventInbox).GetCustomAttributes<SugarIndexAttribute>()
            .SingleOrDefault(item => item.IsUnique);

        Assert.NotNull(index);
        Assert.Equal("ux_sys_event_inbox_dedup_key", index.IndexName);
        Assert.Single(index.IndexFields);
        Assert.True(index.IndexFields.ContainsKey(nameof(SysEventInbox.DedupKey)));
    }

    /// <summary>
    /// 状态索引覆盖状态与创建时间
    /// </summary>
    [Fact]
    public void 状态索引覆盖状态与创建时间()
    {
        var index = typeof(SysEventInbox).GetCustomAttributes<SugarIndexAttribute>()
            .SingleOrDefault(item => !item.IsUnique);

        Assert.NotNull(index);
        Assert.Equal("ix_sys_event_inbox_status", index.IndexName);
        Assert.Equal(2, index.IndexFields.Count);
        Assert.True(index.IndexFields.ContainsKey(nameof(SysEventInbox.Status)));
        Assert.True(index.IndexFields.ContainsKey(nameof(SysEventInbox.CreatedTime)));
    }

    /// <summary>
    /// 主键经构造函数传入
    /// </summary>
    [Fact]
    public void 主键经构造函数传入()
    {
        var id = Guid.NewGuid();

        var entity = new SysEventInbox(id);

        Assert.Equal(id, entity.BasicId);
    }
}
