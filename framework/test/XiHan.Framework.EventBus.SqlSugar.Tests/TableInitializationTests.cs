// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using SqlSugar;
using XiHan.Framework.EventBus.Abstractions.Distributed;
using XiHan.Framework.EventBus.SqlSugar.Entities;
using XiHan.Framework.EventBus.SqlSugar.Mapping;

namespace XiHan.Framework.EventBus.SqlSugar.Tests;

/// <summary>
/// 收发件箱建表测试
/// </summary>
public class TableInitializationTests
{
    /// <summary>
    /// 发件箱表能建出来
    /// </summary>
    [Fact]
    public void 发件箱表能建出来()
    {
        var databaseFile = Path.Combine(Path.GetTempPath(), $"xihan_outbox_{Guid.NewGuid():N}.db");

        try
        {
            using var db = CreateClient(databaseFile);

            db.CodeFirst.InitTables(typeof(SysEventOutbox));

            var tableNames = db.DbMaintenance.GetTableInfoList(false)
                .Select(table => table.Name)
                .ToList();

            Assert.Contains(tableNames, name => string.Equals(name, "sys_event_outbox", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            DeleteQuietly(databaseFile);
        }
    }

    /// <summary>
    /// 事件数据以二进制往返后逐字节相等
    /// </summary>
    [Fact]
    public void 事件数据以二进制往返后逐字节相等()
    {
        var databaseFile = Path.Combine(Path.GetTempPath(), $"xihan_outbox_{Guid.NewGuid():N}.db");

        try
        {
            using var db = CreateClient(databaseFile);

            db.CodeFirst.InitTables(typeof(SysEventOutbox));

            byte[] data = [0, 1, 127, 128, 255];
            var info = new OutgoingEventInfo(Guid.NewGuid(), "Order.Created", data, DateTime.UtcNow);
            info.SetCorrelationId("corr-db");

            db.Insertable(EventOutboxMapper.ToEntity(info)).ExecuteCommand();

            var stored = db.Queryable<SysEventOutbox>()
                .Where(item => item.BasicId == info.Id)
                .First();

            Assert.NotNull(stored);
            Assert.Equal(data, stored.EventData);

            var restored = EventOutboxMapper.ToEventInfo(stored);

            Assert.Equal(info.Id, restored.Id);
            Assert.Equal("Order.Created", restored.EventName);
            Assert.Equal("corr-db", restored.GetCorrelationId());
            Assert.Equal(SysEventOutbox.StatusPending, stored.Status);
        }
        finally
        {
            DeleteQuietly(databaseFile);
        }
    }

    /// <summary>
    /// 收件箱表能建出来
    /// </summary>
    [Fact]
    public void 收件箱表能建出来()
    {
        var databaseFile = Path.Combine(Path.GetTempPath(), $"xihan_inbox_{Guid.NewGuid():N}.db");

        try
        {
            using var db = CreateClient(databaseFile);

            db.CodeFirst.InitTables(typeof(SysEventInbox));

            var tableNames = db.DbMaintenance.GetTableInfoList(false)
                .Select(table => table.Name)
                .ToList();

            Assert.Contains(tableNames, name => string.Equals(name, "sys_event_inbox", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            DeleteQuietly(databaseFile);
        }
    }

    /// <summary>
    /// 重复的去重键被唯一索引拦下
    /// </summary>
    [Fact]
    public void 重复的去重键被唯一索引拦下()
    {
        var databaseFile = Path.Combine(Path.GetTempPath(), $"xihan_inbox_{Guid.NewGuid():N}.db");

        try
        {
            using var db = CreateClient(databaseFile);

            db.CodeFirst.InitTables(typeof(SysEventInbox));

            db.Insertable(NewInboxEntity("dup-key")).ExecuteCommand();

            Assert.ThrowsAny<Exception>(() => db.Insertable(NewInboxEntity("dup-key")).ExecuteCommand());
            Assert.Equal(1, db.Queryable<SysEventInbox>().Count());
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

    private static SysEventInbox NewInboxEntity(string dedupKey)
    {
        return new SysEventInbox(Guid.NewGuid())
        {
            MessageId = dedupKey,
            DedupKey = dedupKey,
            EventName = "Order.Paid",
            EventData = [1],
            CreatedTime = DateTimeOffset.UtcNow,
            Status = SysEventInbox.StatusPending
        };
    }
}
