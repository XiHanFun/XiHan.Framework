// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Data.SqlSugar.Seeders;

namespace XiHan.Framework.Data.SqlSugar.Initializers;

/// <summary>
/// 种子选取器接口
/// </summary>
/// <remarks>
/// 决定某个库该跑哪些种子。默认实现按特性与选项筛选已注册的种子，
/// 需要完全自定义（如按业务开关决定播种范围）时，用 <c>services.Replace</c> 替换本服务。
/// </remarks>
public interface IDataSeederSelector
{
    /// <summary>
    /// 从已注册的种子中选出当前库需要执行的种子
    /// </summary>
    /// <param name="seeders">已注册的种子</param>
    /// <param name="context">当前库上下文</param>
    /// <returns>需要执行的种子</returns>
    IReadOnlyList<IDataSeeder> Select(IReadOnlyList<IDataSeeder> seeders, DbInitializationContext context);
}
