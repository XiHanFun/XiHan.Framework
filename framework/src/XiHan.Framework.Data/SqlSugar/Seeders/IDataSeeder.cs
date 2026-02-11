#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IDataSeeder
// Guid:3f4e5d6c-7a8b-9c0d-1e2f-3a4b5c6d7e8f
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/01/05 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

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
