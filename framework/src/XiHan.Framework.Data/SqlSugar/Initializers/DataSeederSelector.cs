// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Options;
using System.Reflection;
using XiHan.Framework.Core.DynamicProxy;
using XiHan.Framework.Data.SqlSugar.Options;
using XiHan.Framework.Data.SqlSugar.Seeders;

namespace XiHan.Framework.Data.SqlSugar.Initializers;

/// <summary>
/// 种子选取器默认实现
/// </summary>
/// <remarks>
/// 按 <see cref="DataSeedingAttribute"/> 与 <see cref="DataSeedingOptions"/> 逐个筛选已注册的种子。
/// </remarks>
public class DataSeederSelector : IDataSeederSelector
{
    private readonly IOptions<XiHanSqlSugarCoreOptions> _options;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="options">SqlSugarCore 选项</param>
    public DataSeederSelector(IOptions<XiHanSqlSugarCoreOptions> options)
    {
        _options = options;
    }

    /// <summary>
    /// 从已注册的种子中选出当前库需要执行的种子
    /// </summary>
    /// <param name="seeders">已注册的种子</param>
    /// <param name="context">当前库上下文</param>
    /// <returns>需要执行的种子</returns>
    public virtual IReadOnlyList<IDataSeeder> Select(IReadOnlyList<IDataSeeder> seeders, DbInitializationContext context)
    {
        ArgumentNullException.ThrowIfNull(seeders);
        ArgumentNullException.ThrowIfNull(context);

        var selection = _options.Value.DataSeeding;
        return [.. seeders.Where(seeder => ShouldSeed(seeder, selection, context))];
    }

    /// <summary>
    /// 判断种子是否参与当前库的播种
    /// </summary>
    /// <param name="seeder">种子</param>
    /// <param name="selection">种子选取选项</param>
    /// <param name="context">当前库上下文</param>
    /// <returns>参与返回 true</returns>
    protected virtual bool ShouldSeed(IDataSeeder seeder, DataSeedingOptions selection, DbInitializationContext context)
    {
        var seederType = ProxyHelper.GetUnProxiedType(seeder);
        var attribute = seederType.GetCustomAttribute<DataSeedingAttribute>(inherit: true);
        if (attribute is not null)
        {
            if (!attribute.Enabled ||
                !DbInitializationFilters.IsTargetAllowed(attribute.Target, context) ||
                !DbInitializationFilters.IsConnectionAllowed(attribute.ConnectionConfigIds, context))
            {
                return false;
            }
        }
        else if (selection.Mode == DbInitializationMode.OptIn)
        {
            return false;
        }

        if (!DbInitializationFilters.IsGroupAllowed(attribute?.Group, selection.IncludedGroups, selection.ExcludedGroups))
        {
            return false;
        }

        if (selection.IncludedSeeders.Count > 0 &&
            !DbInitializationFilters.MatchesAny(selection.IncludedSeeders, seeder.Name, seederType.Name, seederType.FullName))
        {
            return false;
        }

        return !DbInitializationFilters.MatchesAny(selection.ExcludedSeeders, seeder.Name, seederType.Name, seederType.FullName) &&
               (selection.Filter is null || selection.Filter(seeder));
    }
}
