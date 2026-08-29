// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Data.SqlSugar.Routing;

/// <summary>
/// 数据源注册表：全仓被实体声明过的逻辑数据源名
/// </summary>
/// <remarks>
/// 它把 <c>ConfigId</c> 命名空间切成互不相交的两块——「数据源槽位」与「租户槽位」：
/// 数据源解析只在前者里挑，租户解析只在后者里挑。少了这条边界，
/// 一个名称恰好等于某数据源名的租户，其未声明数据源的实体会被静默路由进该模块库。
/// </remarks>
public interface IDataSourceRegistry
{
    /// <summary>
    /// 全部被声明过的逻辑数据源名（忽略大小写）
    /// </summary>
    IReadOnlyCollection<string> DataSourceNames { get; }

    /// <summary>
    /// 判断某个名称是否为已声明的数据源名
    /// </summary>
    /// <param name="name">待判定的名称</param>
    /// <returns>是数据源名返回 true</returns>
    bool IsDataSource(string? name);
}
