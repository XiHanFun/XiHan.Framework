// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Data;

namespace XiHan.Framework.Uow.Options;

/// <summary>
/// 工作单元选项接口
/// </summary>
public interface IXiHanUnitOfWorkOptions
{
    /// <summary>
    /// 是否启用事务
    /// </summary>
    bool IsTransactional { get; }

    /// <summary>
    /// 事务隔离级别
    /// </summary>
    IsolationLevel? IsolationLevel { get; }

    /// <summary>
    /// 超时时间
    /// </summary>
    int? Timeout { get; }
}
