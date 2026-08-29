// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Data.SqlSugar.Routing;

/// <summary>
/// 实体模块数据源解析器
/// </summary>
/// <remarks>
/// 只回答「这个实体属于哪个<b>模块数据源</b>」，不回答「落到哪条连接」——
/// 后者由 <see cref="IModuleDataSourceConnectionResolver"/> 结合当前租户所在的布局解析。
/// 默认实现按实体特性解析；需要改用别的来源（配置文件、模块清单等）时用 <c>services.Replace</c> 替换。
/// </remarks>
public interface IEntityModuleDataSourceResolver
{
    /// <summary>
    /// 解析实体声明的模块数据源名
    /// </summary>
    /// <param name="entityType">实体类型</param>
    /// <returns>模块数据源名；未声明返回 null，表示落当前租户的主库</returns>
    string? ResolveModuleDataSource(Type entityType);
}
