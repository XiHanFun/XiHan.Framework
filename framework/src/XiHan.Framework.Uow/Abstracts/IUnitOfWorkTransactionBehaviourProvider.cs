// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Uow.Abstracts;

/// <summary>
/// 工作单元事务行为提供者接口
/// </summary>
public interface IUnitOfWorkTransactionBehaviourProvider
{
    /// <summary>
    /// 是否事务性
    /// </summary>
    bool? IsTransactional { get; }
}
