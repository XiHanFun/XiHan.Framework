// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Data.Common;
using Microsoft.Extensions.Logging;
using SqlSugar;
using XiHan.Framework.Persistence.Abstractions;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Persistence.SqlSugar;

/// <summary>
/// 从库的状态与读路由
/// </summary>
/// <remarks>
/// 只管本特性自己建的固定连接（默认连接与命名连接）；分片连接每个作用域现建，不探测。只在配了从库时才登记。每个作用域经 <see cref="ForScope"/> 拿一份自己的连接配置，上面只有此刻能读的从库
/// </remarks>
internal sealed partial class SqlSugarReplicaMonitor
{
    /// <summary>
    /// 单个从库探测的超时上限
    /// </summary>
    /// <remarks>
    /// 探测在启动时要等一轮，一个连不上的从库不该把启动拖到超时
    /// </remarks>
    private static readonly TimeSpan _probeTimeoutCeiling = TimeSpan.FromSeconds(5);

    /// <summary>
    /// 固定连接到它的从库状态
    /// </summary>
    /// <remarks>
    /// 按引用查：键就是特性建的那份配置
    /// </remarks>
    private readonly Dictionary<ConnectionConfig, ReplicaGroup> _groups = new(ReferenceEqualityComparer.Instance);

    /// <summary>
    /// 选项
    /// </summary>
    private readonly SqlSugarReplicaOptions _options;

    /// <summary>
    /// 时钟
    /// </summary>
    private readonly TimeProvider _clock;

    /// <summary>
    /// 日志
    /// </summary>
    private readonly ILogger _logger;

    /// <summary>
    /// 单个从库探测的超时
    /// </summary>
    /// <remarks>
    /// 短于探测周期的单轮超时，这样从库超时总是记在这个从库上
    /// </remarks>
    private readonly TimeSpan _probeTimeout;

    /// <summary>
    /// 创建
    /// </summary>
    /// <param name="connections">配了从库的固定连接</param>
    /// <param name="options">选项</param>
    /// <param name="clock">时钟（冷却窗口与探测超时都按它计时）</param>
    /// <param name="loggerFactory">日志工厂</param>
    public SqlSugarReplicaMonitor(IEnumerable<ConnectionConfig> connections, SqlSugarReplicaOptions options, TimeProvider clock, ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(connections);
        ArgumentNullException.ThrowIfNull(loggerFactory);
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _logger = loggerFactory.CreateLogger<SqlSugarReplicaMonitor>();
        _probeTimeout = TimeSpan.FromTicks(Math.Min(_probeTimeoutCeiling.Ticks, options.HealthCheckInterval.Ticks / 2));
        foreach (var connection in connections)
        {
            _groups[connection] = new ReplicaGroup(connection);
        }
    }

    /// <summary>
    /// 取这个作用域用的连接配置
    /// </summary>
    /// <remarks>
    /// 必须是一份副本：SqlSugar 读从库时把配置的 <c>ConnectionString</c> 改成从库的、读完再改回来，配置在作用域之间共用时，这期间别的作用域建的连接就连到了从库，写入落在从库上，它记下的「主库串」也可能变成从库的。副本上的从库是此刻能读的，一个都没有时为空，SqlSugar 不再做读写分离，读全部回到主库。没有从库的连接不在表里，原样返回，不多一次分配。这份列表所有作用域共用：SqlSugar 只读它，状态变化时整体替换
    /// </remarks>
    public ConnectionConfig ForScope(ConnectionConfig config)
    {
        if (!_groups.TryGetValue(config, out var group))
        {
            return config;
        }

        var copy = UtilMethods.CopyConfig(config);
        copy.SlaveConnectionConfigs = group.Routable;
        return copy;
    }

    /// <summary>
    /// 探测全部从库并更新路由
    /// </summary>
    /// <remarks>
    /// 探针用从库自己的连接串另建连接，只读状态、不写配置，碰不到主库和别的库。全部可读返回成功，否则返回第一个不可读的从库
    /// </remarks>
    public async ValueTask<Result> ProbeAsync(CancellationToken cancellationToken)
    {
        Error? problem = null;
        foreach (var group in _groups.Values)
        {
            var failures = await Task.WhenAll(group.Replicas.Select(replica => TryProbeAsync(group.DbType, replica.ConnectionString, cancellationToken))).ConfigureAwait(false);
            Apply(group, failures);
            problem ??= FirstProblem(group);
        }

        return problem is null ? Result.Success() : problem;
    }

