// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Options;
using SqlSugar;
using System.Reflection;
using XiHan.Framework.Data.SqlSugar.Options;
using XiHan.Framework.Data.SqlSugar.Routing;
using XiHan.Framework.Domain.Entities.Abstracts;
using XiHan.Framework.Utils.Reflections;

namespace XiHan.Framework.Data.SqlSugar.Initializers;

/// <summary>
/// 建表实体提供器默认实现
/// </summary>
/// <remarks>
/// 扫描全部标注 <see cref="SugarTable"/> 的 <see cref="IEntityBase"/> 实体作为候选（扫描结果缓存），
/// 再按模块数据源声明、<see cref="TableInitializationAttribute"/> 与 <see cref="TableInitializationOptions"/> 逐个筛选。
/// </remarks>
public class DbEntityTypeProvider : IDbEntityTypeProvider
{
    private readonly IOptions<XiHanSqlSugarCoreOptions> _options;
    private readonly IEntityModuleDataSourceResolver _moduleDataSourceResolver;
    private readonly Lazy<IReadOnlyList<Type>> _candidateEntityTypes;
    private readonly Lazy<IReadOnlyList<string>> _moduleDataSourceNames;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="options">SqlSugarCore 选项</param>
    /// <param name="moduleDataSourceResolver">实体模块数据源解析器</param>
    public DbEntityTypeProvider(IOptions<XiHanSqlSugarCoreOptions> options, IEntityModuleDataSourceResolver moduleDataSourceResolver)
    {
        _options = options;
        _moduleDataSourceResolver = moduleDataSourceResolver;
        _candidateEntityTypes = new Lazy<IReadOnlyList<Type>>(ScanEntityTypes, LazyThreadSafetyMode.ExecutionAndPublication);
        _moduleDataSourceNames = new Lazy<IReadOnlyList<string>>(CollectModuleDataSourceNames, LazyThreadSafetyMode.ExecutionAndPublication);
    }

    /// <summary>
    /// 全部候选实体类型（未经选取规则筛选）
    /// </summary>
    public IReadOnlyList<Type> CandidateEntityTypes => _candidateEntityTypes.Value;

    /// <summary>
    /// 获取当前库需要建表的实体类型
    /// </summary>
    /// <param name="context">当前库上下文</param>
    /// <returns>实体类型集合</returns>
    public virtual IReadOnlyList<Type> GetEntityTypes(DbInitializationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var selection = _options.Value.TableInitialization;
        return [.. CandidateEntityTypes.Where(entityType => ShouldInitialize(entityType, selection, context))];
    }

    /// <summary>
    /// 判断实体是否参与当前库的建表
    /// </summary>
    /// <param name="entityType">实体类型</param>
    /// <param name="selection">建表选取选项</param>
    /// <param name="context">当前库上下文</param>
    /// <returns>参与返回 true</returns>
    protected virtual bool ShouldInitialize(Type entityType, TableInitializationOptions selection, DbInitializationContext context)
    {
        if (!IsModuleDataSourceAllowed(entityType, selection, context))
        {
            return false;
        }

        var attribute = entityType.GetCustomAttribute<TableInitializationAttribute>(inherit: true);
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

        var tableName = entityType.GetCustomAttribute<SugarTable>(inherit: true)?.TableName;
        if (selection.IncludedTables.Count > 0 &&
            !DbInitializationFilters.MatchesAny(selection.IncludedTables, entityType.Name, entityType.FullName, tableName))
        {
            return false;
        }

        return !DbInitializationFilters.MatchesAny(selection.ExcludedTables, entityType.Name, entityType.FullName, tableName) &&
               (selection.Filter is null || selection.Filter(entityType));
    }

    /// <summary>
    /// 判断实体与当前库的模块数据源归属是否匹配
    /// </summary>
    /// <remarks>
    /// 模块库的 ConfigId 由父连接派生（形如 <c>Default_Erp</c>、<c>Tenant_1001_Erp</c>），
    /// 所以声明了模块数据源的实体，在所有以该模块名结尾的连接上都要建表——每套布局各建一份；
    /// 未声明的实体不进模块库，除非该连接被 <see cref="TableInitializationOptions.SharedConnectionConfigIds"/> 放行。
    /// 当前连接标识未知时一律放行。
    /// </remarks>
    /// <param name="entityType">实体类型</param>
    /// <param name="selection">建表选取选项</param>
    /// <param name="context">当前库上下文</param>
    /// <returns>匹配返回 true</returns>
    protected virtual bool IsModuleDataSourceAllowed(Type entityType, TableInitializationOptions selection, DbInitializationContext context)
    {
        var declaredName = _moduleDataSourceResolver.ResolveModuleDataSource(entityType)?.Trim();
        var currentConfigId = context.ConnectionConfigId?.Trim();

        if (string.IsNullOrWhiteSpace(currentConfigId))
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(declaredName))
        {
            // 相等一条留给 SqlSugar 原生 TenantAttribute：它声明的本就是连接标识本身
            return IsModuleConnection(currentConfigId, declaredName) ||
                   string.Equals(declaredName, currentConfigId, StringComparison.OrdinalIgnoreCase);
        }

        return !IsAnyModuleConnection(currentConfigId) ||
               DbInitializationFilters.MatchesAny(selection.SharedConnectionConfigIds, currentConfigId);
    }

    /// <summary>
    /// 判断某个连接标识是否为指定模块数据源派生出的模块库
    /// </summary>
    /// <param name="configId">连接配置标识</param>
    /// <param name="moduleDataSource">模块数据源名</param>
    /// <returns>是返回 true</returns>
    private static bool IsModuleConnection(string configId, string moduleDataSource)
    {
        var suffix = $"{ModuleDataSourceConfigIds.Separator}{moduleDataSource}";
        return configId.EndsWith(suffix, StringComparison.OrdinalIgnoreCase) &&
               configId.Length > suffix.Length;
    }

    /// <summary>
    /// 扫描全部候选实体类型
    /// </summary>
    /// <returns>实体类型集合</returns>
    private static IReadOnlyList<Type> ScanEntityTypes()
    {
        return [.. ReflectionHelper.GetContainsAttributeTypes<SugarTable>()];
    }

    /// <summary>
    /// 判断某个连接标识是否为任一模块数据源派生出的模块库
    /// </summary>
    /// <param name="configId">连接配置标识</param>
    /// <returns>是返回 true</returns>
    private bool IsAnyModuleConnection(string configId)
    {
        return _moduleDataSourceNames.Value.Any(name => IsModuleConnection(configId, name));
    }

    /// <summary>
    /// 收集配置中出现过的全部模块数据源名
    /// </summary>
    /// <returns>模块数据源名集合</returns>
    private IReadOnlyList<string> CollectModuleDataSourceNames()
    {
        return [.. _options.Value.ConnectionConfigs
            .Where(connectionConfig => connectionConfig.ModuleDataSourceConfigs is { Count: > 0 })
            .SelectMany(connectionConfig => connectionConfig.ModuleDataSourceConfigs!)
            .Select(moduleConfig => moduleConfig.ModuleDataSource?.Trim())
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name!)
            .Distinct(StringComparer.OrdinalIgnoreCase)];
    }
}
