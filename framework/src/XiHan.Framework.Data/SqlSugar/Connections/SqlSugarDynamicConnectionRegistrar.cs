#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:SqlSugarDynamicConnectionRegistrar
// Guid:5b7e1d42-9a08-4c36-af51-2e83b7c04d19
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/07/20 10:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using SqlSugar;
using XiHan.Framework.Data.SqlSugar.Clients;

namespace XiHan.Framework.Data.SqlSugar.Connections;

/// <summary>
/// 动态连接注册器的 SqlSugar 实现
/// </summary>
/// <remarks>
/// 经 <see cref="ITenant.AddConnection"/> 把连接加入运行中的 SqlSugarScope，
/// 之后即可用 <see cref="ITenant.GetConnectionScope"/> 取到客户端。
/// 单例注册：注册结果作用于整个 SqlSugarScope，与请求作用域无关。
/// </remarks>
public sealed class SqlSugarDynamicConnectionRegistrar(
    ISqlSugarClientResolver clientResolver,
    ILogger<SqlSugarDynamicConnectionRegistrar> logger) : IDynamicConnectionRegistrar
{
    private readonly ISqlSugarClientResolver _clientResolver = clientResolver;
    private readonly ILogger<SqlSugarDynamicConnectionRegistrar> _logger = logger;

    /// <summary>
    /// 已注册的 ConfigId 记账（避免重复 AddConnection）
    /// </summary>
    private readonly ConcurrentDictionary<string, byte> _registered = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public bool IsRegistered(string configId)
    {
        return !string.IsNullOrWhiteSpace(configId) && _registered.ContainsKey(configId.Trim());
    }

    /// <inheritdoc />
    public void Register(DynamicConnectionDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentException.ThrowIfNullOrWhiteSpace(descriptor.ConfigId);
        ArgumentException.ThrowIfNullOrWhiteSpace(descriptor.ConnectionString);

        var configId = descriptor.ConfigId.Trim();

        // 记账先行：并发下只让一个调用真正执行 AddConnection
        if (!_registered.TryAdd(configId, 0))
        {
            return;
        }

        try
        {
            // 外部库仅用于读取元数据：不挂全局过滤器、不挂 AOP、不参与多租户解析。
            // 这些是为本系统实体准备的，套在外部表上会直接抛错。
            _clientResolver.AsTenant().AddConnection(new ConnectionConfig
            {
                ConfigId = configId,
                DbType = descriptor.DbType,
                ConnectionString = descriptor.ConnectionString,
                IsAutoCloseConnection = true
            });

            _logger.LogInformation("已注册动态数据库连接：ConfigId={ConfigId}，DbType={DbType}", configId, descriptor.DbType);
        }
        catch
        {
            // 注册失败必须回滚记账，否则后续重试会在上方短路、永远拿不到连接
            _registered.TryRemove(configId, out _);
            throw;
        }
    }

    /// <inheritdoc />
    public ISqlSugarClient? GetClient(string configId)
    {
        if (string.IsNullOrWhiteSpace(configId))
        {
            return null;
        }

        var trimmed = configId.Trim();
        return _registered.ContainsKey(trimmed)
            ? _clientResolver.AsTenant().GetConnectionScope(trimmed)
            : null;
    }
}
