// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using SqlSugar;
using XiHan.Framework.Data.SqlSugar.Clients;
using XiHan.Framework.DistributedIds;
using XiHan.Framework.Settings.Definitions;
using XiHan.Framework.Settings.SqlSugar.Entities;
using XiHan.Framework.Settings.Stores;

namespace XiHan.Framework.Settings.SqlSugar.Stores;

/// <summary>
/// 设置存储的 SqlSugar 实现
/// </summary>
public class SqlSugarSettingStore : ISettingStore
{
    private readonly ISqlSugarClientResolver _clientResolver;
    private readonly IDistributedIdGenerator<long> _idGenerator;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="clientResolver">客户端解析器</param>
    /// <param name="idGenerator">主键生成器</param>
    public SqlSugarSettingStore(
        ISqlSugarClientResolver clientResolver,
        IDistributedIdGenerator<long> idGenerator)
    {
        _clientResolver = clientResolver;
        _idGenerator = idGenerator;
    }

    /// <summary>
    /// 获取设置值
    /// </summary>
    /// <param name="name">设置名称</param>
    /// <param name="providerName">提供者名称</param>
    /// <param name="providerKey">提供者键</param>
    /// <returns>设置值，未命中返回 null</returns>
    public async Task<string?> GetOrNullAsync(string name, string? providerName, string? providerKey)
    {
        var client = _clientResolver.GetClientForEntity<SysSetting>();

        var entity = await FindAsync(client, name, Normalize(providerName), Normalize(providerKey));

        return entity?.SettingValue;
    }

    /// <summary>
    /// 获取所有设置值
    /// </summary>
    /// <param name="names">设置名称数组</param>
    /// <param name="providerName">提供者名称</param>
    /// <param name="providerKey">提供者键</param>
    /// <returns>设置值列表，未命中的名称对应值为 null，顺序与输入一致</returns>
    public async Task<List<SettingValue>> GetAllAsync(string[] names, string? providerName, string? providerKey)
    {
        if (names.Length == 0)
        {
            return [];
        }

        var client = _clientResolver.GetClientForEntity<SysSetting>();
        var normalizedProviderName = Normalize(providerName);
        var normalizedProviderKey = Normalize(providerKey);

        var rows = await client.Queryable<SysSetting>()
            .Where(item => names.Contains(item.SettingName)
                && item.ProviderName == normalizedProviderName
                && item.ProviderKey == normalizedProviderKey)
            .ToListAsync();

        var valuesByName = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var row in rows)
        {
            valuesByName[row.SettingName] = row.SettingValue;
        }

        return [.. names.Select(name => new SettingValue(name, valuesByName.GetValueOrDefault(name)))];
    }

    /// <summary>
    /// 设置值
    /// </summary>
    /// <param name="name">设置名称</param>
    /// <param name="value">设置值</param>
    /// <param name="providerName">提供者名称</param>
    /// <param name="providerKey">提供者键</param>
    public async Task SetAsync(string name, string? value, string? providerName, string? providerKey)
    {
        var client = _clientResolver.GetClientForEntity<SysSetting>();
        var normalizedProviderName = Normalize(providerName);
        var normalizedProviderKey = Normalize(providerKey);

        var existing = await FindAsync(client, name, normalizedProviderName, normalizedProviderKey);

        if (existing is not null)
        {
            existing.SettingValue = value;
            await client.Updateable(existing).ExecuteCommandAsync();
            return;
        }

        var entity = new SysSetting(_idGenerator.NextId())
        {
            SettingName = name,
            ProviderName = normalizedProviderName,
            ProviderKey = normalizedProviderKey,
            SettingValue = value
        };

        await OnBeforeInsertAsync(entity, CancellationToken.None);

        try
        {
            await client.Insertable(entity).ExecuteCommandAsync();
        }
        catch (Exception ex)
        {
            SysSetting? winner;

            try
            {
                winner = await FindAsync(client, name, normalizedProviderName, normalizedProviderKey);
            }
            catch (Exception requeryException)
            {
                throw new AggregateException(ex, requeryException);
            }

            if (winner is null)
            {
                throw;
            }

            winner.SettingValue = value;
            await client.Updateable(winner).ExecuteCommandAsync();
        }
    }

    /// <summary>
    /// 删除设置值
    /// </summary>
    /// <param name="name">设置名称</param>
    /// <param name="providerName">提供者名称</param>
    /// <param name="providerKey">提供者键</param>
    public async Task DeleteAsync(string name, string? providerName, string? providerKey)
    {
        var client = _clientResolver.GetClientForEntity<SysSetting>();
        var normalizedProviderName = Normalize(providerName);
        var normalizedProviderKey = Normalize(providerKey);

        await client.Deleteable<SysSetting>()
            .Where(item => item.SettingName == name
                && item.ProviderName == normalizedProviderName
                && item.ProviderKey == normalizedProviderKey)
            .ExecuteCommandAsync();
    }

    /// <summary>
    /// 把可空的提供者字段归一化为非空的哨兵值
    /// </summary>
    /// <param name="value">原始值</param>
    /// <returns>归一化后的值</returns>
    private static string Normalize(string? value)
    {
        return value ?? string.Empty;
    }

    /// <summary>
    /// 按业务键查找一行设置
    /// </summary>
    /// <param name="client">客户端</param>
    /// <param name="name">设置名称</param>
    /// <param name="providerName">已归一化的提供者名称</param>
    /// <param name="providerKey">已归一化的提供者键</param>
    /// <returns>命中的实体，未命中返回 null</returns>
    private static async Task<SysSetting?> FindAsync(
        ISqlSugarClient client, string name, string providerName, string providerKey)
    {
        return await client.Queryable<SysSetting>()
            .FirstAsync(item => item.SettingName == name
                && item.ProviderName == providerName
                && item.ProviderKey == providerKey);
    }

    /// <summary>
    /// 插入前的扩展点，供测试注入并发写入
    /// </summary>
    /// <param name="entity">即将插入的实体</param>
    /// <param name="cancellationToken">取消令牌</param>
    protected virtual Task OnBeforeInsertAsync(SysSetting entity, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
