// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Data.SqlSugar.Routing;

/// <summary>
/// 实体数据源解析器
/// </summary>
/// <remarks>
/// 决定某个实体固定落在哪个连接配置上。默认实现按实体特性解析，
/// 需要改用别的来源（配置文件、模块清单等）时，用 <c>services.Replace</c> 替换本服务；
/// 仓储路由与建表初始化共用同一份解析结果。
/// </remarks>
public interface IEntityDataSourceResolver
{
    /// <summary>
    /// 解析实体声明的连接配置标识
    /// </summary>
    /// <param name="entityType">实体类型</param>
    /// <returns>连接配置标识；未声明数据源返回 null，表示按当前租户上下文解析连接</returns>
    string? ResolveConfigId(Type entityType);
}
