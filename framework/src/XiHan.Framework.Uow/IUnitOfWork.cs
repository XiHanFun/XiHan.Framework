#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IUnitOfWork
// Guid:2b5f6c5c-ef6e-4bee-ab01-03b1f01e19e9
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/14 6:27:31
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Uow.Abstracts;
using XiHan.Framework.Uow.Options;

namespace XiHan.Framework.Uow;

/// <summary>
/// 工作单元接口
/// </summary>
public interface IUnitOfWork : IDatabaseApiContainer, ITransactionApiContainer, IDisposable
{
    //TODO：切换到OnFailed （sync）和ondispose （sync）方法来兼容OnCompleted
    /// <summary>
    /// 工作单元失败事件
    /// </summary>
    event EventHandler<UnitOfWorkFailedEventArgs> Failed;

    /// <summary>
    /// 工作单元完成事件
    /// </summary>
    event EventHandler<UnitOfWorkEventArgs> Disposed;

    /// <summary>
    /// 工作单元ID
    /// </summary>
    Guid Id { get; }

    /// <summary>
    /// 工作单元项
    /// </summary>
    Dictionary<string, object> Items { get; }

    /// <summary>
    /// 工作单元选项
    /// </summary>
    IXiHanUnitOfWorkOptions Options { get; }

    /// <summary>
    /// 外部工作单元
    /// </summary>
    IUnitOfWork? Outer { get; }

    /// <summary>
    /// 是否已保留
    /// </summary>
    bool IsReserved { get; }

    /// <summary>
    /// 是否已释放
    /// </summary>
    bool IsDisposed { get; }

    /// <summary>
    /// 是否已完成
    /// </summary>
    bool IsCompleted { get; }

    /// <summary>
    /// 订阅名称
    /// </summary>
    string? ReservationName { get; }

    /// <summary>
    /// 设置外部工作单元
    /// </summary>
    /// <param name="outer"></param>
    void SetOuter(IUnitOfWork? outer);

    /// <summary>
    /// 初始化
    /// </summary>
    /// <param name="options"></param>
    void Initialize(XiHanUnitOfWorkOptions options);

    /// <summary>
    /// 开始
    /// </summary>
    /// <param name="reservationName"></param>
    void Reserve(string reservationName);

    /// <summary>
    /// 异步保存更改
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 异步完成
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task CompleteAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 异步回滚
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task RollbackAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 完成
    /// </summary>
    /// <param name="handler"></param>
    void OnCompleted(Func<Task> handler);

    /// <summary>
    /// 添加或替换本地事件
    /// </summary>
    /// <param name="eventRecord"></param>
    /// <param name="replacementSelector"></param>
    void AddOrReplaceLocalEvent(UnitOfWorkEventRecord eventRecord, Predicate<UnitOfWorkEventRecord>? replacementSelector = null);

    /// <summary>
    /// 添加或替换分布式事件
    /// </summary>
    /// <param name="eventRecord"></param>
    /// <param name="replacementSelector"></param>
    void AddOrReplaceDistributedEvent(UnitOfWorkEventRecord eventRecord, Predicate<UnitOfWorkEventRecord>? replacementSelector = null);
}
