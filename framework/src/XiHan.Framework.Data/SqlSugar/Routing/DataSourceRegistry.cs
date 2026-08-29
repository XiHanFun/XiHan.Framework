// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using SqlSugar;
using XiHan.Framework.Domain.Entities.Abstracts;
using XiHan.Framework.Utils.Reflections;

namespace XiHan.Framework.Data.SqlSugar.Routing;

/// <summary>
/// 数据源注册表默认实现：扫描全部实体，收集其声明的逻辑数据源名
/// </summary>
/// <remarks>
/// 扫描结果惰性求值并缓存一次。实体集合在进程生命周期内不变，无需失效。
/// </remarks>
public class DataSourceRegistry : IDataSourceRegistry
{
    private readonly IEntityDataSourceResolver _dataSourceResolver;
    private readonly Lazy<HashSet<string>> _names;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="dataSourceResolver">实体数据源解析器</param>
    public DataSourceRegistry(IEntityDataSourceResolver dataSourceResolver)
    {
        _dataSourceResolver = dataSourceResolver;
        _names = new Lazy<HashSet<string>>(Scan, LazyThreadSafetyMode.ExecutionAndPublication);
    }

    /// <summary>
    /// 全部被声明过的逻辑数据源名
    /// </summary>
    public IReadOnlyCollection<string> DataSourceNames => _names.Value;

    /// <summary>
    /// 判断某个名称是否为已声明的数据源名
    /// </summary>
    /// <param name="name">待判定的名称</param>
    /// <returns>是数据源名返回 true</returns>
    public bool IsDataSource(string? name)
    {
        return !string.IsNullOrWhiteSpace(name) && _names.Value.Contains(name.Trim());
    }

    /// <summary>
    /// 扫描全部实体声明的数据源名
    /// </summary>
    /// <returns>数据源名集合</returns>
    private HashSet<string> Scan()
    {
        var entityTypes = ReflectionHelper.GetContainsAttributeSubClasses<IEntityBase, SugarTable>();
        return new HashSet<string>(
            entityTypes
                .Select(entityType => _dataSourceResolver.ResolveDataSourceName(entityType)?.Trim())
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(name => name!),
            StringComparer.OrdinalIgnoreCase);
    }
}
