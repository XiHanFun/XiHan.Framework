#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:OperationType
// Guid:0bb4ba7a-182a-4a74-aee2-20eed339b5d8
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/3/17 13:56:10
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Enums;

/// <summary>
/// 操作类型
/// </summary>
public enum OperationType
{
    /// <summary>
    /// 查询
    /// </summary>
    Query = 0,

    /// <summary>
    /// 创建
    /// </summary>
    Create = 1,

    /// <summary>
    /// 更新
    /// </summary>
    Update = 2,

    /// <summary>
    /// 删除
    /// </summary>
    Delete = 3,

    /// <summary>
    /// 导入
    /// </summary>
    Import = 4,

    /// <summary>
    /// 导出
    /// </summary>
    Export = 5,

    /// <summary>
    /// 审核
    /// </summary>
    Audit = 6,

    /// <summary>
    /// 授权
    /// </summary>
    Authorize = 7
}
