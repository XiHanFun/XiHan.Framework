// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Data.SqlSugar.Initializers;
using XiHan.Framework.Data.SqlSugar.Seeders;

namespace XiHan.Framework.Data.SqlSugar.Options;

/// <summary>
/// 种子数据选取选项
/// </summary>
/// <remarks>
/// 只决定「哪些种子参与播种」，是否播种由 <c>XiHanSqlSugarCoreOptions.EnableDataSeeding</c> 总开关控制。
/// 判定顺序：种子特性 → 模式 → 分组 → 名称 → 自定义委托，任一环节否决即不播。
/// </remarks>
public class DataSeedingOptions
{
    /// <summary>
    /// 选取模式，默认 <see cref="DbInitializationMode.All"/>（注册的种子全部播种）
    /// </summary>
    public DbInitializationMode Mode { get; set; } = DbInitializationMode.All;

    /// <summary>
    /// 仅播这些分组的种子（对应 <see cref="DataSeedingAttribute.Group"/>），为空表示不限分组
    /// </summary>
    public List<string> IncludedGroups { get; set; } = [];

    /// <summary>
    /// 不播这些分组的种子
    /// </summary>
    public List<string> ExcludedGroups { get; set; } = [];

    /// <summary>
    /// 仅播这些种子，为空表示不限；支持 <c>*</c>、<c>?</c> 通配，按 <see cref="IDataSeeder.Name"/>、种子类名、种子全名任一匹配即算命中
    /// </summary>
    public List<string> IncludedSeeders { get; set; } = [];

    /// <summary>
    /// 不播这些种子，匹配规则同 <see cref="IncludedSeeders"/>
    /// </summary>
    public List<string> ExcludedSeeders { get; set; } = [];

    /// <summary>
    /// 自定义筛选委托，仅支持代码方式注册；返回 false 表示不播该种子
    /// </summary>
    public Func<IDataSeeder, bool>? Filter { get; set; }
}
