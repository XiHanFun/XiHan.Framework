// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Data.SqlSugar.Initializers;

/// <summary>
/// 建表实体提供器接口
/// </summary>
/// <remarks>
/// 决定某个库该建哪些表。默认实现按特性与选项筛选扫描到的实体，
/// 需要完全自定义（如从配置/清单文件读取建表范围）时，用 <c>services.Replace</c> 替换本服务。
/// </remarks>
public interface IDbEntityTypeProvider
{
    /// <summary>
    /// 获取当前库需要建表的实体类型
    /// </summary>
    /// <param name="context">当前库上下文</param>
    /// <returns>实体类型集合</returns>
    IReadOnlyList<Type> GetEntityTypes(DbInitializationContext context);
}
