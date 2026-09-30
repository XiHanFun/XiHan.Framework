// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using SqlSugar;
using XiHan.Framework.Auditing.SqlSugar.Entities;

namespace XiHan.Framework.Auditing.SqlSugar.Tests;

/// <summary>
/// 日志表建表测试
/// </summary>
public class TableInitializationTests
{
    [Fact]
    public void 六类日志实体都能建出当月分表()
    {
        var databaseFile = NewDatabasePath();

        try
        {
            using var db = CreateClient(databaseFile);

            Type[] entityTypes =
            [
                typeof(SysAccessLog),
                typeof(SysApiLog),
                typeof(SysExceptionLog),
                typeof(SysLoginLog),
                typeof(SysOperationLog),
                typeof(SysDiffLog)
            ];

            foreach (var entityType in entityTypes)
            {
                db.CodeFirst.SplitTables().InitTables(entityType);
            }

            var tableNames = db.DbMaintenance.GetTableInfoList(false)
                .Select(table => table.Name)
                .ToList();

            Assert.Contains(tableNames, name => name.StartsWith("sys_access_log_", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(tableNames, name => name.StartsWith("sys_api_log_", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(tableNames, name => name.StartsWith("sys_exception_log_", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(tableNames, name => name.StartsWith("sys_login_log_", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(tableNames, name => name.StartsWith("sys_operation_log_", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(tableNames, name => name.StartsWith("sys_diff_log_", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            DeleteDatabase(databaseFile);
        }
    }

    /// <summary>
    /// 分表名为创建时间所在月的首日
    /// </summary>
    [Fact]
    public void 建出的分表名是当月首日()
    {
        var databaseFile = NewDatabasePath();

        try
        {
            using var db = CreateClient(databaseFile);

            db.CodeFirst.SplitTables().InitTables(typeof(SysOperationLog));

            // 初始分表名由 SqlSugar 用数据库时钟推出（SQLite 为本地时间），期望值取同一来源
            var expected = $"sys_operation_log_{db.GetDate():yyyyMM}01";
            var tableNames = db.DbMaintenance.GetTableInfoList(false)
                .Select(table => table.Name)
                .ToList();

            Assert.Contains(expected, tableNames);
        }
        finally
        {
            DeleteDatabase(databaseFile);
        }
    }

    [Fact]
    public void 操作日志写入后能按时间区间查回()
    {
        var databaseFile = NewDatabasePath();

        try
        {
            using var db = CreateClient(databaseFile);

            db.CodeFirst.SplitTables().InitTables(typeof(SysOperationLog));

            var now = DateTimeOffset.UtcNow;
            var log = new SysOperationLog(now.Ticks)
            {
                CreatedTime = now,
                TraceId = "trace-1",
                Method = "POST",
                Path = "/Order",
                StatusCode = 200,
                ElapsedMilliseconds = 12
            };

            db.Insertable(log).SplitTable().ExecuteCommand();

            var found = db.Queryable<SysOperationLog>()
                .SplitTable(now.DateTime.AddDays(-1), now.DateTime.AddDays(1))
                .Where(item => item.TraceId == "trace-1")
                .ToList();

            Assert.Single(found);
            Assert.Equal("/Order", found[0].Path);
            Assert.Equal(200, found[0].StatusCode);
            Assert.Equal(12L, found[0].ElapsedMilliseconds);
        }
        finally
        {
            DeleteDatabase(databaseFile);
        }
    }

    /// <summary>
    /// 落在上月的日志按时间区间查询时命中上月分表
    /// </summary>
    [Fact]
    public void 跨月查询命中对应分表()
    {
        var databaseFile = NewDatabasePath();

        try
        {
            using var db = CreateClient(databaseFile);

            db.CodeFirst.SplitTables().InitTables(typeof(SysOperationLog));

            var lastMonth = DateTimeOffset.UtcNow.AddMonths(-1);

            var log = new SysOperationLog(lastMonth.Ticks)
            {
                CreatedTime = lastMonth,
                TraceId = "trace-last-month",
                Method = "GET",
                Path = "/Archive",
                StatusCode = 200
            };

            db.Insertable(log).SplitTable().ExecuteCommand();

            var found = db.Queryable<SysOperationLog>()
                .SplitTable(lastMonth.DateTime.AddDays(-1), DateTimeOffset.UtcNow.DateTime)
                .Where(item => item.TraceId == "trace-last-month")
                .ToList();

            Assert.Single(found);
            Assert.Equal("/Archive", found[0].Path);
        }
        finally
        {
            DeleteDatabase(databaseFile);
        }
    }

    private static string NewDatabasePath()
    {
        return Path.Combine(Path.GetTempPath(), $"xihan_auditing_{Guid.NewGuid():N}.db");
    }

    private static SqlSugarClient CreateClient(string databaseFile)
    {
        // 关闭连接池，避免用例结束后驱动仍持有临时库文件句柄。
        return new(new ConnectionConfig
        {
            ConnectionString = $"DataSource={databaseFile};Pooling=False",
            DbType = DbType.Sqlite,
            IsAutoCloseConnection = true
        });
    }

    private static void DeleteDatabase(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}