    /// <summary>
    /// 健康条目的数据
    /// </summary>
    /// <remarks>
    /// 每个连接 <c>&lt;连接&gt;.replicas</c>、<c>&lt;连接&gt;.available</c>，每个从库 <c>&lt;连接&gt;.&lt;序号&gt;</c> 是状态，不可读时 <c>&lt;连接&gt;.&lt;序号&gt;.reason</c> 是异常类型。连接串是机密，不进这里
    /// </remarks>
    public Dictionary<string, object?> Data()
    {
        var data = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var group in _groups.Values)
        {
            var replicas = group.Replicas;
            data[$"{group.Name}.replicas"] = replicas.Length;
            data[$"{group.Name}.available"] = replicas.Count(static replica => replica.Status == ReplicaStatus.Available);
            for (var index = 0; index < replicas.Length; index++)
            {
                data[$"{group.Name}.{index}"] = StatusText(replicas[index].Status);
                if (replicas[index].Failure is { } failure)
                {
                    data[$"{group.Name}.{index}.reason"] = failure.FullName ?? failure.Name;
                }
            }
        }

        return data;
    }

    /// <summary>
    /// 探测一个从库，返回失败的异常，成功为空
    /// </summary>
    /// <remarks>
    /// 执行一条语句而不只是打开连接：连接池会把空闲连接直接交出来，从库已经宕了也打得开。自己打开原生连接拿驱动的异常，<c>IsValidConnection</c> 会把它吞掉。整体取消（停止）照常抛出
    /// </remarks>
    private async Task<Exception?> TryProbeAsync(DbType dbType, string connectionString, CancellationToken cancellationToken)
    {
        using var timeout = new CancellationTokenSource(_probeTimeout, _clock);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
        try
        {
            using var client = new SqlSugarClient(new ConnectionConfig { DbType = dbType, ConnectionString = connectionString, IsAutoCloseConnection = true });
            var connection = client.Ado.Connection;
            if (connection is DbConnection database)
            {
                await database.OpenAsync(linked.Token).ConfigureAwait(false);
                await using var command = database.CreateCommand();
                command.CommandText = ProbeSql(dbType);
                await command.ExecuteScalarAsync(linked.Token).ConfigureAwait(false);
            }
            else
            {
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = ProbeSql(dbType);
                command.ExecuteScalar();
            }

            return null;
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            return exception;
        }
    }

    /// <summary>
    /// 把一轮探测结果写成状态，再更新路由
    /// </summary>
    private void Apply(ReplicaGroup group, Exception?[] failures)
    {
        var now = _clock.GetUtcNow();
        var wasOnReplicas = group.Routable is not null;
        var next = new ReplicaState[group.Replicas.Length];
        for (var index = 0; index < next.Length; index++)
        {
            var current = group.Replicas[index];
            if (failures[index] is { } failure)
            {
                // 同一种失败只记一次 Warning，持续时记 Debug：每个周期一条带堆栈的 Warning 会淹没日志（ADR-0115）
                var type = failure.GetType();
                if (type == current.Failure)
                {
                    LogStillUnavailable(_logger, group.Name, index, failure);
                }
                else
                {
                    LogUnavailable(_logger, group.Name, index, failure);
                }

                next[index] = current with { Status = ReplicaStatus.Unavailable, FailedAt = now, Failure = type };
            }
            else if (current.FailedAt is { } failedAt && now - failedAt < _options.FailureCooldown)
            {
                // 探通了但还在冷却：刚恢复的从库可能还在抖动，也可能落后很多，先不放读流量。失败时刻不更新，冷却才走得完
                next[index] = current with { Status = ReplicaStatus.CoolingDown };
            }
            else
            {
                if (current.Status != ReplicaStatus.Available)
                {
                    LogRestored(_logger, group.Name, index);
                }

                next[index] = current with { Status = ReplicaStatus.Available, FailedAt = null, Failure = null };
            }
        }

        group.Publish(next);
        if (wasOnReplicas && group.Routable is null)
        {
            LogAllUnavailable(_logger, group.Name);
        }
        else if (!wasOnReplicas && group.Routable is not null)
        {
            LogBackOnReplicas(_logger, group.Name);
        }
    }

    /// <summary>
    /// 第一个不可读的从库构成的错误，全部可读为空
    /// </summary>
    private static Error? FirstProblem(ReplicaGroup group)
    {
        for (var index = 0; index < group.Replicas.Length; index++)
        {
            var replica = group.Replicas[index];
            if (replica.Status != ReplicaStatus.Available)
            {
                // 消息里只写异常类型：异常消息常带主机与端口，健康快照和日志的读者不一样（ADR-0115）
                var reason = replica.Failure is { } failure ? failure.FullName ?? failure.Name : string.Empty;
                return Error.Unavailable(PersistenceErrors.ReplicaUnavailable, PersistenceMessages.ReplicaUnavailable, MessageArguments.Of(("connection", group.Name), ("index", index), ("reason", reason)));
            }
        }

        return null;
    }

    /// <summary>
    /// 探测用的语句
    /// </summary>
    private static string ProbeSql(DbType dbType) => dbType is DbType.Oracle or DbType.Dm ? "SELECT 1 FROM DUAL" : "SELECT 1";

    /// <summary>
    /// 状态在健康数据里的写法
    /// </summary>
    private static string StatusText(ReplicaStatus status) => status switch
    {
        ReplicaStatus.Available => "available",
        ReplicaStatus.CoolingDown => "cooling-down",
        _ => "unavailable",
    };

    /// <summary>
    /// 从库不可用（这一种失败第一次）
    /// </summary>
    [LoggerMessage(Level = LogLevel.Warning, Message = "Replica {Index} of connection '{Connection}' is unavailable and no longer serves reads.")]
    private static partial void LogUnavailable(ILogger logger, string connection, int index, Exception exception);

    /// <summary>
    /// 从库持续不可用
    /// </summary>
    [LoggerMessage(Level = LogLevel.Debug, Message = "Replica {Index} of connection '{Connection}' is still unavailable.")]
    private static partial void LogStillUnavailable(ILogger logger, string connection, int index, Exception exception);

    /// <summary>
    /// 从库恢复
    /// </summary>
    [LoggerMessage(Level = LogLevel.Information, Message = "Replica {Index} of connection '{Connection}' serves reads again.")]
    private static partial void LogRestored(ILogger logger, string connection, int index);

    /// <summary>
    /// 一个连接的从库全部不可读，读回到主库
    /// </summary>
    [LoggerMessage(Level = LogLevel.Error, Message = "No replica of connection '{Connection}' can serve reads; reads go to the primary until one recovers.")]
    private static partial void LogAllUnavailable(ILogger logger, string connection);

    /// <summary>
    /// 读回到从库
    /// </summary>
    [LoggerMessage(Level = LogLevel.Information, Message = "Reads on connection '{Connection}' go to its replicas again.")]
    private static partial void LogBackOnReplicas(ILogger logger, string connection);

    /// <summary>
    /// 一个从库的状态
    /// </summary>
    private enum ReplicaStatus
    {
        /// <summary>
        /// 可读
        /// </summary>
        Available,

        /// <summary>
        /// 不可用
        /// </summary>
        Unavailable,

        /// <summary>
        /// 探通了，还在冷却
        /// </summary>
        CoolingDown,
    }

    /// <summary>
    /// 一个从库此刻的情况
    /// </summary>
    /// <param name="ConnectionString">连接串</param>
    /// <param name="HitRate">归一化之后的权重</param>
    /// <param name="Status">状态</param>
    /// <param name="FailedAt">最后一次探测失败的时刻；恢复后为空</param>
    /// <param name="Failure">最后一次失败的异常类型；恢复后为空</param>
    private sealed record ReplicaState(string ConnectionString, int HitRate, ReplicaStatus Status, DateTimeOffset? FailedAt, Type? Failure);

    /// <summary>
    /// 一个连接的从库
    /// </summary>
    private sealed class ReplicaGroup
    {
        /// <summary>
        /// 全部从库的情况
        /// </summary>
        /// <remarks>
        /// 探测完成后整体替换，读它不加锁
        /// </remarks>
        private volatile ReplicaState[] _replicas;

        /// <summary>
        /// 此刻能读的从库
        /// </summary>
        /// <remarks>
        /// 同样整体替换；一个都没有时为空
        /// </remarks>
        private volatile List<SlaveConnectionConfig>? _routable;

        /// <summary>
        /// 创建
        /// </summary>
        /// <remarks>
        /// 第一轮探测之前都算可读：启动时那轮探测完成才对外服务
        /// </remarks>
        public ReplicaGroup(ConnectionConfig config)
        {
            Name = SqlSugarReplicaRules.NameOf(config);
            DbType = config.DbType;
            _replicas = [.. config.SlaveConnectionConfigs.Select(static replica => new ReplicaState(replica.ConnectionString, replica.HitRate, ReplicaStatus.Available, null, null))];
            _routable = Routes(_replicas);
        }

        /// <summary>
        /// 连接名
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// 数据库类型
        /// </summary>
        public DbType DbType { get; }

        /// <summary>
        /// 全部从库的情况
        /// </summary>
        public ReplicaState[] Replicas => _replicas;

        /// <summary>
        /// 此刻能读的从库
        /// </summary>
        public List<SlaveConnectionConfig>? Routable => _routable;

        /// <summary>
        /// 换上新的情况
        /// </summary>
        public void Publish(ReplicaState[] replicas)
        {
            _replicas = replicas;
            _routable = Routes(replicas);
        }

        /// <summary>
        /// 能读的从库
        /// </summary>
        private static List<SlaveConnectionConfig>? Routes(ReplicaState[] replicas)
        {
            List<SlaveConnectionConfig> routes = [.. replicas.Where(static replica => replica.Status == ReplicaStatus.Available).Select(static replica => new SlaveConnectionConfig { ConnectionString = replica.ConnectionString, HitRate = replica.HitRate })];
            return routes.Count > 0 ? routes : null;
        }
    }
}
