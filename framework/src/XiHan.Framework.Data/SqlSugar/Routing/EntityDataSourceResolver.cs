// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Concurrent;
using System.Reflection;

namespace XiHan.Framework.Data.SqlSugar.Routing;

/// <summary>
/// 实体数据源解析器默认实现
/// </summary>
/// <remarks>
/// 先读框架的 <see cref="DataSourceAttribute"/>，未标注再读 SqlSugar 原生的
/// <see cref="global::SqlSugar.TenantAttribute"/>，两者都没有则返回 null。解析结果按实体类型缓存。
/// </remarks>
public class EntityDataSourceResolver : IEntityDataSourceResolver
{
    private static readonly ConcurrentDictionary<Type, string?> ConfigIdCache = new();

    /// <summary>
    /// 解析实体声明的连接配置标识
    /// </summary>
    /// <param name="entityType">实体类型</param>
    /// <returns>连接配置标识；未声明数据源返回 null</returns>
    public virtual string? ResolveConfigId(Type entityType)
    {
        ArgumentNullException.ThrowIfNull(entityType);
        return ConfigIdCache.GetOrAdd(entityType, static type => ReadDeclaredConfigId(type));
    }

    /// <summary>
    /// 读取实体特性上声明的连接配置标识
    /// </summary>
    /// <param name="entityType">实体类型</param>
    /// <returns>连接配置标识；未声明返回 null</returns>
    private static string? ReadDeclaredConfigId(Type entityType)
    {
        var dataSource = entityType.GetCustomAttribute<DataSourceAttribute>(inherit: true);
        if (dataSource is not null)
        {
            return dataSource.ConfigId;
        }

        var configId = entityType.GetCustomAttribute<global::SqlSugar.TenantAttribute>(inherit: true)?.configId?.ToString();
        return string.IsNullOrWhiteSpace(configId) ? null : configId.Trim();
    }
}
