#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IUnitOfWorkTransactionBehaviourProvider
// Guid:e6d49b3c-c158-41e3-8128-71f82130be8a
// Author:afand
// Email:me@zhaifanhua.com
// CreateTime:2025/4/1 21:09:59
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Uow.Abstracts;

/// <summary>
/// 工作单元事务行为提供者
/// </summary>
public interface IUnitOfWorkTransactionBehaviourProvider
{
    /// <summary>
    /// 是否事务性
    /// </summary>
    bool? IsTransactional { get; }
}
