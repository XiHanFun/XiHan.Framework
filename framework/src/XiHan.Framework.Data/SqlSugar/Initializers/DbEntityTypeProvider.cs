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
/// 再按数据源声明、<see cref="TableInitializationAttribute"/> 与 <see cref="TableInitializationOptions"/> 逐个筛选。
/// </remarks>
public class DbEntityTypeProvider : IDbEntityTypeProvider
{
    private readonly IOptions<XiHanSqlSugarCoreOptions> _options;
    private readonly IEntityDataSourceResolver _dataSourceResolver;
    private readonly Lazy<IReadOnlyList<Type>> _candidateEntityTypes;
    private readonly Lazy<HashSet<string>> _dataSourceConfigIds;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="options">SqlSugarCore 选项</param>
    /// <param name="dataSourceResolver">实体数据源解析器</param>
    public DbEntityTypeProvider(IOptions<XiHanSqlSugarCoreOptions> options, IEntityDataSourceResolver dataSourceResolver)
    {
        _options = options;
        _dataSourceResolver = dataSourceResolver;
        _candidateEntityTypes = new Lazy<IReadOnlyList<Type>>(ScanEntityTypes, LazyThreadSafetyMode.ExecutionAndPublication);
        _dataSourceConfigIds = new Lazy<HashSet<string>>(ScanDataSourceConfigIds, LazyThreadSafetyMode.ExecutionAndPublication);
    }

    /// <summary>
    /// 全部候选实体类型（未经选取规则筛选）
    /// </summary>
    public IReadOnlyList<Type> CandidateEntityTypes => _candidateEntityTypes.Value;

    /// <summary>
    /// 被候选实体声明为数据源的连接配置标识（即模块专属库）
    /// </summary>
    public IReadOnlyCollection<string> DataSourceConfigIds => _dataSourceConfigIds.Value;

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
        if (!IsDataSourceAllowed(entityType, selection, context))
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
    /// 判断实体与当前库的数据源归属是否匹配
    /// </summary>
    /// <remarks>
    /// 声明了数据源的实体只在自己的库建表；未声明数据源的实体不进模块专属库，
    /// 除非该连接被 <see cref="TableInitializationOptions.SharedConnectionConfigIds"/> 放行。
    /// 当前连接标识未知时一律放行。
    /// </remarks>
    /// <param name="entityType">实体类型</param>
    /// <param name="selection">建表选取选项</param>
    /// <param name="context">当前库上下文</param>
    /// <returns>匹配返回 true</returns>
    protected virtual bool IsDataSourceAllowed(Type entityType, TableInitializationOptions selection, DbInitializationContext context)
    {
        var declaredConfigId = _dataSourceResolver.ResolveConfigId(entityType)?.Trim();
        var currentConfigId = context.ConnectionConfigId?.Trim();

        if (string.IsNullOrWhiteSpace(currentConfigId))
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(declaredConfigId))
        {
            return string.Equals(declaredConfigId, currentConfigId, StringComparison.OrdinalIgnoreCase);
        }

        return !_dataSourceConfigIds.Value.Contains(currentConfigId) ||
               DbInitializationFilters.MatchesAny(selection.SharedConnectionConfigIds, currentConfigId);
    }

    /// <summary>
    /// 扫描全部候选实体类型
    /// </summary>
    /// <returns>实体类型集合</returns>
    private static IReadOnlyList<Type> ScanEntityTypes()
    {
        return [.. ReflectionHelper.GetContainsAttributeSubClasses<IEntityBase, SugarTable>()];
    }

    /// <summary>
    /// 扫描候选实体声明的全部数据源连接配置标识
    /// </summary>
    /// <returns>连接配置标识集合</returns>
    private HashSet<string> ScanDataSourceConfigIds()
    {
        return new HashSet<string>(
            CandidateEntityTypes
                .Select(entityType => _dataSourceResolver.ResolveConfigId(entityType)?.Trim())
                .Where(configId => !string.IsNullOrWhiteSpace(configId))
                .Select(configId => configId!),
            StringComparer.OrdinalIgnoreCase);
    }
}
