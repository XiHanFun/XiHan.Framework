// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Data.SqlSugar.Initializers;

namespace XiHan.Framework.Data.SqlSugar.Options;

/// <summary>
/// 建表初始化选取选项
/// </summary>
/// <remarks>
/// 只决定「哪些实体参与建表」，是否建表由 <c>XiHanSqlSugarCoreOptions.EnableTableInitialization</c> 总开关控制。
/// 判定顺序：数据源 → 实体特性 → 模式 → 分组 → 名称 → 自定义委托，任一环节否决即不建。
/// </remarks>
public class TableInitializationOptions
{
    /// <summary>
    /// 模块专属库中额外允许建「未声明数据源实体」的连接名单，支持 <c>*</c>、<c>?</c> 通配
    /// </summary>
    /// <remarks>
    /// 被任一实体经 <c>[DataSource("XXX")]</c> 声明的连接即模块专属库，默认只建声明了该数据源的实体的表；
    /// 模块库还需要框架公共表（审计、字典等）时，把该 ConfigId 列入本名单。
    /// </remarks>
    public List<string> SharedConnectionConfigIds { get; set; } = [];

    /// <summary>
    /// 选取模式，默认 <see cref="DbInitializationMode.All"/>（扫描到的实体全部建表）
    /// </summary>
    public DbInitializationMode Mode { get; set; } = DbInitializationMode.All;

    /// <summary>
    /// 仅建这些分组的表（对应 <see cref="TableInitializationAttribute.Group"/>），为空表示不限分组
    /// </summary>
    public List<string> IncludedGroups { get; set; } = [];

    /// <summary>
    /// 不建这些分组的表
    /// </summary>
    public List<string> ExcludedGroups { get; set; } = [];

    /// <summary>
    /// 仅建这些表，为空表示不限；支持 <c>*</c>、<c>?</c> 通配，按实体类名、实体全名、表名任一匹配即算命中
    /// </summary>
    public List<string> IncludedTables { get; set; } = [];

    /// <summary>
    /// 不建这些表，匹配规则同 <see cref="IncludedTables"/>
    /// </summary>
    public List<string> ExcludedTables { get; set; } = [];

    /// <summary>
    /// 自定义筛选委托，仅支持代码方式注册；返回 false 表示不建该表
    /// </summary>
    public Func<Type, bool>? Filter { get; set; }
}
