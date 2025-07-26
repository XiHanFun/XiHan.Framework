#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:EnvironmentType
// Guid:0bb4ba7a-182a-4a74-aee2-20eed339b5d1
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/3/17 13:56:10
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Enums;

/// <summary>
/// 环境类型
/// </summary>
public enum EnvironmentType
{
    /// <summary>
    /// 开发环境
    /// </summary>
    Development = 0,

    /// <summary>
    /// 测试环境
    /// </summary>
    Test = 1,

    /// <summary>
    /// 预生产环境
    /// </summary>
    Staging = 2,

    /// <summary>
    /// 生产环境
    /// </summary>
    Production = 3
}
