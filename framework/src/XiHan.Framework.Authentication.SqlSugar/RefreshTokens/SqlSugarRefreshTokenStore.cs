// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SqlSugar;
using XiHan.Framework.Authentication.Jwt;
using XiHan.Framework.Authentication.SqlSugar.Entities;
using XiHan.Framework.Authentication.SqlSugar.Mapping;
using XiHan.Framework.Authentication.SqlSugar.Options;
using XiHan.Framework.Data.SqlSugar.Clients;
using XiHan.Framework.DistributedIds;
using XiHan.Framework.MultiTenancy.Abstractions;

namespace XiHan.Framework.Authentication.SqlSugar.RefreshTokens;

/// <summary>
/// 刷新令牌 SqlSugar 存储
/// </summary>
/// <remarks>
/// 只保存令牌的 SHA-256 哈希；移除令牌时标记撤销而不删除行。已撤销的令牌再次被校验时，
/// 按配置在独立连接上撤销同一租户下同一主体的全部未撤销令牌。每次调用在新的依赖注入作用域中解析数据库客户端。
/// </remarks>
public class SqlSugarRefreshTokenStore : IRefreshTokenStore
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IDistributedIdGenerator<long> _idGenerator;
    private readonly TimeProvider _timeProvider;
    private readonly XiHanAuthenticationSqlSugarOptions _options;
    private readonly ILogger<SqlSugarRefreshTokenStore> _logger;
    private int _saveCount;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="scopeFactory">作用域工厂</param>
    /// <param name="idGenerator">主键生成器</param>
    /// <param name="timeProvider">时间提供程序</param>
    /// <param name="options">认证存储配置</param>
    /// <param name="logger">日志器</param>
    public SqlSugarRefreshTokenStore(
        IServiceScopeFactory scopeFactory,
        IDistributedIdGenerator<long> idGenerator,
        TimeProvider timeProvider,
        IOptions<XiHanAuthenticationSqlSugarOptions> options,
        ILogger<SqlSugarRefreshTokenStore> logger)
    {
        _scopeFactory = scopeFactory;
        _idGenerator = idGenerator;
        _timeProvider = timeProvider;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// 保存刷新令牌
    /// </summary>
    /// <remarks>
    /// 过期时间不晚于当前时间时只把该令牌标记为已撤销，不插入、不抛出。每保存若干次先在独立连接上删除已过期的记录。
    /// </remarks>
    /// <param name="refreshToken">刷新令牌</param>
    /// <param name="subject">主体标识</param>
    /// <param name="expiresAt">过期时间</param>
    public void Save(string refreshToken, string? subject, DateTime expiresAt)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return;
        }

        var now = GetUtcNow();
        var expiresAtUtc = StorageTime.ToUtc(expiresAt);

        using var scope = _scopeFactory.CreateScope();
        var client = GetClient(scope);

        if (expiresAtUtc <= now)
        {
            Revoke(client, RefreshTokenHasher.Hash(refreshToken), now);
            return;
        }

        CleanupIfDue(client, now);

        client.Insertable(new SysAuthRefreshToken(_idGenerator.NextId())
        {
            TenantId = GetTenantId(scope),
            TokenHash = RefreshTokenHasher.Hash(refreshToken),
            Subject = subject,
            ExpiresAt = expiresAtUtc,
            CreatedTime = now
        }).ExecuteCommand();
    }

    /// <summary>
    /// 校验刷新令牌
    /// </summary>
    /// <remarks>
    /// 已撤销的令牌再次被校验时，按配置撤销同一租户下同一主体的全部未撤销令牌。
    /// </remarks>
    /// <param name="refreshToken">刷新令牌</param>
    /// <param name="subject">主体标识，为空白时不做绑定校验</param>
    /// <returns>令牌存在、未过期、未撤销且主体相符时返回 true</returns>
    public bool Validate(string refreshToken, string? subject = null)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return false;
        }

        var tokenHash = RefreshTokenHasher.Hash(refreshToken);
        var now = GetUtcNow();

        using var scope = _scopeFactory.CreateScope();
        var client = GetClient(scope);

        var entry = client.Queryable<SysAuthRefreshToken>()
            .Where(item => item.TokenHash == tokenHash)
            .First();

        if (entry is null || entry.ExpiresAt <= now)
        {
            return false;
        }

        if (entry.RevokedTime is not null)
        {
            HandleReuse(client, entry, now);
            return false;
        }

        return string.IsNullOrWhiteSpace(subject) ||
               string.Equals(entry.Subject, subject, StringComparison.Ordinal);
    }

    /// <summary>
    /// 移除刷新令牌，标记为已撤销
    /// </summary>
    /// <remarks>
    /// 令牌不存在时不做任何事；条件更新未命中而令牌存在，说明已被撤销，抛出异常。
    /// </remarks>
    /// <param name="refreshToken">刷新令牌</param>
    /// <exception cref="InvalidOperationException">该令牌已被撤销</exception>
    public void Remove(string refreshToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return;
        }

        var tokenHash = RefreshTokenHasher.Hash(refreshToken);

        using var scope = _scopeFactory.CreateScope();
        var client = GetClient(scope);

        if (Revoke(client, tokenHash, GetUtcNow()) > 0)
        {
            return;
        }

        var exists = client.Queryable<SysAuthRefreshToken>()
            .Where(item => item.TokenHash == tokenHash)
            .Any();

        if (exists)
        {
            throw new InvalidOperationException("刷新令牌已被撤销。");
        }
    }

    /// <summary>
    /// 把未撤销的令牌标记为已撤销
    /// </summary>
    /// <param name="client">当前客户端</param>
    /// <param name="tokenHash">令牌哈希</param>
    /// <param name="now">当前 UTC 时间</param>
    /// <returns>受影响的行数</returns>
    private static int Revoke(ISqlSugarClient client, string tokenHash, DateTime now)
    {
        return client.Updateable<SysAuthRefreshToken>()
            .SetColumns(item => new SysAuthRefreshToken { RevokedTime = now })
            .Where(item => item.TokenHash == tokenHash && item.RevokedTime == null)
            .ExecuteCommand();
    }

    /// <summary>
    /// 处理已撤销令牌的再次使用
    /// </summary>
    /// <param name="client">当前客户端</param>
    /// <param name="entry">被再次使用的令牌记录</param>
    /// <param name="now">当前 UTC 时间</param>
    private void HandleReuse(ISqlSugarClient client, SysAuthRefreshToken entry, DateTime now)
    {
        if (!_options.RefreshTokenReuseDetection)
        {
            return;
        }

        var gracePeriod = _options.RefreshTokenReuseGracePeriod;
        if (gracePeriod > TimeSpan.Zero && now - entry.RevokedTime!.Value < gracePeriod)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(entry.Subject))
        {
            _logger.LogWarning("已撤销的刷新令牌再次被使用，该令牌未绑定主体，未执行级联撤销。");
            return;
        }

        var tenantId = entry.TenantId;
        var subject = entry.Subject;

        try
        {
            using var isolated = client.CopyNew();

            var revoked = isolated.Updateable<SysAuthRefreshToken>()
                .SetColumns(item => new SysAuthRefreshToken { RevokedTime = now })
                .Where(item => item.TenantId == tenantId && item.Subject == subject && item.RevokedTime == null)
                .ExecuteCommand();

            _logger.LogWarning(
                "已撤销的刷新令牌再次被使用，已撤销租户 {TenantId} 下主体 {Subject} 的全部刷新令牌，共 {Count} 条。",
                tenantId,
                subject,
                revoked);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "撤销租户 {TenantId} 下主体 {Subject} 的刷新令牌失败。", tenantId, subject);
        }
    }

    /// <summary>
    /// 达到清理频率时在独立连接上删除已过期的记录
    /// </summary>
    /// <param name="client">当前客户端</param>
    /// <param name="now">当前 UTC 时间</param>
    private void CleanupIfDue(ISqlSugarClient client, DateTime now)
    {
        var frequency = _options.RefreshTokenCleanupFrequency;
        if (frequency <= 0 || Interlocked.Increment(ref _saveCount) % frequency != 0)
        {
            return;
        }

        try
        {
            using var isolated = client.CopyNew();

            isolated.Deleteable<SysAuthRefreshToken>()
                .Where(item => item.ExpiresAt < now)
                .ExecuteCommand();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "清理已过期的刷新令牌失败。");
        }
    }

    private DateTime GetUtcNow()
    {
        return _timeProvider.GetUtcNow().UtcDateTime;
    }

    private static ISqlSugarClient GetClient(IServiceScope scope)
    {
        return scope.ServiceProvider.GetRequiredService<ISqlSugarClientResolver>().GetClientForEntity<SysAuthRefreshToken>();
    }

    private static long GetTenantId(IServiceScope scope)
    {
        return scope.ServiceProvider.GetService<ICurrentTenant>()?.Id ?? 0;
    }
}
