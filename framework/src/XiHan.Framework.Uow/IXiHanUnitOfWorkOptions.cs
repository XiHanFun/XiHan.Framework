#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2024 ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IXiHanUnitOfWorkOptions
// Guid:4521fa4f-b62a-457f-9781-4950d1aa75e1
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/16 4:07:55
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Data;

namespace XiHan.Framework.Uow;

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
