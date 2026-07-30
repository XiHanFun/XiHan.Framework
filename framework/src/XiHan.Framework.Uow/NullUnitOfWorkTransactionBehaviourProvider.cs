// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

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
