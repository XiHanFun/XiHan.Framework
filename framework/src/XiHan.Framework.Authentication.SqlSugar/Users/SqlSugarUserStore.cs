// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Globalization;
using SqlSugar;
using XiHan.Framework.Authentication.SqlSugar.Entities;
using XiHan.Framework.Authentication.SqlSugar.Mapping;
using XiHan.Framework.Authentication.Users;
using XiHan.Framework.Data.SqlSugar.Clients;
using XiHan.Framework.DistributedIds;
using XiHan.Framework.MultiTenancy.Abstractions;

namespace XiHan.Framework.Authentication.SqlSugar.Users;

/// <summary>
/// 用户 SqlSugar 存储
/// </summary>
/// <remarks>
/// 读写都限定在当前租户内。同一实例内对同一用户的多次读取返回同一个 <see cref="UserInfo"/> 实例，
/// 更新密码、失败计数与锁定时间的方法会同步修改该实例。
/// </remarks>
public class SqlSugarUserStore : IUserStore
{
    private readonly ISqlSugarClientResolver _clientResolver;
    private readonly ICurrentTenant _currentTenant;
    private readonly IDistributedIdGenerator<long> _idGenerator;
    private readonly TimeProvider _timeProvider;
    private readonly Dictionary<long, UserInfo> _loadedUsers = [];

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="clientResolver">客户端解析器</param>
    /// <param name="currentTenant">当前租户</param>
    /// <param name="idGenerator">主键生成器</param>
    /// <param name="timeProvider">时间提供程序</param>
    public SqlSugarUserStore(
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
    /// 根据用户名获取用户，不区分大小写
    /// </summary>
    /// <param name="username">用户名</param>
    /// <param name="cancellationToken">取消令牌，仅在访问数据库前检查</param>
    /// <returns>用户信息，不存在时返回空</returns>
    public async Task<UserInfo?> GetUserByUsernameAsync(string username, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(username))
        {
            return null;
        }

        var entity = await FindByUserNameAsync(GetClient(), GetTenantId(), username);

        return entity is null ? null : Track(entity);
    }

    /// <summary>
    /// 根据用户标识获取用户
    /// </summary>
    /// <param name="userId">用户标识</param>
    /// <param name="cancellationToken">取消令牌，仅在访问数据库前检查</param>
    /// <returns>用户信息，不存在或标识不是正整数时返回空</returns>
    public async Task<UserInfo?> GetUserByIdAsync(string userId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!TryParseUserId(userId, out var id))
        {
            return null;
        }

        var tenantId = GetTenantId();
        var entity = await GetClient().Queryable<SysAuthUser>()
            .Where(item => item.BasicId == id && item.TenantId == tenantId)
            .FirstAsync();

