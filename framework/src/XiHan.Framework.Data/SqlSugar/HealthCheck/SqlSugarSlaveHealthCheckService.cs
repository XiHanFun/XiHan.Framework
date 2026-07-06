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
/// <item>异步不阻塞启动，探测异常一律吞掉不影响主业务（fail-safe）。</item>
/// </list>
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

            // 首次记录原始权重快照（此时已被 NormalizeSlaveHitRates 归一化过）
            if (!_originalWeights.TryGetValue(configId, out var originalWeights))
            {
                originalWeights = slaves.Select(s => s.HitRate).ToList();
                _originalWeights[configId] = originalWeights;
            }

            for (var i = 0; i < slaves.Count; i++)
            {
                ProbeSlave(configId, i, liveConfig!.DbType, slaves[i], originalWeights);
            }
        }
    }

    private void ProbeSlave(string configId, int index, DbType dbType, SlaveConnectionConfig slave, List<int> originalWeights)
    {
        var key = $"{configId}#{index}";
        var alive = TestConnection(dbType, slave.ConnectionString);

        if (!alive)
        {
            if (slave.HitRate != 0)
            {
                _logger.LogWarning("SqlSugar 从库不可用，已摘除读流量：ConfigId={ConfigId} Slave#{Index}", configId, index);
            }

            slave.HitRate = 0;
            _lastFailureUtc[key] = DateTimeOffset.UtcNow;
            return;
        }

        // 冷却窗口内即便恢复也暂不回填，避免抖动
        if (_lastFailureUtc.TryGetValue(key, out var failedAt) &&
            (DateTimeOffset.UtcNow - failedAt).TotalSeconds < _options.SlaveFailureCooldownSeconds)
        {
            return;
        }

        if (index >= originalWeights.Count)
        {
            return;
        }

        var target = originalWeights[index];
        if (slave.HitRate != target)
        {
            slave.HitRate = target;
            _lastFailureUtc.Remove(key);
            _logger.LogInformation("SqlSugar 从库已恢复，回填读权重：ConfigId={ConfigId} Slave#{Index} HitRate={HitRate}",
                configId, index, target);
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
