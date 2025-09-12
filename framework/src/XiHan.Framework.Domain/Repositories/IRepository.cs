#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IRepository
// Guid:f17d5dcc-34fb-42bb-9702-02de950d0360
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/6/2 6:23:35
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Domain.Aggregates.Abstracts;

namespace XiHan.Framework.Domain.Repositories;

/// <summary>
/// 聚合根仓储接口
/// </summary>
/// <typeparam name="TAggregateRoot">聚合根类型</typeparam>
/// <typeparam name="TKey">主键类型</typeparam>
public interface IRepository<TAggregateRoot, TKey> : IRepositoryBase<TAggregateRoot, TKey>
    where TAggregateRoot : class, IAggregateRoot<TKey>
    where TKey : IEquatable<TKey>
{
    /// <summary>
    /// 工作单元
    /// 用于事务控制和领域事件的统一提交
    /// </summary>
    IUnitOfWork UnitOfWork { get; }
}