        return entity is null ? null : Track(entity);
    }

    /// <summary>
    /// 更新用户信息
    /// </summary>
    /// <remarks>
    /// 不写密码哈希、登录失败次数与锁定状态；它们只经各自的专用方法修改。
    /// </remarks>
    /// <param name="user">用户信息</param>
    /// <param name="cancellationToken">取消令牌，仅在访问数据库前检查</param>
    /// <exception cref="ArgumentException">用户信息为空、用户标识不是正整数或用户名为空白</exception>
    /// <exception cref="InvalidOperationException">当前租户内不存在该用户</exception>
    public async Task UpdateUserAsync(UserInfo user, CancellationToken cancellationToken = default)
    {
        if (user is null || !TryParseUserId(user.UserId, out var id))
        {
            throw new ArgumentException("用户信息或用户ID无效", nameof(user));
        }

        if (string.IsNullOrWhiteSpace(user.Username))
        {
            throw new ArgumentException("用户名不能为空", nameof(user));
        }

        cancellationToken.ThrowIfCancellationRequested();

        var client = GetClient();
        var tenantId = GetTenantId();
        var values = AuthUserMapper.ToEntity(user, id, tenantId);
        var userName = values.UserName;
        var normalizedUserName = values.NormalizedUserName;
        var email = values.Email;
        var phoneNumber = values.PhoneNumber;
        var twoFactorEnabled = values.TwoFactorEnabled;
        var twoFactorSecret = values.TwoFactorSecret;
        var recoveryCodes = values.RecoveryCodes;
        var lastLoginTime = values.LastLoginTime;
        var passwordChangedTime = values.PasswordChangedTime;
        var isActive = values.IsActive;
        var additionalData = values.AdditionalData;

        var affected = await client.Updateable<SysAuthUser>()
            .SetColumns(item => new SysAuthUser
            {
                UserName = userName,
                NormalizedUserName = normalizedUserName,
                Email = email,
                PhoneNumber = phoneNumber,
                TwoFactorEnabled = twoFactorEnabled,
                TwoFactorSecret = twoFactorSecret,
                RecoveryCodes = recoveryCodes,
                LastLoginTime = lastLoginTime,
                PasswordChangedTime = passwordChangedTime,
                IsActive = isActive,
                AdditionalData = additionalData
            })
            .Where(item => item.BasicId == id && item.TenantId == tenantId)
            .ExecuteCommandAsync();

        if (affected == 0 && !await ExistsAsync(client, id, tenantId))
        {
            throw new InvalidOperationException($"用户 {user.UserId} 不存在");
        }

        _loadedUsers[id] = user;
    }

    /// <summary>
    /// 更新用户密码
    /// </summary>
    /// <remarks>
    /// 密码哈希按原样写入。
    /// </remarks>
    /// <param name="userId">用户标识</param>
    /// <param name="passwordHash">密码哈希</param>
    /// <param name="cancellationToken">取消令牌，仅在访问数据库前检查</param>
    /// <exception cref="ArgumentException">用户标识或密码哈希为空白</exception>
    /// <exception cref="InvalidOperationException">当前租户内不存在该用户</exception>
    public async Task UpdatePasswordAsync(string userId, string passwordHash, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(passwordHash))
        {
            throw new ArgumentException("用户ID和密码哈希不能为空");
        }

        if (!TryParseUserId(userId, out var id))
        {
            throw new InvalidOperationException($"用户 {userId} 不存在");
        }

        cancellationToken.ThrowIfCancellationRequested();

        var client = GetClient();
        var tenantId = GetTenantId();

        var affected = await client.Updateable<SysAuthUser>()
            .SetColumns(item => new SysAuthUser { PasswordHash = passwordHash })
            .Where(item => item.BasicId == id && item.TenantId == tenantId)
            .ExecuteCommandAsync();

        if (affected == 0 && !await ExistsAsync(client, id, tenantId))
        {
            throw new InvalidOperationException($"用户 {userId} 不存在");
        }

        if (_loadedUsers.TryGetValue(id, out var loaded))
        {
            loaded.PasswordHash = passwordHash;
        }
    }

    /// <summary>
    /// 获取登录失败次数
    /// </summary>
    /// <param name="username">用户名</param>
    /// <param name="cancellationToken">取消令牌，仅在访问数据库前检查</param>
    /// <returns>失败次数，用户不存在时返回 0</returns>
    public async Task<int> GetFailedLoginAttemptsAsync(string username, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(username))
        {
            return 0;
        }

        var entity = await RefreshSecurityStateAsync(GetClient(), GetTenantId(), username);

        return entity?.FailedLoginAttempts ?? 0;
    }

    /// <summary>
    /// 记录登录失败，失败次数在数据库侧加一
    /// </summary>
    /// <param name="username">用户名</param>
    /// <param name="cancellationToken">取消令牌，仅在访问数据库前检查</param>
    public async Task IncrementFailedLoginAttemptsAsync(string username, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(username))
        {
            return;
        }

        var client = GetClient();
        var tenantId = GetTenantId();
        var normalizedUserName = AuthUserMapper.NormalizeUserName(username);

        await client.Updateable<SysAuthUser>()
            .SetColumns(item => item.FailedLoginAttempts == item.FailedLoginAttempts + 1)
            .Where(item => item.TenantId == tenantId && item.NormalizedUserName == normalizedUserName)
            .ExecuteCommandAsync();

        await RefreshSecurityStateAsync(client, tenantId, username);
    }

    /// <summary>
    /// 重置登录失败次数
    /// </summary>
    /// <param name="username">用户名</param>
    /// <param name="cancellationToken">取消令牌，仅在访问数据库前检查</param>
    public async Task ResetFailedLoginAttemptsAsync(string username, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(username))
        {
            return;
        }

        var client = GetClient();
        var tenantId = GetTenantId();
        var normalizedUserName = AuthUserMapper.NormalizeUserName(username);

        await client.Updateable<SysAuthUser>()
            .SetColumns(item => new SysAuthUser { FailedLoginAttempts = 0 })
            .Where(item => item.TenantId == tenantId && item.NormalizedUserName == normalizedUserName)
            .ExecuteCommandAsync();

        await RefreshSecurityStateAsync(client, tenantId, username);
    }

    /// <summary>
    /// 设置账户锁定时间
    /// </summary>
    /// <remarks>
    /// 锁定结束时间晚于当前时间时标记为锁定。
    /// </remarks>
    /// <param name="username">用户名</param>
    /// <param name="lockoutEnd">锁定结束时间，为空表示解除锁定</param>
    /// <param name="cancellationToken">取消令牌，仅在访问数据库前检查</param>
    public async Task SetLockoutEndAsync(string username, DateTime? lockoutEnd, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(username))
        {
            return;
        }

        var client = GetClient();
        var tenantId = GetTenantId();
        var normalizedUserName = AuthUserMapper.NormalizeUserName(username);
        var lockoutEndUtc = StorageTime.ToUtc(lockoutEnd);
        var isLocked = lockoutEndUtc.HasValue && lockoutEndUtc.Value > _timeProvider.GetUtcNow().UtcDateTime;

        await client.Updateable<SysAuthUser>()
            .SetColumns(item => new SysAuthUser
            {
                LockoutEnd = lockoutEndUtc,
                IsLocked = isLocked
            })
            .Where(item => item.TenantId == tenantId && item.NormalizedUserName == normalizedUserName)
            .ExecuteCommandAsync();

        await RefreshSecurityStateAsync(client, tenantId, username);
    }

    /// <summary>
    /// 获取账户锁定结束时间
    /// </summary>
    /// <param name="username">用户名</param>
    /// <param name="cancellationToken">取消令牌，仅在访问数据库前检查</param>
    /// <returns>锁定结束时间（UTC），用户不存在或未锁定时返回空</returns>
    public async Task<DateTime?> GetLockoutEndAsync(string username, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(username))
        {
            return null;
        }

        var entity = await RefreshSecurityStateAsync(GetClient(), GetTenantId(), username);

        return StorageTime.FromStorage(entity?.LockoutEnd);
    }

    /// <summary>
    /// 添加用户
    /// </summary>
    /// <remarks>
    /// 密码哈希按原样写入。用户标识为空时生成新标识并回写到 <paramref name="user"/>。
    /// </remarks>
    /// <param name="user">用户信息</param>
    /// <param name="cancellationToken">取消令牌，仅在访问数据库前检查</param>
    /// <returns>用户标识</returns>
    /// <exception cref="ArgumentException">用户名为空白，或用户标识不是正整数</exception>
    /// <exception cref="InvalidOperationException">当前租户内已存在同名用户（不区分大小写）</exception>
    public async Task<string> AddUserAsync(UserInfo user, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);

        if (string.IsNullOrWhiteSpace(user.Username))
        {
            throw new ArgumentException("用户名不能为空", nameof(user));
        }

        long id;
        if (string.IsNullOrWhiteSpace(user.UserId))
        {
            id = _idGenerator.NextId();
        }
        else if (!TryParseUserId(user.UserId, out id))
        {
            throw new ArgumentException("用户ID必须是正整数", nameof(user));
        }

        cancellationToken.ThrowIfCancellationRequested();

        var client = GetClient();
        var tenantId = GetTenantId();

        if (await FindByUserNameAsync(client, tenantId, user.Username) is not null)
        {
            throw new InvalidOperationException($"用户名 {user.Username} 已存在");
        }

        try
        {
            await client.Insertable(AuthUserMapper.ToEntity(user, id, tenantId)).ExecuteCommandAsync();
        }
        catch (Exception ex)
        {
            SysAuthUser? conflict;

            try
            {
                conflict = await FindByUserNameAsync(client, tenantId, user.Username);
            }
            catch (Exception requeryException)
            {
                throw new AggregateException(ex, requeryException);
            }

            if (conflict is not null)
            {
                throw new InvalidOperationException($"用户名 {user.Username} 已存在");
            }

            throw;
        }

        user.UserId = id.ToString(CultureInfo.InvariantCulture);
        _loadedUsers[id] = user;

        return user.UserId;
    }

    private ISqlSugarClient GetClient()
    {
        return _clientResolver.GetClientForEntity<SysAuthUser>();
    }

    private long GetTenantId()
    {
        return _currentTenant.Id ?? 0;
    }

    private UserInfo Track(SysAuthUser entity)
    {
        if (_loadedUsers.TryGetValue(entity.BasicId, out var loaded))
        {
            return loaded;
        }

        var user = AuthUserMapper.ToUserInfo(entity);
        _loadedUsers[entity.BasicId] = user;

        return user;
    }

    private async Task<SysAuthUser?> RefreshSecurityStateAsync(ISqlSugarClient client, long tenantId, string username)
    {
        var entity = await FindByUserNameAsync(client, tenantId, username);
        if (entity is not null)
        {
            SyncSecurityState(entity);
        }

        return entity;
    }

    private void SyncSecurityState(SysAuthUser entity)
    {
        if (!_loadedUsers.TryGetValue(entity.BasicId, out var loaded))
        {
            return;
        }

        loaded.FailedLoginAttempts = entity.FailedLoginAttempts;
        loaded.IsLocked = entity.IsLocked;
        loaded.LockoutEnd = StorageTime.FromStorage(entity.LockoutEnd);
    }

    private static async Task<SysAuthUser?> FindByUserNameAsync(ISqlSugarClient client, long tenantId, string username)
    {
        var normalizedUserName = AuthUserMapper.NormalizeUserName(username);

        return await client.Queryable<SysAuthUser>()
            .Where(item => item.TenantId == tenantId && item.NormalizedUserName == normalizedUserName)
            .FirstAsync();
    }

    private static async Task<bool> ExistsAsync(ISqlSugarClient client, long id, long tenantId)
    {
        return await client.Queryable<SysAuthUser>()
            .Where(item => item.BasicId == id && item.TenantId == tenantId)
            .AnyAsync();
    }

    private static bool TryParseUserId(string? userId, out long id)
    {
        return long.TryParse(userId, NumberStyles.None, CultureInfo.InvariantCulture, out id) && id > 0;
    }
}
