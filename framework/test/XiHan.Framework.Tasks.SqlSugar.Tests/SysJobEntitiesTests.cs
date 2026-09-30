// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using SqlSugar;
using XiHan.Framework.Tasks.SqlSugar.Entities;

namespace XiHan.Framework.Tasks.SqlSugar.Tests;

/// <summary>
/// 定时任务实体建表测试
/// </summary>
public class SysJobEntitiesTests
{
    /// <summary>
    /// 任务实例表与执行历史表及索引能建出来
    /// </summary>
    [Fact]
    public void 任务实例表与执行历史表及索引能建出来()
    {
        var databaseFile = Path.Combine(Path.GetTempPath(), $"xihan_tasks_{Guid.NewGuid():N}.db");

        try
        {
            using var db = new SqlSugarClient(new ConnectionConfig
            {
                // 关闭连接池，用例结束后驱动不再持有临时库文件句柄
                ConnectionString = $"DataSource={databaseFile};Pooling=False",
                DbType = DbType.Sqlite,
                IsAutoCloseConnection = true
            });

            db.CodeFirst.InitTables(typeof(SysJobInstance), typeof(SysJobHistory));

            var tableNames = db.DbMaintenance.GetTableInfoList(false)
                .Select(table => table.Name)
                .ToList();

            Assert.Contains(tableNames, name => string.Equals(name, "sys_job_instance", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(tableNames, name => string.Equals(name, "sys_job_history", StringComparison.OrdinalIgnoreCase));
            Assert.True(db.DbMaintenance.IsAnyIndex("idx_sys_job_instance_name_status"));
            Assert.True(db.DbMaintenance.IsAnyIndex("idx_sys_job_history_name_started"));
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
