// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Reflection;
using SqlSugar;
using XiHan.Framework.Auditing.SqlSugar.Entities;
using XiHan.Framework.Domain.Entities.Abstracts;

namespace XiHan.Framework.Auditing.SqlSugar.Tests;

/// <summary>
/// 日志实体映射测试
/// </summary>
public class EntityMappingTests
{
    /// <summary>
    /// 操作日志表名使用 sys_ 前缀并带三个分表变量
    /// </summary>
    [Fact]
    public void SysOperationLog_表名带前缀与三个分表变量()
    {
        var table = typeof(SysOperationLog).GetCustomAttribute<SugarTable>();

        Assert.NotNull(table);
        Assert.Equal("sys_operation_log_{year}{month}{day}", table.TableName);
    }

    /// <summary>
    /// 操作日志按月分表
    /// </summary>
    [Fact]
    public void SysOperationLog_按月分表()
    {
        var split = typeof(SysOperationLog).GetCustomAttribute<SplitTableAttribute>();

        Assert.NotNull(split);
        Assert.Equal(SplitType.Month, split.SplitType);
    }

    /// <summary>
    /// 分表字段标注在 CreatedTime 上
    /// </summary>
    [Fact]
    public void SysOperationLog_分表字段为创建时间()
    {
        var property = typeof(SysOperationLog).GetProperty(nameof(SysOperationLog.CreatedTime));

        Assert.NotNull(property);
        Assert.NotNull(property!.GetCustomAttribute<SplitFieldAttribute>());
    }

    /// <summary>
    /// 实体实现分表标记接口
    /// </summary>
    [Fact]
    public void SysOperationLog_实现分表标记接口()
    {
        Assert.True(typeof(ISplitTableEntity).IsAssignableFrom(typeof(SysOperationLog)));
    }

    /// <summary>
    /// 列名使用 Pascal_Snake_Case
    /// </summary>
    [Fact]
    public void SysOperationLog_列名使用帕斯卡下划线()
    {
        var property = typeof(SysOperationLog).GetProperty(nameof(SysOperationLog.TraceId));
        var column = property!.GetCustomAttribute<SugarColumn>();

        Assert.NotNull(column);
        Assert.Equal("Trace_Id", column!.ColumnName);
    }

    /// <summary>
    /// 四个实体的表名均带 sys_ 前缀与三个分表变量
    /// </summary>
    [Theory]
    [InlineData(typeof(SysAccessLog), "sys_access_log_{year}{month}{day}")]
    [InlineData(typeof(SysApiLog), "sys_api_log_{year}{month}{day}")]
    [InlineData(typeof(SysExceptionLog), "sys_exception_log_{year}{month}{day}")]
    [InlineData(typeof(SysLoginLog), "sys_login_log_{year}{month}{day}")]
    public void 其余日志实体_表名符合约定(Type entityType, string expectedTableName)
    {
        var table = entityType.GetCustomAttribute<SugarTable>();

        Assert.NotNull(table);
        Assert.Equal(expectedTableName, table!.TableName);
    }

    /// <summary>
    /// 四个实体均按月分表且分表字段为创建时间
    /// </summary>
    [Theory]
    [InlineData(typeof(SysAccessLog))]
    [InlineData(typeof(SysApiLog))]
    [InlineData(typeof(SysExceptionLog))]
    [InlineData(typeof(SysLoginLog))]
    public void 其余日志实体_按月分表且分表字段为创建时间(Type entityType)
    {
        var split = entityType.GetCustomAttribute<SplitTableAttribute>();
        Assert.NotNull(split);
        Assert.Equal(SplitType.Month, split!.SplitType);

        var property = entityType.GetProperty("CreatedTime");
        Assert.NotNull(property);
        Assert.NotNull(property!.GetCustomAttribute<SplitFieldAttribute>());

        Assert.True(typeof(ISplitTableEntity).IsAssignableFrom(entityType));
    }

    /// <summary>
    /// 登录日志的业务时间与分表字段是两个不同的列
    /// </summary>
    [Fact]
    public void SysLoginLog_登录时间与创建时间分列()
    {
        var loginTime = typeof(SysLoginLog).GetProperty(nameof(SysLoginLog.LoginTime));
        var column = loginTime!.GetCustomAttribute<SugarColumn>();

        Assert.NotNull(column);
        Assert.Equal("Login_Time", column!.ColumnName);
        Assert.Null(loginTime.GetCustomAttribute<SplitFieldAttribute>());
    }

    /// <summary>
    /// 差异日志表名带前缀与三个分表变量
    /// </summary>
    [Fact]
    public void SysDiffLog_表名带前缀与三个分表变量()
    {
        var table = typeof(SysDiffLog).GetCustomAttribute<SugarTable>();

        Assert.NotNull(table);
        Assert.Equal("sys_diff_log_{year}{month}{day}", table.TableName);
    }

    /// <summary>
    /// 差异日志按月分表且分表字段为创建时间
    /// </summary>
    [Fact]
    public void SysDiffLog_按月分表且分表字段为创建时间()
    {
        var split = typeof(SysDiffLog).GetCustomAttribute<SplitTableAttribute>();
        Assert.NotNull(split);
        Assert.Equal(SplitType.Month, split!.SplitType);

        var property = typeof(SysDiffLog).GetProperty(nameof(SysDiffLog.CreatedTime));
        Assert.NotNull(property);
        Assert.NotNull(property!.GetCustomAttribute<SplitFieldAttribute>());

        Assert.True(typeof(ISplitTableEntity).IsAssignableFrom(typeof(SysDiffLog)));
    }

    /// <summary>
    /// 差异日志的实体标识列不是主键，只是普通业务列
    /// </summary>
    [Fact]
    public void SysDiffLog_实体标识列不是主键()
    {
        var property = typeof(SysDiffLog).GetProperty(nameof(SysDiffLog.EntityId));
        var column = property!.GetCustomAttribute<SugarColumn>();

        Assert.NotNull(column);
        Assert.Equal("Entity_Id", column!.ColumnName);
        Assert.False(column.IsPrimaryKey);
    }

    /// <summary>
    /// 差异日志的大文本列用 BigString 且不设长度上限
    /// </summary>
    [Theory]
    [InlineData(nameof(SysDiffLog.BeforeData), "Before_Data")]
    [InlineData(nameof(SysDiffLog.AfterData), "After_Data")]
    [InlineData(nameof(SysDiffLog.ChangedFields), "Changed_Fields")]
    public void SysDiffLog_大文本列用BigString且不设长度(string propertyName, string expectedColumnName)
    {
        var property = typeof(SysDiffLog).GetProperty(propertyName);
        var column = property!.GetCustomAttribute<SugarColumn>();

        Assert.NotNull(column);
        Assert.Equal(expectedColumnName, column!.ColumnName);
        Assert.Equal(StaticConfig.CodeFirst_BigString, column.ColumnDataType);
        Assert.Equal(0, column.Length);
    }

    /// <summary>
    /// 审计类型默认值为 EntityChange
    /// </summary>
    [Fact]
    public void SysDiffLog_审计类型默认值为EntityChange()
    {
        var entity = new SysDiffLog();

        Assert.Equal("EntityChange", entity.AuditType);
    }
}
