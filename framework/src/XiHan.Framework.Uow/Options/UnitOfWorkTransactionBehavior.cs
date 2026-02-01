#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:UnitOfWorkTransactionBehavior
// Guid:a527b463-3c07-4d0a-95b5-be8a0fa104b9
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/04/01 20:46:39
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Uow.Options;

/// <summary>
/// 工作单元事务行为
/// </summary>
public enum UnitOfWorkTransactionBehavior
{
    /// <summary>
    /// 自动
    /// </summary>
    Auto,

    /// <summary>
    /// 启用
    /// </summary>
    Enabled,

    /// <summary>
    /// 禁用
    /// </summary>
    Disabled
}
