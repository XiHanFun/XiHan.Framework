// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using XiHan.Framework.Data.SqlSugar.Clients;
using XiHan.Framework.MultiTenancy.Abstractions;
using XiHan.Framework.Traffic.GrayRouting.Abstractions;
using XiHan.Framework.Traffic.GrayRouting.Models;
using XiHan.Framework.Traffic.SqlSugar.Entities;
using XiHan.Framework.Traffic.SqlSugar.Mapping;
using XiHan.Framework.Traffic.SqlSugar.Options;
using XiHan.Framework.Uow;
using XiHan.Framework.Uow.Options;

namespace XiHan.Framework.Traffic.SqlSugar.Repositories;

/// <summary>
/// 灰度规则 SqlSugar 只读仓储
/// </summary>
/// <remarks>
/// 只负责查询与缓存，不提供写方法；规则的增删改由应用层直接对 sys_gray_rule 表操作。
/// 缓存到期后同一时刻只有一个调用查库；查库失败时保留上次成功加载的规则并退避重试。
/// </remarks>
public class SqlSugarGrayRuleRepository : IGrayRuleRepository
{
    private static readonly TimeSpan MaxFailureBackoff = TimeSpan.FromSeconds(5);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly XiHanTrafficSqlSugarOptions _options;
    private readonly ILogger<SqlSugarGrayRuleRepository> _logger;
    private readonly TimeProvider _timeProvider;
    private readonly SemaphoreSlim _refreshGate = new(1, 1);

    private volatile Dictionary<string, GrayRule> _cache = new(StringComparer.Ordinal);
    private long _lastRefreshTicks;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="scopeFactory">服务范围工厂，用于按需解析 Scoped 的客户端解析器</param>
    /// <param name="options">缓存刷新配置</param>
    /// <param name="logger">日志记录器</param>
    /// <param name="timeProvider">时间提供器</param>
    public SqlSugarGrayRuleRepository(
        IServiceScopeFactory scopeFactory,
        IOptions<XiHanTrafficSqlSugarOptions> options,
        ILogger<SqlSugarGrayRuleRepository> logger,
        TimeProvider timeProvider)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
        _timeProvider = timeProvider;
    }

    /// <summary>
    /// 获取所有启用的灰度规则
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>规则列表</returns>
    public async Task<List<IGrayRule>> GetEnabledRulesAsync(CancellationToken cancellationToken = default)
    {
        await EnsureFreshAsync(cancellationToken);

        return [.. _cache.Values.Where(rule => rule.IsEnabled)];
    }

    /// <summary>
    /// 根据规则标识获取规则
    /// </summary>
    /// <param name="ruleId">规则标识</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>规则</returns>
    public async Task<IGrayRule?> GetRuleByIdAsync(string ruleId, CancellationToken cancellationToken = default)
    {
        await EnsureFreshAsync(cancellationToken);

        return _cache.GetValueOrDefault(ruleId);
    }

    /// <summary>
    /// 强制从数据库重新加载全部规则
    /// </summary>
    /// <remarks>
    /// 加载期间当前租户切换为平台（0 号租户），规则从平台布局的库读取。
    /// 加载失败时保留原缓存并抛出异常。
    /// </remarks>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        await _refreshGate.WaitAsync(cancellationToken);
        try
        {
            await LoadAsync(cancellationToken);
        }
        finally
        {
            _refreshGate.Release();
        }
    }

    /// <summary>
    /// 缓存到期时刷新，未到期直接返回；刷新失败时保留旧缓存且不向外抛出
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    private async Task EnsureFreshAsync(CancellationToken cancellationToken)
    {
        if (!IsExpired())
        {
            return;
        }

        await _refreshGate.WaitAsync(cancellationToken);
        try
        {
            if (!IsExpired())
            {
                return;
            }

            try
            {
                await LoadAsync(cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // 失败已在加载方法中记录并退避，读取方继续使用旧缓存
            }
        }
        finally
        {
            _refreshGate.Release();
        }
    }

    /// <summary>
    /// 从数据库加载全部规则并替换缓存，调用方须已持有刷新闸门
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        try
        {
            List<SysGrayRule> entities;
            using (var scope = _scopeFactory.CreateScope())
            {
                var unitOfWorkManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
                using var unitOfWork = unitOfWorkManager.Begin(new XiHanUnitOfWorkOptions { IsTransactional = false }, requiresNew: true);

                var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
                using (currentTenant.Change(null))
                {
                    var clientResolver = scope.ServiceProvider.GetRequiredService<ISqlSugarClientResolver>();
                    var client = clientResolver.GetClientForEntity<SysGrayRule>();

                    entities = await client.Queryable<SysGrayRule>().ToListAsync(cancellationToken);
                }

                await unitOfWork.CompleteAsync(cancellationToken);
            }

            var loaded = new Dictionary<string, GrayRule>(StringComparer.Ordinal);
            foreach (var entity in entities)
            {
                loaded[entity.BasicId] = GrayRuleMapper.ToModel(entity);
            }

            _cache = loaded;
            Volatile.Write(ref _lastRefreshTicks, _timeProvider.GetUtcNow().UtcTicks);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            var backoff = _options.RefreshInterval < MaxFailureBackoff ? _options.RefreshInterval : MaxFailureBackoff;
            var deferTicks = Math.Max(_options.RefreshInterval.Ticks - backoff.Ticks, 0);
            Volatile.Write(ref _lastRefreshTicks, Math.Max(_timeProvider.GetUtcNow().UtcTicks - deferTicks, 1));

            _logger.LogWarning(ex, "灰度规则加载失败，保留上次成功加载的规则，{Backoff} 后重试", backoff);
            throw;
        }
    }

    /// <summary>
    /// 缓存是否已到期
    /// </summary>
    private bool IsExpired()
    {
        var lastRefreshTicks = Volatile.Read(ref _lastRefreshTicks);
        if (lastRefreshTicks == 0)
        {
            return true;
        }

        return _timeProvider.GetUtcNow().UtcTicks - lastRefreshTicks >= _options.RefreshInterval.Ticks;
    }
}
