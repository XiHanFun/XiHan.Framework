// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Concurrent;
using System.Reflection;

namespace XiHan.Framework.Data.SqlSugar.Routing;

/// <summary>
/// 实体模块数据源解析器默认实现
/// </summary>
/// <remarks>
/// 先读框架的 <see cref="ModuleDataSourceAttribute"/>，未标注再读 SqlSugar 原生的
/// <see cref="global::SqlSugar.TenantAttribute"/>（兼容既有写法），两者都没有则返回 null。
/// 解析结果按实体类型缓存。
/// </remarks>
public class EntityModuleDataSourceResolver : IEntityModuleDataSourceResolver
{
    private static readonly ConcurrentDictionary<Type, string?> NameCache = new();

    private static readonly ConcurrentDictionary<Type, bool> PlatformCache = new();

    /// <summary>
    /// 解析实体声明的模块数据源名
    /// </summary>
    /// <param name="entityType">实体类型</param>
    /// <returns>模块数据源名；未声明返回 null</returns>
    public virtual string? ResolveModuleDataSource(Type entityType)
    {
        ArgumentNullException.ThrowIfNull(entityType);
        return NameCache.GetOrAdd(entityType, static type => ReadDeclaredName(type));
    }

    /// <summary>
    /// 读取实体特性上声明的模块数据源名
    /// </summary>
    /// <param name="entityType">实体类型</param>
    /// <returns>模块数据源名；未声明返回 null</returns>
    /// <summary>
    /// 实体是否固定落在平台库
    /// </summary>
    public virtual bool IsPlatformPlaced(Type entityType)
    {
        ArgumentNullException.ThrowIfNull(entityType);
        return PlatformCache.GetOrAdd(entityType, static type => ReadPlatformPlacement(type));
    }

    private static bool ReadPlatformPlacement(Type entityType)
    {
        if (entityType.GetCustomAttribute<PlatformDataSourceAttribute>(inherit: true) is null)
        {
            return false;
        }

        // 平台库与模块库是两个互斥的落点：同时声明是配置错误，启动即失败，不猜哪个优先
        if (!string.IsNullOrWhiteSpace(ReadDeclaredName(entityType)))
        {
            throw new InvalidOperationException(
                $"实体 {entityType.FullName} 同时声明了平台库与模块数据源，二者只能取其一。");
        }

        return true;
    }

    private static string? ReadDeclaredName(Type entityType)
    {
        var moduleDataSource = entityType.GetCustomAttribute<ModuleDataSourceAttribute>(inherit: true);
        if (moduleDataSource is not null)
        {
            return moduleDataSource.Name;
        }

        var name = entityType.GetCustomAttribute<global::SqlSugar.TenantAttribute>(inherit: true)?.configId?.ToString();
        return string.IsNullOrWhiteSpace(name) ? null : name.Trim();
    }
}
