// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Data.SqlSugar.Seeders;

/// <summary>
/// 数据种子接口
/// </summary>
public interface IDataSeeder
{
    /// <summary>
    /// 种子数据优先级（数字越小优先级越高）
    /// </summary>
    int Order { get; }

    /// <summary>
    /// 种子数据名称
    /// </summary>
    string Name { get; }

    /// <summary>
    /// 种子数据
    /// </summary>
    Task SeedAsync();
}
