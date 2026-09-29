// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using SqlSugar;
using XiHan.Framework.Data.SqlSugar.Clients;
using XiHan.Framework.DistributedIds;
using XiHan.Framework.Security.Services;
using XiHan.Framework.Security.SqlSugar.Entities;

namespace XiHan.Framework.Security.SqlSugar.Services;

/// <summary>
/// 密码历史记录 SqlSugar 存储
/// </summary>
public class SqlSugarPasswordHistoryStore : IPasswordHistoryStore
{
    private readonly ISqlSugarClientResolver _clientResolver;
    private readonly IDistributedIdGenerator<long> _idGenerator;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="clientResolver">客户端解析器</param>
    /// <param name="idGenerator">主键生成器</param>
    public SqlSugarPasswordHistoryStore(
        ISqlSugarClientResolver clientResolver,
        IDistributedIdGenerator<long> idGenerator)
    {
        _clientResolver = clientResolver;
        _idGenerator = idGenerator;
    }

    /// <summary>
    /// 获取用户最近的密码哈希列表，按记录时间由旧到新排列
    /// </summary>
    /// <param name="userId">用户标识</param>
    /// <param name="count">获取数量</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>密码哈希列表</returns>
    public async Task<IReadOnlyList<string>> GetRecentPasswordHashesAsync(long userId, int count, CancellationToken ct = default)
    {
        if (count <= 0)
        {
            return [];
        }

        var client = _clientResolver.GetClientForEntity<SysPasswordHistory>();

        var recent = await client.Queryable<SysPasswordHistory>()
            .Where(item => item.UserId == userId)
            .OrderBy(item => item.CreatedTime, OrderByType.Desc)
            .OrderBy(item => item.BasicId, OrderByType.Desc)
            .Take(count)
            .Select(item => item.PasswordHash)
            .ToListAsync(ct);

        recent.Reverse();
        return recent;
    }

    /// <summary>
    /// 记录新密码哈希，并把该用户的历史记录裁剪到上限之内
    /// </summary>
    /// <param name="userId">用户标识</param>
    /// <param name="passwordHash">密码哈希</param>
    /// <param name="maxHistoryCount">最大历史记录数</param>
    /// <param name="ct">取消令牌</param>
    public async Task RecordPasswordAsync(long userId, string passwordHash, int maxHistoryCount = 10, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);

        var client = _clientResolver.GetClientForEntity<SysPasswordHistory>();

        var entity = new SysPasswordHistory(_idGenerator.NextId())
        {
            UserId = userId,
            PasswordHash = passwordHash,
            CreatedTime = DateTimeOffset.UtcNow
        };

        await client.Insertable(entity).ExecuteCommandAsync(ct);

        var orderedIds = await client.Queryable<SysPasswordHistory>()
            .Where(item => item.UserId == userId)
            .OrderBy(item => item.CreatedTime, OrderByType.Desc)
            .OrderBy(item => item.BasicId, OrderByType.Desc)
            .Select(item => item.BasicId)
            .ToListAsync(ct);

        var staleIds = orderedIds.Skip(maxHistoryCount).ToList();

        if (staleIds.Count > 0)
        {
            await client.Deleteable<SysPasswordHistory>().In(staleIds).ExecuteCommandAsync(ct);
        }
    }
}
