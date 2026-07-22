// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

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
