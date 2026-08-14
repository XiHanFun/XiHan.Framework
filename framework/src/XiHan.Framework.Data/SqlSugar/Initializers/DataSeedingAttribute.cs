// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Data.SqlSugar.Initializers;

/// <summary>
/// 声明种子参与播种的方式
/// </summary>
/// <remarks>
/// 不标注时按 <c>XiHanSqlSugarCoreOptions.DataSeeding.Mode</c> 处理：<c>All</c> 参与、<c>OptIn</c> 不参与。
/// 标注在种子基类上对派生种子同样生效。
/// </remarks>
[AttributeUsage(AttributeTargets.Class, Inherited = true, AllowMultiple = false)]
public sealed class DataSeedingAttribute : Attribute
{
    /// <summary>
    /// 构造函数
    /// </summary>
    public DataSeedingAttribute()
    {
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="enabled">是否参与播种</param>
    public DataSeedingAttribute(bool enabled)
    {
        Enabled = enabled;
    }

    /// <summary>
    /// 是否参与播种
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// 所属分组，配合 <c>DataSeeding.IncludedGroups</c> / <c>ExcludedGroups</c> 按组开关
    /// </summary>
    public string? Group { get; set; }

    /// <summary>
    /// 播种目标库，默认平台库与租户独立库都播
    /// </summary>
    public DbInitializationTarget Target { get; set; } = DbInitializationTarget.All;

    /// <summary>
    /// 仅在这些连接配置标识上播种，为空表示不限连接
    /// </summary>
    public string[] ConnectionConfigIds { get; set; } = [];
}
