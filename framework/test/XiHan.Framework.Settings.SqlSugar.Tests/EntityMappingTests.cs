// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Reflection;
using SqlSugar;
using XiHan.Framework.Settings.SqlSugar.Entities;

namespace XiHan.Framework.Settings.SqlSugar.Tests;

/// <summary>
/// 设置值实体映射测试
/// </summary>
public class EntityMappingTests
{
    /// <summary>
    /// 表名为 sys_setting
    /// </summary>
    [Fact]
    public void SysSetting_表名为固定表名()
    {
        var table = typeof(SysSetting).GetCustomAttribute<SugarTable>();

        Assert.NotNull(table);
        Assert.Equal("sys_setting", table.TableName);
    }

    /// <summary>
    /// 设置名列使用 Pascal_Snake_Case 且非空
    /// </summary>
    [Fact]
    public void SysSetting_设置名列非空()
    {
        var property = typeof(SysSetting).GetProperty(nameof(SysSetting.SettingName));
        var column = property!.GetCustomAttribute<SugarColumn>();

        Assert.NotNull(column);
        Assert.Equal("Setting_Name", column.ColumnName);
        Assert.False(column.IsNullable);
    }

    /// <summary>
    /// 提供者名与提供者键列均为非空列
    /// </summary>
    [Fact]
    public void SysSetting_提供者名与提供者键均非空()
    {
        var providerName = typeof(SysSetting).GetProperty(nameof(SysSetting.ProviderName))!.GetCustomAttribute<SugarColumn>();
        var providerKey = typeof(SysSetting).GetProperty(nameof(SysSetting.ProviderKey))!.GetCustomAttribute<SugarColumn>();

        Assert.NotNull(providerName);
        Assert.False(providerName.IsNullable);
        Assert.Equal("Provider_Name", providerName.ColumnName);

        Assert.NotNull(providerKey);
        Assert.False(providerKey.IsNullable);
        Assert.Equal("Provider_Key", providerKey.ColumnName);
    }

    /// <summary>
    /// 设置值列使用大文本类型
    /// </summary>
    [Fact]
    public void SysSetting_设置值列使用大文本类型()
    {
        var column = typeof(SysSetting).GetProperty(nameof(SysSetting.SettingValue))!.GetCustomAttribute<SugarColumn>();

        Assert.NotNull(column);
        Assert.Equal("Setting_Value", column.ColumnName);
        Assert.Equal(StaticConfig.CodeFirst_BigString, column.ColumnDataType);
    }

    /// <summary>
    /// 声明了覆盖三列的唯一索引
    /// </summary>
    [Fact]
    public void SysSetting_声明了唯一索引()
    {
        var index = typeof(SysSetting).GetCustomAttribute<SugarIndexAttribute>();

        Assert.NotNull(index);
        Assert.True(index.IsUnique);
        Assert.Equal(3, index.IndexFields.Count);
        Assert.Contains(nameof(SysSetting.SettingName), index.IndexFields.Keys);
        Assert.Contains(nameof(SysSetting.ProviderName), index.IndexFields.Keys);
        Assert.Contains(nameof(SysSetting.ProviderKey), index.IndexFields.Keys);
    }

    /// <summary>
    /// 能建出 sys_setting 表
    /// </summary>
    [Fact]
    public void SysSetting_能建出表()
    {
        var databaseFile = Path.Combine(Path.GetTempPath(), $"xihan_settings_{Guid.NewGuid():N}.db");

        try
        {
            using var db = new SqlSugarClient(new ConnectionConfig
            {
                ConnectionString = $"DataSource={databaseFile};Pooling=False",
                DbType = DbType.Sqlite,
                IsAutoCloseConnection = true
            });

            db.CodeFirst.InitTables(typeof(SysSetting));

            var tableNames = db.DbMaintenance.GetTableInfoList(false).Select(table => table.Name).ToList();

            Assert.Contains("sys_setting", tableNames, StringComparer.OrdinalIgnoreCase);
        }
        finally
        {
            if (File.Exists(databaseFile))
            {
                File.Delete(databaseFile);
            }
        }
    }

    /// <summary>
    /// 相同键的第二次插入被唯一索引拒绝
    /// </summary>
    [Fact]
    public void SysSetting_相同键的第二次插入被唯一索引拒绝()
    {
        var databaseFile = Path.Combine(Path.GetTempPath(), $"xihan_settings_{Guid.NewGuid():N}.db");

        try
        {
            using var db = new SqlSugarClient(new ConnectionConfig
            {
                ConnectionString = $"DataSource={databaseFile};Pooling=False",
                DbType = DbType.Sqlite,
                IsAutoCloseConnection = true
            });

            db.CodeFirst.InitTables(typeof(SysSetting));

            db.Insertable(new SysSetting(1L)
            {
                SettingName = "App.PageSize",
                ProviderName = "G",
                ProviderKey = string.Empty,
                SettingValue = "20"
            }).ExecuteCommand();

            Assert.ThrowsAny<Exception>(() => db.Insertable(new SysSetting(2L)
            {
                SettingName = "App.PageSize",
                ProviderName = "G",
                ProviderKey = string.Empty,
                SettingValue = "30"
            }).ExecuteCommand());
        }
        finally
        {
            if (File.Exists(databaseFile))
            {
                File.Delete(databaseFile);
            }
        }
    }
}
