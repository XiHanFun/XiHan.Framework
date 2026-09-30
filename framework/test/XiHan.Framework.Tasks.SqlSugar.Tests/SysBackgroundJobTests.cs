// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using SqlSugar;
using XiHan.Framework.Tasks.SqlSugar.Entities;

namespace XiHan.Framework.Tasks.SqlSugar.Tests;

/// <summary>
/// 后台作业实体建表测试
/// </summary>
public class SysBackgroundJobTests
{
    /// <summary>
    /// 后台作业表与索引能建出来
    /// </summary>
    [Fact]
    public void 后台作业表与索引能建出来()
    {
        var databaseFile = Path.Combine(Path.GetTempPath(), $"xihan_tasks_{Guid.NewGuid():N}.db");

        try
        {
            using var db = CreateClient(databaseFile);

            db.CodeFirst.InitTables(typeof(SysBackgroundJob));

            var tableNames = db.DbMaintenance.GetTableInfoList(false)
                .Select(table => table.Name)
                .ToList();

            Assert.Contains(tableNames, name => string.Equals(name, "sys_background_job", StringComparison.OrdinalIgnoreCase));
            Assert.True(db.DbMaintenance.IsAnyIndex("idx_sys_background_job_waiting"));
            Assert.True(db.DbMaintenance.IsAnyIndex("idx_sys_background_job_claim_token"));
        }
        finally
        {
            DeleteQuietly(databaseFile);
        }
    }

    private static SqlSugarClient CreateClient(string databaseFile)
    {
        return new SqlSugarClient(new ConnectionConfig
        {
            // 关闭连接池，用例结束后驱动不再持有临时库文件句柄
            ConnectionString = $"DataSource={databaseFile};Pooling=False",
            DbType = DbType.Sqlite,
            IsAutoCloseConnection = true
        });
    }

    private static void DeleteQuietly(string databaseFile)
    {
        if (File.Exists(databaseFile))
        {
            File.Delete(databaseFile);
        }
    }
}
