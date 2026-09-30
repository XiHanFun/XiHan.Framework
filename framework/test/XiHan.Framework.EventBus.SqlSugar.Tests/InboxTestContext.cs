// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Logging.Abstractions;
using SqlSugar;
using XiHan.Framework.EventBus.SqlSugar.Entities;
using XiHan.Framework.EventBus.SqlSugar.Inbox;
using XiHan.Framework.EventBus.SqlSugar.Options;
using XiHan.Framework.MultiTenancy.Abstractions;

namespace XiHan.Framework.EventBus.SqlSugar.Tests;

/// <summary>
/// 收件箱测试夹具，提供宿主主库与一个租户库两个临时 SQLite 库
/// </summary>
internal sealed class InboxTestContext : IDisposable
{
    /// <summary>
    /// 宿主主库的连接配置标识
    /// </summary>
    public const string MainConfigId = "Default";

    /// <summary>
    /// 租户库的连接配置标识
    /// </summary>
    public const string TenantConfigId = "Tenant_1001";

    /// <summary>
    /// 租户标识
    /// </summary>
    public const long TenantId = 1001;

    private readonly List<string> _databaseFiles = [];

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="claimTimeout">领取超时</param>
    /// <param name="retentionPeriod">已完结记录的保留期</param>
    public InboxTestContext(TimeSpan? claimTimeout = null, TimeSpan? retentionPeriod = null)
    {
        string[] configIds = [MainConfigId, TenantConfigId];

        foreach (var configId in configIds)
        {
            var databaseFile = Path.Combine(Path.GetTempPath(), $"xihan_inbox_{Guid.NewGuid():N}.db");
            _databaseFiles.Add(databaseFile);

            var client = new SqlSugarClient(new ConnectionConfig
            {
                // 禁用连接池
                ConnectionString = $"DataSource={databaseFile};Pooling=False",
                DbType = DbType.Sqlite,
                IsAutoCloseConnection = true
            });

            client.CodeFirst.InitTables(typeof(SysEventInbox));
            Clients[configId] = client;
        }

        CurrentTenant = new FakeCurrentTenant();

        Resolver = new StubClientResolver(Clients, [MainConfigId], MainConfigId)
        {
            CurrentConfigIdSelector = () => CurrentTenant.Id is null ? MainConfigId : TenantConfigId
        };

        Inbox = new SqlSugarEventInbox(
            Resolver,
            CurrentTenant,
            Microsoft.Extensions.Options.Options.Create(new XiHanSqlSugarEventBoxOptions
            {
                ClaimTimeout = claimTimeout ?? TimeSpan.FromMinutes(5),
                InboxRetentionPeriod = retentionPeriod ?? TimeSpan.FromDays(7)
            }),
            NullLogger<SqlSugarEventInbox>.Instance);
    }

    /// <summary>
    /// 各库的客户端
    /// </summary>
    public Dictionary<string, SqlSugarClient> Clients { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// 可编程的客户端解析器，当前库随当前租户切换
    /// </summary>
    public StubClientResolver Resolver { get; }

    /// <summary>
    /// 当前租户
    /// </summary>
    public FakeCurrentTenant CurrentTenant { get; }

    /// <summary>
    /// 被测收件箱
    /// </summary>
    public SqlSugarEventInbox Inbox { get; }

    /// <summary>
    /// 宿主主库客户端
    /// </summary>
    public SqlSugarClient Client
    {
        get { return Clients[MainConfigId]; }
    }

    /// <summary>
    /// 租户库客户端
    /// </summary>
    public SqlSugarClient TenantClient
    {
        get { return Clients[TenantConfigId]; }
    }

    /// <summary>
    /// 释放客户端并删除临时库文件
    /// </summary>
    public void Dispose()
    {
        foreach (var client in Clients.Values)
        {
            client.Dispose();
        }

        foreach (var databaseFile in _databaseFiles)
        {
            if (File.Exists(databaseFile))
            {
                File.Delete(databaseFile);
            }
        }
    }
}

/// <summary>
/// 测试用当前租户，切换后在释放时恢复原值
/// </summary>
internal sealed class FakeCurrentTenant : ICurrentTenant
{
    /// <summary>
    /// 当前租户是否可用
    /// </summary>
    public bool IsAvailable => Id.HasValue;

    /// <summary>
    /// 当前租户标识
    /// </summary>
    public long? Id { get; private set; }

    /// <summary>
    /// 当前租户名称
    /// </summary>
    public string? Name { get; private set; }

    /// <summary>
    /// 临时切换当前租户
    /// </summary>
    /// <param name="id">租户标识，为空表示无租户</param>
    /// <param name="name">租户名称</param>
    /// <returns>释放时恢复原租户的对象</returns>
    public IDisposable Change(long? id, string? name = null)
    {
        var restorer = new TenantRestorer(this, Id, Name);

        Id = id;
        Name = name;

        return restorer;
    }

    /// <summary>
    /// 释放时把租户恢复为切换前的值
    /// </summary>
    private sealed class TenantRestorer : IDisposable
    {
        private readonly FakeCurrentTenant _owner;
        private readonly long? _id;
        private readonly string? _name;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="owner">所属的当前租户</param>
        /// <param name="id">切换前的租户标识</param>
        /// <param name="name">切换前的租户名称</param>
        public TenantRestorer(FakeCurrentTenant owner, long? id, string? name)
        {
            _owner = owner;
            _id = id;
            _name = name;
        }

        /// <summary>
        /// 恢复切换前的租户
        /// </summary>
        public void Dispose()
        {
            _owner.Id = _id;
            _owner.Name = _name;
        }
    }
}
