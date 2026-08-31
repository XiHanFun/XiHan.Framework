// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Options;
using SqlSugar;
using XiHan.Framework.Core.Exceptions;
using XiHan.Framework.Data.SqlSugar.Options;

namespace XiHan.Framework.Data.SqlSugar.Routing;

/// <summary>
/// 模块数据源连接解析器默认实现
/// </summary>
/// <remarks>
/// 解析链（自上而下，命中即返回）：
/// <list type="number">
///   <item>当前布局下的模块库 <c>{父ConfigId}_{模块名}</c>：租户自带该模块的独立库时命中；</item>
///   <item>默认布局下的模块库 <c>{DefaultConfigId}_{模块名}</c>：所有租户共享的模块库，
///         「租户主库独立但模块库仍共享」这种组合走这一步；</item>
///   <item>都没有：fail-closed 抛异常，绝不回落主库。</item>
/// </list>
/// 第 1、2 步在平台态会指向同一个连接，等价于直接取共享模块库。
/// </remarks>
public sealed class ModuleDataSourceConnectionResolver : IModuleDataSourceConnectionResolver
{
    private readonly SqlSugarScope _sqlSugarScope;
    private readonly XiHanSqlSugarCoreOptions _options;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="sqlSugarScope">SqlSugar 根作用域</param>
    /// <param name="options">SqlSugarCore 选项</param>
    public ModuleDataSourceConnectionResolver(SqlSugarScope sqlSugarScope, IOptions<XiHanSqlSugarCoreOptions> options)
    {
        _sqlSugarScope = sqlSugarScope;
        _options = options.Value;
    }

    /// <summary>
    /// 解析模块数据源在当前租户布局下对应的客户端
    /// </summary>
    /// <param name="moduleDataSource">模块数据源名</param>
    /// <param name="parentConfigId">当前租户所在布局的父连接 ConfigId</param>
    /// <returns>该模块库的 Scope 级客户端</returns>
    public ISqlSugarClient ResolveClient(string moduleDataSource, string parentConfigId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleDataSource);
        ArgumentException.ThrowIfNullOrWhiteSpace(parentConfigId);

        var name = moduleDataSource.Trim();

        // ① 当前布局自带的模块库
        var scopedConfigId = ModuleDataSourceConfigIds.Build(parentConfigId, name);
        if (_sqlSugarScope.IsAnyConnection(scopedConfigId))
        {
            return _sqlSugarScope.GetConnectionScope(scopedConfigId);
        }

        // ② 默认布局下的共享模块库
        if (!string.IsNullOrWhiteSpace(_options.DefaultConfigId))
        {
            var sharedConfigId = ModuleDataSourceConfigIds.Build(_options.DefaultConfigId, name);
            if (_sqlSugarScope.IsAnyConnection(sharedConfigId))
            {
                return _sqlSugarScope.GetConnectionScope(sharedConfigId);
            }
        }

        // ③ fail-closed：绝不回落主库，否则是静默跨库串写
        throw new XiHanException(
            $"模块数据源 [{name}] 没有对应的连接配置，已按 fail-closed 拒绝请求。" +
            $"请在 {XiHanSqlSugarCoreOptions.SectionName}:ConnectionConfigs 中为 [{_options.DefaultConfigId}] " +
            $"补一条 ModuleDataSourceConfigs 条目（ModuleDataSource = {name}）；" +
            $"若该模块不需要分库，把它的 ConnectionString 留空即可。");
    }
}
