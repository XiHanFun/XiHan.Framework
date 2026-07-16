#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:SqlSugarSlaveHealthCheckService
// Guid:5c1e7a42-9b0d-4f3e-8a6c-1d2f3b4c5e60
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/07/06 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SqlSugar;
using XiHan.Framework.Data.SqlSugar.Options;

namespace XiHan.Framework.Data.SqlSugar.HealthCheck;

/// <summary>
/// SqlSugar 从库健康探针（现成探针，默认关闭）
/// </summary>
/// <remarks>
/// 定期探测各连接的从库连通性：不可用的从库把 <c>HitRate</c> 摘为 0（不再分担读流量），
/// 恢复后按故障冷却窗口回填原始权重。相较粉丝 demo 的做法，本实现：
/// <list type="bullet">
/// <item>用注入的单例实例状态替代 <c>static Dictionary</c>，多实例/单测互不干扰；</item>
/// <item>后台周期性复探（非仅启动一次），死而复活的从库能自动回归；</item>
/// <item>异步不阻塞启动，探测异常一律吞掉不影响主业务（fail-safe）；</item>
/// <item><b>全灭降级回主库</b>：SqlSugar 的主从判定只看 <c>SlaveConnectionConfigs</c> 列表非空、不看权重——
///       若把全部从库权重摘为 0（单从库部署宕机即触发），读选路会对空候选集执行 <c>Random.Next(1, 0)</c>
///       抛 ArgumentOutOfRangeException，主库健康却全站读崩溃。因此本探针维持不变量：
///       <b>列表非空则绝不允许全零权重</b>——全灭（含存活从库全在冷却期的过渡窗口）时把各从库槽位的
///       连接串原子改写为主库快照并回填权重，读流量物理降级回主库；从库恢复后自动还原。</item>
/// </list>
/// 实现约束（勿破坏）：各请求上下文经 CopyConfig 共享同一批 <c>SlaveConnectionConfig</c> 实例（列表按引用复制），
/// 探针只做字段原子赋值（<c>HitRate</c> int 写 / <c>ConnectionString</c> 引用写）即可全局生效；
/// <b>严禁</b>对共享列表 Clear/Add/Remove（与并发读的 Where 枚举竞态），也不能用 <c>Ado.IsDisableMasterSlaveSeparation</c>
/// （那是各上下文 AdoProvider 实例属性，探针改不到存量请求上下文）。
/// 仅当 <see cref="XiHanSqlSugarCoreOptions.EnableSlaveHealthCheck"/> 为 true 时工作，否则直接空转退出。
/// 注：仅覆盖 appsettings 静态声明的连接；运行时动态注册的租户连接不在探测范围内。
/// </remarks>
public sealed class SqlSugarSlaveHealthCheckService : BackgroundService
{
    private readonly SqlSugarScope _sqlSugarScope;
    private readonly XiHanSqlSugarCoreOptions _options;
    private readonly ILogger<SqlSugarSlaveHealthCheckService> _logger;

    // ConfigId -> 各从库原始权重快照（首次探测时记录，用于恢复）
    private readonly Dictionary<string, List<int>> _originalWeights = new(StringComparer.OrdinalIgnoreCase);

    // ConfigId -> 各从库原始连接串快照（降级会把槽位连接串改写为主库，探测与还原都必须用原始串）
    private readonly Dictionary<string, List<string>> _originalSlaveConnections = new(StringComparer.OrdinalIgnoreCase);

    // ConfigId -> 主库连接串快照（降级目标；探针上下文的 ConnectionConfig 是私有副本，
    // 其 ConnectionString 不会被其他请求上下文的主从切换改写，首见快照即稳定）
    private readonly Dictionary<string, string> _masterConnections = new(StringComparer.OrdinalIgnoreCase);

    // 处于「全灭降级回主库」状态的 ConfigId（用于状态迁移日志去抖）
    private readonly HashSet<string> _degradedConfigs = new(StringComparer.OrdinalIgnoreCase);

