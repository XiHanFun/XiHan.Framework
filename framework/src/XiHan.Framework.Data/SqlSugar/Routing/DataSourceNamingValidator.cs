// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Options;
using XiHan.Framework.Data.SqlSugar.Options;

namespace XiHan.Framework.Data.SqlSugar.Routing;

/// <summary>
/// 数据源命名校验器：在启动期把「数据源槽位」与「租户槽位」的命名冲突暴露出来
/// </summary>
/// <remarks>
/// 租户解析会拿租户 Id 的字符串形式、<c>{租户前缀}{租户Id}</c> 与租户名称去匹配 ConfigId。
/// 数据源名一旦落进这些形态，两条维度就会在同一个名字上撞车。运行期表现是「数据写进了另一个库」
/// 且不报错，极难排查，因此在启动期直接拒绝。
/// </remarks>
public class DataSourceNamingValidator
{
    private readonly IDataSourceRegistry _registry;
    private readonly XiHanSqlSugarCoreOptions _options;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="registry">数据源注册表</param>
    /// <param name="options">SqlSugarCore 选项</param>
    public DataSourceNamingValidator(IDataSourceRegistry registry, IOptions<XiHanSqlSugarCoreOptions> options)
    {
        _registry = registry;
        _options = options.Value;
    }

    /// <summary>
    /// 校验全部数据源名，发现冲突即抛异常
    /// </summary>
    /// <exception cref="InvalidOperationException">存在与租户槽位冲突的数据源名</exception>
    public void Validate()
    {
        var problems = new List<string>();

        foreach (var name in _registry.DataSourceNames)
        {
            // 纯数字会被租户解析的第一步 tenantId.ToString() 命中
            if (long.TryParse(name, out _))
            {
                problems.Add($"[{name}]：纯数字数据源名会与租户 Id 的解析形态撞车");
            }

            // 带租户前缀的名字会被 {前缀}{租户Id} 那一步命中
            if (!string.IsNullOrWhiteSpace(_options.TenantConfigIdPrefix) &&
                name.StartsWith(_options.TenantConfigIdPrefix, StringComparison.OrdinalIgnoreCase))
            {
                problems.Add($"[{name}]：以租户连接前缀 [{_options.TenantConfigIdPrefix}] 开头，会与租户独立库的命名撞车");
            }

            // 与默认连接同名会让默认连接被排除出租户槽位
            if (string.Equals(name, _options.DefaultConfigId, StringComparison.OrdinalIgnoreCase))
            {
                problems.Add($"[{name}]：与默认连接标识 DefaultConfigId 同名，会把默认连接挤出租户解析范围");
            }
        }

        if (problems.Count > 0)
        {
            throw new InvalidOperationException(
                "数据源命名与租户连接解析冲突，已在启动期拒绝：" + Environment.NewLine +
                string.Join(Environment.NewLine, problems.Select(problem => "  - " + problem)) + Environment.NewLine +
                "数据源名请使用不与租户 Id / 租户前缀 / 默认连接重合的业务名（如 Erp、Crm、Mes）。");
        }
    }
}
