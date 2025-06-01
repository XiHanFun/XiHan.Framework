#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:NullUnitOfWorkTransactionBehaviourProvider
// Guid:3b99dc5c-d4cd-4271-ac77-36047e206f82
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/6/2 5:02:31
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Core.DependencyInjection.ServiceLifetimes;
using XiHan.Framework.Uow.Abstracts;

namespace XiHan.Framework.Uow;

/// <summary>
/// 默认空工作单元事务行为提供者
/// </summary>
/// <remarks>
/// 提供 IUnitOfWorkTransactionBehaviourProvider 的默认实现
/// 默认情况下不强制指定事务行为，由具体的工作单元配置决定
/// </remarks>
public class NullUnitOfWorkTransactionBehaviourProvider : IUnitOfWorkTransactionBehaviourProvider, ISingletonDependency
{
    /// <summary>
    /// 是否事务性
    /// </summary>
    /// <remarks>
    /// 返回 null 表示由工作单元的其他配置决定是否使用事务
    /// 具体的数据访问层实现可以重写此提供者来设置默认行为
    /// </remarks>
    public bool? IsTransactional => null;
}
