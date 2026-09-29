// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Reflection;
using SqlSugar;
using XiHan.Framework.Upgrade.SqlSugar.Entities;
using XiHan.Framework.Upgrade.SqlSugar.Mapping;

namespace XiHan.Framework.Upgrade.SqlSugar.Tests;

/// <summary>
/// 升级实体与映射测试
/// </summary>
public class EntityMappingTests
{
    /// <summary>
    /// 版本状态表名符合约定
    /// </summary>
    [Fact]
    public void 版本状态表名符合约定()
    {
        var table = typeof(SysUpgradeVersion).GetCustomAttribute<SugarTable>();

        Assert.NotNull(table);
        Assert.Equal("sys_upgrade_version", table.TableName);
    }

    /// <summary>
    /// 迁移历史表名符合约定
    /// </summary>
    [Fact]
    public void 迁移历史表名符合约定()
    {
        var table = typeof(SysUpgradeMigrationHistory).GetCustomAttribute<SugarTable>();

        Assert.NotNull(table);
        Assert.Equal("sys_upgrade_migration_history", table.TableName);
    }

    /// <summary>
    /// 版本状态表在租户键上建唯一索引
    /// </summary>
    [Fact]
    public void 版本状态表在租户键上建唯一索引()
    {
        var index = typeof(SysUpgradeVersion).GetCustomAttribute<SugarIndexAttribute>();

        Assert.NotNull(index);
        Assert.True(index.IsUnique);
        Assert.True(index.IndexFields.ContainsKey(nameof(SysUpgradeVersion.TenantKey)));
    }

    /// <summary>
    /// 迁移历史表不带唯一索引
    /// </summary>
    [Fact]
    public void 迁移历史表不带唯一索引()
    {
        var index = typeof(SysUpgradeMigrationHistory).GetCustomAttribute<SugarIndexAttribute>();

        Assert.Null(index);
    }

    /// <summary>
    /// 租户键对宿主与租户分别生成不同格式
    /// </summary>
    [Fact]
    public void 租户键对宿主与租户分别生成不同格式()
    {
        Assert.Equal("host", UpgradeMapper.BuildTenantKey(null));
        Assert.Equal("tenant:7", UpgradeMapper.BuildTenantKey(7L));
    }

    /// <summary>
    /// 版本号规范化空白值为零版本
    /// </summary>
    [Fact]
    public void 版本号规范化空白值为零版本()
    {
        Assert.Equal("0.0.0", UpgradeMapper.NormalizeVersion(null));
        Assert.Equal("0.0.0", UpgradeMapper.NormalizeVersion("   "));
        Assert.Equal("1.2.3", UpgradeMapper.NormalizeVersion("  1.2.3  "));
    }

    /// <summary>
    /// 版本状态实体到模型的映射保留全部字段
    /// </summary>
    [Fact]
    public void 版本状态实体到模型的映射保留全部字段()
    {
        var entity = new SysUpgradeVersion(1001L)
        {
            TenantId = 7L,
            TenantKey = "tenant:7",
            AppVersion = "1.0.0",
            DbVersion = "1.0.0",
            MinSupportVersion = "0.9.0",
            IsUpgrading = true,
            UpgradeNode = "node-1",
            UpgradeStartTime = DateTimeOffset.UtcNow
        };

        var state = UpgradeMapper.ToState(entity);

        Assert.Equal(1001L, state.Id);
        Assert.Equal(7L, state.TenantId);
        Assert.Equal("1.0.0", state.AppVersion);
        Assert.Equal("1.0.0", state.DbVersion);
        Assert.Equal("0.9.0", state.MinSupportVersion);
        Assert.True(state.IsUpgrading);
        Assert.Equal("node-1", state.UpgradeNode);
    }

    /// <summary>
    /// 迁移历史实体到模型的映射保留全部字段
    /// </summary>
    [Fact]
    public void 迁移历史实体到模型的映射保留全部字段()
    {
        var executedTime = DateTimeOffset.UtcNow;
        var entity = new SysUpgradeMigrationHistory(2001L)
        {
            TenantId = 7L,
            TenantKey = "tenant:7",
            Version = "1.0.0",
            ScriptName = "0001.sql",
            ExecutedTime = executedTime,
            Success = true,
            NodeName = "node-1",
            ErrorMessage = null
        };

        var history = UpgradeMapper.ToHistory(entity);

        Assert.Equal(7L, history.TenantId);
        Assert.Equal("1.0.0", history.Version);
        Assert.Equal("0001.sql", history.ScriptName);
        Assert.Equal(executedTime, history.ExecutedTime);
        Assert.True(history.Success);
        Assert.Equal("node-1", history.NodeName);
        Assert.Null(history.ErrorMessage);
    }
}
