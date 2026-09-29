// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using SqlSugar;
using XiHan.Framework.Authentication.OAuth;
using XiHan.Framework.Authentication.SqlSugar.Entities;
using XiHan.Framework.Data.SqlSugar.Clients;
using XiHan.Framework.DistributedIds;
using XiHan.Framework.MultiTenancy.Abstractions;

namespace XiHan.Framework.Authentication.SqlSugar.ExternalLogins;

/// <summary>
/// 第三方登录绑定 SqlSugar 存储
/// </summary>
/// <remarks>
/// 未指定租户时使用当前租户。同一第三方账号已绑定其他用户时拒绝再次绑定。
/// 提供商名称不区分大小写，提供商用户标识区分大小写。
/// </remarks>
public class SqlSugarExternalLoginStore : IExternalLoginStore
{
    private const int MaxDisplayNameLength = 256;
    private const int MaxEmailLength = 256;
    private const int MaxAvatarUrlLength = 2048;

    private readonly ISqlSugarClientResolver _clientResolver;
    private readonly ICurrentTenant _currentTenant;
    private readonly IDistributedIdGenerator<long> _idGenerator;
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="clientResolver">客户端解析器</param>
    /// <param name="currentTenant">当前租户</param>
    /// <param name="idGenerator">主键生成器</param>
    /// <param name="timeProvider">时间提供程序</param>
    public SqlSugarExternalLoginStore(
        ISqlSugarClientResolver clientResolver,
        ICurrentTenant currentTenant,
        IDistributedIdGenerator<long> idGenerator,
        TimeProvider timeProvider)
    {
        _clientResolver = clientResolver;
        _currentTenant = currentTenant;
        _idGenerator = idGenerator;
        _timeProvider = timeProvider;
    }

    /// <summary>
    /// 根据提供商和提供商用户标识查找关联的内部用户标识
    /// </summary>
    /// <param name="provider">提供商名称</param>
    /// <param name="providerKey">提供商用户标识</param>
    /// <param name="tenantId">租户标识，为空时使用当前租户</param>
    /// <param name="cancellationToken">取消令牌，仅在访问数据库前检查</param>
    /// <returns>内部用户标识，未绑定时返回空</returns>
    public async Task<long?> FindUserIdAsync(string provider, string providerKey, long? tenantId = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(provider) || string.IsNullOrWhiteSpace(providerKey))
        {
            return null;
        }

        var entity = await FindAsync(GetClient(), ResolveTenantId(tenantId), provider, providerKey);

        return entity?.UserId;
    }

    /// <summary>
    /// 创建第三方登录绑定记录
    /// </summary>
    /// <remarks>
    /// 已绑定到同一用户时不做任何事。
    /// </remarks>
    /// <param name="userId">内部用户标识</param>
    /// <param name="info">第三方登录信息</param>
    /// <param name="tenantId">租户标识，为空时使用当前租户</param>
    /// <param name="cancellationToken">取消令牌，仅在访问数据库前检查</param>
    /// <exception cref="ArgumentOutOfRangeException">用户标识不是正数</exception>
    /// <exception cref="ArgumentException">提供商名称或提供商用户标识为空白</exception>
    /// <exception cref="InvalidOperationException">该第三方账号已绑定到其他用户</exception>
    public async Task CreateAsync(long userId, ExternalLoginInfo info, long? tenantId = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(info);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(userId);

        if (string.IsNullOrWhiteSpace(info.Provider) || string.IsNullOrWhiteSpace(info.ProviderKey))
        {
            throw new ArgumentException("提供商名称与提供商用户标识不能为空", nameof(info));
        }

        cancellationToken.ThrowIfCancellationRequested();

        var client = GetClient();
        var effectiveTenantId = ResolveTenantId(tenantId);
        var existing = await FindAsync(client, effectiveTenantId, info.Provider, info.ProviderKey);

        if (existing is not null)
        {
            if (existing.UserId == userId)
            {
                return;
            }

            throw new InvalidOperationException($"{info.Provider} 账号已绑定到其他用户");
        }

        var entity = new SysAuthExternalLogin(_idGenerator.NextId())
        {
            TenantId = effectiveTenantId,
            UserId = userId,
            Provider = NormalizeProvider(info.Provider),
            ProviderKey = info.ProviderKey,
            DisplayName = Truncate(info.DisplayName, MaxDisplayNameLength),
            Email = Truncate(info.Email, MaxEmailLength),
            AvatarUrl = Truncate(info.AvatarUrl, MaxAvatarUrlLength),
            CreatedTime = _timeProvider.GetUtcNow().UtcDateTime
        };

        try
        {
            await client.Insertable(entity).ExecuteCommandAsync();
        }
        catch (Exception ex)
        {
            SysAuthExternalLogin? conflict;

            try
            {
                conflict = await FindAsync(client, effectiveTenantId, info.Provider, info.ProviderKey);
            }
            catch (Exception requeryException)
            {
                throw new AggregateException(ex, requeryException);
            }

            if (conflict is null)
            {
                throw;
            }

            if (conflict.UserId == userId)
            {
                return;
            }

            throw new InvalidOperationException($"{info.Provider} 账号已绑定到其他用户");
        }
    }

    /// <summary>
    /// 删除该用户在指定提供商下的全部绑定记录
    /// </summary>
    /// <param name="userId">内部用户标识</param>
    /// <param name="provider">提供商名称</param>
    /// <param name="cancellationToken">取消令牌，仅在访问数据库前检查</param>
    /// <exception cref="ArgumentException">提供商名称为空白</exception>
    public async Task RemoveAsync(long userId, string provider, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(provider))
        {
            throw new ArgumentException("提供商名称不能为空", nameof(provider));
        }

        cancellationToken.ThrowIfCancellationRequested();

        var normalizedProvider = NormalizeProvider(provider);

        await GetClient().Deleteable<SysAuthExternalLogin>()
            .Where(item => item.UserId == userId && item.Provider == normalizedProvider)
            .ExecuteCommandAsync();
    }

    private ISqlSugarClient GetClient()
    {
        return _clientResolver.GetClientForEntity<SysAuthExternalLogin>();
    }

    private long ResolveTenantId(long? tenantId)
    {
        return tenantId ?? _currentTenant.Id ?? 0;
    }

    private static async Task<SysAuthExternalLogin?> FindAsync(ISqlSugarClient client, long tenantId, string provider, string providerKey)
    {
        var normalizedProvider = NormalizeProvider(provider);

        var candidates = await client.Queryable<SysAuthExternalLogin>()
            .Where(item => item.TenantId == tenantId && item.Provider == normalizedProvider && item.ProviderKey == providerKey)
            .ToListAsync();

        return candidates.FirstOrDefault(item => string.Equals(item.ProviderKey, providerKey, StringComparison.Ordinal));
    }

    private static string NormalizeProvider(string provider)
    {
        return provider.ToLowerInvariant();
    }

    private static string? Truncate(string? value, int maxLength)
    {
        return value is null || value.Length <= maxLength ? value : value[..maxLength];
    }
}
