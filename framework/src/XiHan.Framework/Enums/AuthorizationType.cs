#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:AuthorizationType
// Guid:0bb4ba7a-182a-4a74-aee2-20eed339b5d5
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/3/17 13:56:10
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Enums;

/// <summary>
/// 授权类型
/// </summary>
public enum AuthorizationType
{
    /// <summary>
    /// 基于角色
    /// </summary>
    Role = 0,

    /// <summary>
    /// 基于策略
    /// </summary>
    Policy = 1,

    /// <summary>
    /// 基于声明
    /// </summary>
    Claim = 2,

    /// <summary>
    /// 基于资源
    /// </summary>
    Resource = 3,

    /// <summary>
    /// 基于操作
    /// </summary>
    Operation = 4
}
