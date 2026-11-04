// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Globalization;
using SqlSugar;
using XiHan.Framework.Persistence.Abstractions;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Persistence.SqlSugar;

/// <summary>
/// 从库配置的归一化与核对
/// </summary>
internal static class SqlSugarReplicaRules
{
    /// <summary>
    /// 连接是否配了从库
    /// </summary>
    public static bool HasReplicas(ConnectionConfig config) => config.SlaveConnectionConfigs is { Count: > 0 };

    /// <summary>
    /// 把权重不大于 0 的从库归一化
    /// </summary>
    /// <remarks>
    /// SqlSugar 按权重大于 0 选从库，全为 0 时对空集合取随机数直接抛异常
    /// </remarks>
    /// <exception cref="ArgumentException">从库的连接串是空白；消息只带连接名</exception>
    public static void Normalize(ConnectionConfig config, int defaultHitRate)
    {
        foreach (var replica in config.SlaveConnectionConfigs ?? [])
        {
            if (string.IsNullOrWhiteSpace(replica.ConnectionString))
            {
                throw new ArgumentException($"A replica of connection '{NameOf(config)}' has a blank connection string.", nameof(config));
            }

            if (replica.HitRate <= 0)
            {
                replica.HitRate = defaultHitRate;
            }
        }
    }

    /// <summary>
    /// 核对几个连接没有共用从库，返回从库连接串到所属连接的表
    /// </summary>
    /// <remarks>
    /// 回调对每个连接都调用一次，不看 <c>ConfigId</c> 就把默认连接的从库挂到别的连接上时，那个连接的读会读到别的库的数据。主库相同的连接是同一个库，共用从库没有问题
    /// </remarks>
    /// <exception cref="FrameworkException">主库不同的连接共用从库（<see cref="PersistenceErrors.ReplicaMisplaced"/>）</exception>
    public static Dictionary<string, ConnectionConfig> Owners(IEnumerable<ConnectionConfig> configs)
    {
        var owners = new Dictionary<string, ConnectionConfig>(StringComparer.Ordinal);
        foreach (var config in configs)
        {
            foreach (var replica in config.SlaveConnectionConfigs ?? [])
            {
                if (owners.TryGetValue(replica.ConnectionString, out var owner))
                {
                    EnsureSameDatabase(config, owner);
                }
                else
                {
                    owners[replica.ConnectionString] = config;
                }
            }
        }

        return owners;
    }

    /// <summary>
    /// 核对分片连接没有用别的连接的从库
    /// </summary>
    /// <remarks>
    /// 分片连接的配置每个作用域现建，这里在作用域取客户端时核对：配错了调用失败，好过把别的库的数据读给这个租户
    /// </remarks>
    /// <exception cref="FrameworkException">从库属于主库不同的连接（<see cref="PersistenceErrors.ReplicaMisplaced"/>）</exception>
    public static void EnsureOwn(ConnectionConfig config, Dictionary<string, ConnectionConfig> owners)
    {
        foreach (var replica in config.SlaveConnectionConfigs ?? [])
        {
            if (owners.TryGetValue(replica.ConnectionString, out var owner))
            {
                EnsureSameDatabase(config, owner);
            }
        }
    }

    /// <summary>
    /// 连接的名字（<c>ConfigId</c>）
    /// </summary>
    public static string NameOf(ConnectionConfig config) => Convert.ToString(config.ConfigId, CultureInfo.InvariantCulture) ?? string.Empty;

    /// <summary>
    /// 两个连接的主库必须相同
    /// </summary>
    private static void EnsureSameDatabase(ConnectionConfig config, ConnectionConfig owner)
    {
        if (!string.Equals(config.ConnectionString, owner.ConnectionString, StringComparison.Ordinal))
        {
            throw new FrameworkException(PersistenceErrors.ReplicaMisplaced, $"Connection '{NameOf(config)}' lists a replica of connection '{NameOf(owner)}', which has a different primary; set replicas only on the connection they belong to, checking 'ConnectionConfig.ConfigId' in the configure callback.");
        }
    }
}