    // "ConfigId#index" -> 最近一次探测失败的时间（用于冷却窗口）
    private readonly Dictionary<string, DateTimeOffset> _lastFailureUtc = new(StringComparer.Ordinal);

    /// <summary>
    /// 构造函数
    /// </summary>
    public SqlSugarSlaveHealthCheckService(
        SqlSugarScope sqlSugarScope,
        IOptions<XiHanSqlSugarCoreOptions> options,
        ILogger<SqlSugarSlaveHealthCheckService> logger)
    {
        _sqlSugarScope = sqlSugarScope;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.EnableSlaveHealthCheck)
        {
            return;
        }

        var seconds = Math.Max(5, _options.SlaveHealthCheckIntervalSeconds);
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(seconds));

        _logger.LogInformation("SqlSugar 从库健康探针已启动，探测周期 {Seconds}s，故障冷却 {Cooldown}s。",
            seconds, _options.SlaveFailureCooldownSeconds);

        try
        {
            do
            {
                ProbeOnce();
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException)
        {
            // 正常停机
        }
    }

    /// <summary>
    /// 执行一轮探测
    /// </summary>
    private void ProbeOnce()
    {
        foreach (var connConfig in _options.ConnectionConfigs)
        {
            var configId = connConfig.ConfigId;
            if (string.IsNullOrWhiteSpace(configId))
            {
                continue;
            }

            SqlSugarScopeProvider provider;
            try
            {
                provider = _sqlSugarScope.GetConnectionScope(configId);
            }
            catch
            {
                continue;
            }

            var liveConfig = provider.CurrentConnectionConfig;
            var slaves = liveConfig?.SlaveConnectionConfigs;
            if (slaves is null || slaves.Count == 0)
            {
                continue;
            }

            // 首次快照：原始权重（已被 NormalizeSlaveHitRates 归一化）、原始从库连接串、主库连接串
            if (!_originalWeights.TryGetValue(configId, out var originalWeights))
            {
                originalWeights = slaves.Select(s => s.HitRate).ToList();
                _originalWeights[configId] = originalWeights;
                _originalSlaveConnections[configId] = slaves.Select(s => s.ConnectionString).ToList();
                _masterConnections[configId] = liveConfig!.ConnectionString;
            }

            var originalConnections = _originalSlaveConnections[configId];
            var masterConnection = _masterConnections[configId];

            // 先对全部从库完成探测再统一应用结果——探测必须打【原始连接串】：
            // 降级模式下 slave.ConnectionString 已被改写为主库，探当前串会把「主库活着」误判成「从库恢复」
            var aliveFlags = new bool[slaves.Count];
            for (var i = 0; i < slaves.Count; i++)
            {
                aliveFlags[i] = i < originalConnections.Count && TestConnection(liveConfig!.DbType, originalConnections[i]);
            }

            ApplyProbeResults(configId, slaves, aliveFlags, originalWeights, originalConnections, masterConnection);
        }
    }

    /// <summary>
    /// 应用一轮探测结果，维持出口不变量：从库列表非空则任一时刻都<b>绝不出现全零权重</b>
    /// </summary>
    /// <remarks>
    /// 关键并发约束：SqlSugar 读选路对空候选集（全零权重）执行 <c>Random.Next(1,0)</c> 抛异常，
    /// 且它在另一线程按 <c>Where(HitRate&gt;0)</c> 即时快照——因此本方法<b>先算出本轮终态、再落字段</b>，
    /// 且<b>正权重先写、零权重后写</b>：归纳保证进入时至少一个正权重、退出时（含降级兜底）至少一个正权重，
    /// 全程读侧永不观测到全零。所有写均为字段原子赋值（int/引用），绝不对共享列表做结构性修改。
    /// </remarks>
    private void ApplyProbeResults(
        string configId,
        List<SlaveConnectionConfig> slaves,
        bool[] aliveFlags,
        List<int> originalWeights,
        List<string> originalConnections,
        string masterConnection)
    {
        var now = DateTimeOffset.UtcNow;
        var count = Math.Min(slaves.Count, Math.Min(originalWeights.Count, originalConnections.Count));
        var wasDegraded = _degradedConfigs.Contains(configId);

        // 阶段一：计算每个槽位的终态（连接串 + 权重），并顺带维护失败时间戳/冷却，均不触碰会被读侧观测的 HitRate
        var targetConn = new string[count];
        var targetHitRate = new int[count];
        for (var i = 0; i < count; i++)
        {
            var key = $"{configId}#{i}";
            targetConn[i] = originalConnections[i];

            if (!aliveFlags[i])
            {
                if (slaves[i].HitRate != 0 && !wasDegraded)
                {
                    _logger.LogWarning("SqlSugar 从库不可用，已摘除读流量：ConfigId={ConfigId} Slave#{Index}", configId, i);
                }
                targetHitRate[i] = 0;
                _lastFailureUtc[key] = now;
                continue;
            }

            // 存活但仍在故障冷却窗口内：暂不信任（权重 0），保留原失败时间戳让冷却自然流逝（不重置，否则永不恢复）
            if (_lastFailureUtc.TryGetValue(key, out var failedAt) &&
                (now - failedAt).TotalSeconds < _options.SlaveFailureCooldownSeconds)
            {
                targetHitRate[i] = 0;
                continue;
            }

            // 存活且已过冷却 → 目标为原始权重
            targetHitRate[i] = originalWeights[i];
            _lastFailureUtc.Remove(key);
        }

        // 出口不变量：终态若全零（全灭，或存活从库全处冷却期），整体降级回主库——槽位连接串改写为主库、权重回填 max(1,原权重)
        var degrade = count > 0 && targetHitRate.All(rate => rate == 0);
        if (degrade)
        {
            for (var i = 0; i < count; i++)
            {
                targetConn[i] = masterConnection;
                targetHitRate[i] = Math.Max(1, originalWeights[i]);
            }
        }

        // 阶段二：先落全部「正权重」槽位（连接串先于权重），再落「零权重」槽位——
        // 归纳前提是进入时已有正权重，如此任一时刻都至少存在一个正权重，读侧永不见全零
        for (var i = 0; i < count; i++)
        {
            if (targetHitRate[i] <= 0)
            {
                continue;
            }

            if (!string.Equals(slaves[i].ConnectionString, targetConn[i], StringComparison.Ordinal))
            {
                slaves[i].ConnectionString = targetConn[i];
            }
            if (slaves[i].HitRate != targetHitRate[i])
            {
                slaves[i].HitRate = targetHitRate[i];
            }
        }
        for (var i = 0; i < count; i++)
        {
            // 零权重槽位仅需归零：HitRate=0 已被读侧 Where(HitRate>0) 排除，其连接串无关紧要，不写以减少抖动
            if (targetHitRate[i] <= 0 && slaves[i].HitRate != 0)
            {
                slaves[i].HitRate = 0;
            }
        }

        // 降级状态迁移日志（去抖：仅在进入/解除时各记一次）
        if (degrade)
        {
            if (_degradedConfigs.Add(configId))
            {
                _logger.LogError("SqlSugar 全部从库不可用：ConfigId={ConfigId}，读流量已整体降级回主库（从库槽位连接串已改写为主库快照），从库恢复并过冷却窗口后自动还原。", configId);
            }
        }
        else if (_degradedConfigs.Remove(configId))
        {
            _logger.LogInformation("SqlSugar 从库降级解除：ConfigId={ConfigId}，存活从库已还原读流量。", configId);
        }
    }

    /// <summary>
    /// 探测单个从库连通性（独立短连接，探测失败一律视为不可用）
    /// </summary>
    private bool TestConnection(DbType dbType, string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return false;
        }

        try
        {
            using var probe = new SqlSugarClient(new ConnectionConfig
            {
                ConnectionString = connectionString,
                DbType = dbType,
                IsAutoCloseConnection = true,
                InitKeyType = InitKeyType.Attribute
            });
            return probe.Ado.IsValidConnection();
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "SqlSugar 从库探测异常，视为不可用。");
            return false;
        }
    }
}
