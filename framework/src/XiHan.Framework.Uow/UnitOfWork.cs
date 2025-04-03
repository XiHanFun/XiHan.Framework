#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:UnitOfWork
// Guid:784f259f-d532-4941-9d81-ed8c0d9e5d8a
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/4/1 20:40:57
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.Options;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using XiHan.Framework.Core.DependencyInjection.ServiceLifetimes;
using XiHan.Framework.Core.Exceptions;
using XiHan.Framework.Uow.Abstracts;
using XiHan.Framework.Uow.Options;
using XiHan.Framework.Utils.Collections;
using XiHan.Framework.Utils.System;

namespace XiHan.Framework.Uow;

/// <summary>
/// 工作单元
/// </summary>
public class UnitOfWork : IUnitOfWork, ITransientDependency
{
    /// <summary>
    /// 工作单元保留名称
    /// </summary>
    public const string UnitOfWorkReservationName = "_XiHanActionUnitOfWork";

    // 数据库API
    private readonly Dictionary<string, IDatabaseApi> _databaseApis;

    // 事务API
    private readonly Dictionary<string, ITransactionApi> _transactionApis;

    // 默认选项
    private readonly XiHanUnitOfWorkDefaultOptions _defaultOptions;

    // 异常
    private Exception? _exception;

    // 是否正在完成
    private bool _isCompleting;

    // 是否已回滚
    private bool _isRolledback;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="serviceProvider"></param>
    /// <param name="unitOfWorkEventPublisher"></param>
    /// <param name="options"></param>
    public UnitOfWork(
        IServiceProvider serviceProvider,
        IUnitOfWorkEventPublisher unitOfWorkEventPublisher,
        IOptions<XiHanUnitOfWorkDefaultOptions> options)
    {
        ServiceProvider = serviceProvider;
        UnitOfWorkEventPublisher = unitOfWorkEventPublisher;
        _defaultOptions = options.Value;

        _databaseApis = [];
        _transactionApis = [];

        Items = [];
    }

    /// <summary>
    /// 工作单元失败事件
    /// </summary>
    public event EventHandler<UnitOfWorkFailedEventArgs> Failed = default!;

    /// <summary>
    /// 工作单元完成事件
    /// </summary>
    public event EventHandler<UnitOfWorkEventArgs> Disposed = default!;

    /// <summary>
    /// 是否启用过时的 DbContext 创建警告
    /// </summary>
    public static bool EnableObsoleteDbContextCreationWarning { get; } = false;

    /// <summary>
    /// 工作单元ID
    /// </summary>
    public Guid Id { get; } = Guid.NewGuid();

    /// <summary>
    /// 工作单元选项
    /// </summary>
    public IXiHanUnitOfWorkOptions Options { get; private set; } = default!;

    /// <summary>
    /// 外部工作单元
    /// </summary>
    public IUnitOfWork? Outer { get; private set; }

    /// <summary>
    /// 是否已保留
    /// </summary>
    public bool IsReserved { get; set; }

    /// <summary>
    /// 是否已释放
    /// </summary>
    public bool IsDisposed { get; private set; }

    /// <summary>
    /// 是否已完成
    /// </summary>
    public bool IsCompleted { get; private set; }

    /// <summary>
    /// 预留名称
    /// </summary>
    public string? ReservationName { get; set; }

    /// <summary>
    /// 服务提供程序
    /// </summary>
    public IServiceProvider ServiceProvider { get; }

    /// <summary>
    /// 工作单元项
    /// </summary>
    [NotNull]
    public Dictionary<string, object> Items { get; }

    /// <summary>
    /// 完成处理程序
    /// </summary>
    protected List<Func<Task>> CompletedHandlers { get; } = [];

    /// <summary>
    /// 分布式事件与谓词
    /// </summary>
    protected List<KeyValuePair<UnitOfWorkEventRecord, Predicate<UnitOfWorkEventRecord>?>> DistributedEventWithPredicates { get; } = [];

    /// <summary>
    /// 分布式事件
    /// </summary>
    protected List<UnitOfWorkEventRecord> DistributedEvents { get; } = [];

    /// <summary>
    /// 本地事件与谓词
    /// </summary>
    protected List<KeyValuePair<UnitOfWorkEventRecord, Predicate<UnitOfWorkEventRecord>?>> LocalEventWithPredicates { get; } = [];

    /// <summary>
    /// 本地事件
    /// </summary>
    protected List<UnitOfWorkEventRecord> LocalEvents { get; } = [];

    /// <summary>
    /// 工作单元事件发布者
    /// </summary>
    protected IUnitOfWorkEventPublisher UnitOfWorkEventPublisher { get; }

    /// <summary>
    /// 初始化
    /// </summary>
    /// <param name="options"></param>
    /// <exception cref="XiHanException"></exception>
    public virtual void Initialize([NotNull] XiHanUnitOfWorkOptions options)
    {
        CheckHelper.NotNull(options, nameof(options));

        if (Options != null)
        {
            throw new XiHanException("这个工作单元已经初始化了。");
        }

        Options = _defaultOptions.Normalize(options.Clone());
        IsReserved = false;
    }

    /// <summary>
    /// 预留
    /// </summary>
    /// <param name="reservationName"></param>
    public virtual void Reserve([NotNull] string reservationName)
    {
        CheckHelper.NotNullOrWhiteSpace(reservationName, nameof(reservationName));

        ReservationName = reservationName;
        IsReserved = true;
    }

    /// <summary>
    /// 设置外部工作单元
    /// </summary>
    /// <param name="outer"></param>
    public virtual void SetOuter(IUnitOfWork? outer)
    {
        Outer = outer;
    }

    /// <summary>
    /// 保存更改
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public virtual async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        if (_isRolledback)
        {
            return;
        }

        foreach (var databaseApi in GetAllActiveDatabaseApis())
        {
            if (databaseApi is ISupportsSavingChanges supportsSavingChangesDatabaseApi)
            {
                await supportsSavingChangesDatabaseApi.SaveChangesAsync(cancellationToken);
            }
        }
    }

    /// <summary>
    /// 获取所有活动数据库API
    /// </summary>
    /// <returns></returns>
    public virtual IReadOnlyList<IDatabaseApi> GetAllActiveDatabaseApis()
    {
        return _databaseApis.Values.ToImmutableList();
    }

    /// <summary>
    /// 获取所有活动事务API
    /// </summary>
    /// <returns></returns>
    public virtual IReadOnlyList<ITransactionApi> GetAllActiveTransactionApis()
    {
        return _transactionApis.Values.ToImmutableList();
    }

    /// <summary>
    /// 完成
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public virtual async Task CompleteAsync(CancellationToken cancellationToken = default)
    {
        if (_isRolledback)
        {
            return;
        }

        PreventMultipleComplete();

        try
        {
            _isCompleting = true;
            await SaveChangesAsync(cancellationToken);

            LocalEvents.AddRange(GetEventsRecords(LocalEventWithPredicates));
            LocalEventWithPredicates.Clear();
            DistributedEvents.AddRange(GetEventsRecords(DistributedEventWithPredicates));
            DistributedEventWithPredicates.Clear();

            while (LocalEvents.Count != 0 || DistributedEvents.Count != 0)
            {
                if (LocalEvents.Count != 0)
                {
                    var localEventsToBePublished = LocalEvents.OrderBy(e => e.EventOrder).ToArray();
                    LocalEvents.Clear();
                    await UnitOfWorkEventPublisher.PublishLocalEventsAsync(
                        localEventsToBePublished
                    );
                }

                if (DistributedEvents.Count != 0)
                {
                    var distributedEventsToBePublished = DistributedEvents.OrderBy(e => e.EventOrder).ToArray();
                    DistributedEvents.Clear();
                    await UnitOfWorkEventPublisher.PublishDistributedEventsAsync(
                        distributedEventsToBePublished
                    );
                }

                await SaveChangesAsync(cancellationToken);

                LocalEvents.AddRange(GetEventsRecords(LocalEventWithPredicates));
                LocalEventWithPredicates.Clear();
                DistributedEvents.AddRange(GetEventsRecords(DistributedEventWithPredicates));
                DistributedEventWithPredicates.Clear();
            }

            await CommitTransactionsAsync(cancellationToken);
            IsCompleted = true;
            await OnCompletedAsync();
        }
        catch (Exception ex)
        {
            _exception = ex;
            throw;
        }
    }

    /// <summary>
    /// 回滚
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public virtual async Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        if (_isRolledback)
        {
            return;
        }

        _isRolledback = true;

        await RollbackAllAsync(cancellationToken);
    }

    /// <summary>
    /// 获取数据库API
    /// </summary>
    /// <param name="key"></param>
    /// <returns></returns>
    public virtual IDatabaseApi? FindDatabaseApi([NotNull] string key)
    {
        return _databaseApis.GetOrDefault(key);
    }

    /// <summary>
    /// 添加数据库API
    /// </summary>
    /// <param name="key"></param>
    /// <param name="api"></param>
    /// <exception cref="XiHanException"></exception>
    public virtual void AddDatabaseApi([NotNull] string key, [NotNull] IDatabaseApi api)
    {
        CheckHelper.NotNullOrWhiteSpace(key, nameof(key));
        CheckHelper.NotNull(api, nameof(api));

        if (_databaseApis.ContainsKey(key))
        {
            throw new XiHanException("This unit of work already contains a database API for the given key.");
        }

        _databaseApis.Add(key, api);
    }

    /// <summary>
    /// 获取或添加数据库API
    /// </summary>
    /// <param name="key"></param>
    /// <param name="factory"></param>
    /// <returns></returns>
    public virtual IDatabaseApi GetOrAddDatabaseApi([NotNull] string key, [NotNull] Func<IDatabaseApi> factory)
    {
        CheckHelper.NotNullOrWhiteSpace(key, nameof(key));
        CheckHelper.NotNull(factory, nameof(factory));

        return _databaseApis.GetOrAdd(key, factory);
    }

    /// <summary>
    /// 查找事务API
    /// </summary>
    /// <param name="key"></param>
    /// <returns></returns>
    public virtual ITransactionApi? FindTransactionApi([NotNull] string key)
    {
        CheckHelper.NotNullOrWhiteSpace(key, nameof(key));

        return _transactionApis.GetOrDefault(key);
    }

    /// <summary>
    /// 添加事务API
    /// </summary>
    /// <param name="key"></param>
    /// <param name="api"></param>
    /// <exception cref="XiHanException"></exception>
    public virtual void AddTransactionApi([NotNull] string key, [NotNull] ITransactionApi api)
    {
        CheckHelper.NotNullOrWhiteSpace(key, nameof(key));
        CheckHelper.NotNull(api, nameof(api));

        if (_transactionApis.ContainsKey(key))
        {
            throw new XiHanException("这个工作单元已经包含了给定键的事务API。");
        }

        _transactionApis.Add(key, api);
    }

    /// <summary>
    /// 获取或添加事务API
    /// </summary>
    /// <param name="key"></param>
    /// <param name="factory"></param>
    /// <returns></returns>
    public virtual ITransactionApi GetOrAddTransactionApi([NotNull] string key, [NotNull] Func<ITransactionApi> factory)
    {
        CheckHelper.NotNullOrWhiteSpace(key, nameof(key));
        CheckHelper.NotNull(factory, nameof(factory));

        return _transactionApis.GetOrAdd(key, factory);
    }

    /// <summary>
    /// 添加完成处理程序
    /// </summary>
    /// <param name="handler"></param>
    public virtual void OnCompleted(Func<Task> handler)
    {
        CompletedHandlers.Add(handler);
    }

    /// <summary>
    /// 添加本地事件
    /// </summary>
    /// <param name="eventRecord"></param>
    /// <param name="replacementSelector"></param>
    public virtual void AddOrReplaceLocalEvent(
        UnitOfWorkEventRecord eventRecord,
        Predicate<UnitOfWorkEventRecord>? replacementSelector = null)
    {
        LocalEventWithPredicates.Add(new KeyValuePair<UnitOfWorkEventRecord, Predicate<UnitOfWorkEventRecord>?>(eventRecord, replacementSelector));
    }

    /// <summary>
    /// 添加分布式事件
    /// </summary>
    /// <param name="eventRecord"></param>
    /// <param name="replacementSelector"></param>
    public virtual void AddOrReplaceDistributedEvent(
        UnitOfWorkEventRecord eventRecord,
        Predicate<UnitOfWorkEventRecord>? replacementSelector = null)
    {
        DistributedEventWithPredicates.Add(new KeyValuePair<UnitOfWorkEventRecord, Predicate<UnitOfWorkEventRecord>?>(eventRecord, replacementSelector));
    }

    /// <summary>
    /// 释放
    /// </summary>
    public virtual void Dispose()
    {
        if (IsDisposed)
        {
            return;
        }

        IsDisposed = true;

        DisposeTransactions();

        if (!IsCompleted || _exception != null)
        {
            OnFailed();
        }

        OnDisposed();

        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// 转换为字符串
    /// </summary>
    /// <returns></returns>
    public override string ToString()
    {
        return $"[UnitOfWork {Id}]";
    }

    /// <summary>
    /// 获取事件记录
    /// </summary>
    /// <param name="eventWithPredicates"></param>
    /// <returns></returns>
    protected virtual List<UnitOfWorkEventRecord> GetEventsRecords(List<KeyValuePair<UnitOfWorkEventRecord, Predicate<UnitOfWorkEventRecord>?>> eventWithPredicates)
    {
        var eventRecords = new List<UnitOfWorkEventRecord>();
        foreach (var eventWithPredicate in eventWithPredicates)
        {
            var eventRecord = eventWithPredicate.Key;
            var replacementSelector = eventWithPredicate.Value;

            if (replacementSelector == null)
            {
                eventRecords.Add(eventRecord);
            }
            else
            {
                var foundIndex = eventRecords.FindIndex(replacementSelector);
                if (foundIndex < 0)
                {
                    eventRecords.Add(eventRecord);
                }
                else
                {
                    eventRecord.SetOrder(eventRecords[foundIndex].EventOrder);
                    eventRecords[foundIndex] = eventRecord;
                }
            }
        }

        return eventRecords;
    }

    /// <summary>
    /// 完成异步
    /// </summary>
    /// <returns></returns>
    protected virtual async Task OnCompletedAsync()
    {
        foreach (var handler in CompletedHandlers)
        {
            await handler.Invoke();
        }
    }

    /// <summary>
    /// 失败
    /// </summary>
    protected virtual void OnFailed()
    {
        Failed.InvokeSafely(this, new UnitOfWorkFailedEventArgs(this, _exception, _isRolledback));
    }

    /// <summary>
    /// 释放
    /// </summary>
    protected virtual void OnDisposed()
    {
        Disposed.InvokeSafely(this, new UnitOfWorkEventArgs(this));
    }

    /// <summary>
    /// 回滚所有异步
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    protected virtual async Task RollbackAllAsync(CancellationToken cancellationToken)
    {
        foreach (var databaseApi in GetAllActiveDatabaseApis())
        {
            if (databaseApi is ISupportsRollback supportsRollbackDatabaseApi)
            {
                try
                {
                    await supportsRollbackDatabaseApi.RollbackAsync(cancellationToken);
                }
                catch { }
            }
        }

        foreach (var transactionApi in GetAllActiveTransactionApis())
        {
            if (transactionApi is ISupportsRollback supportsRollbackTransactionApi)
            {
                try
                {
                    await supportsRollbackTransactionApi.RollbackAsync(cancellationToken);
                }
                catch { }
            }
        }
    }

    /// <summary>
    /// 提交所有事务异步
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    protected virtual async Task CommitTransactionsAsync(CancellationToken cancellationToken)
    {
        foreach (var transaction in GetAllActiveTransactionApis())
        {
            await transaction.CommitAsync(cancellationToken);
        }
    }

    /// <summary>
    /// 释放事务
    /// </summary>
    private void DisposeTransactions()
    {
        foreach (var transactionApi in GetAllActiveTransactionApis())
        {
            try
            {
                transactionApi.Dispose();
            }
            catch
            {
            }
        }
    }

    /// <summary>
    /// 防止多次完成
    /// </summary>
    /// <exception cref="XiHanException"></exception>
    private void PreventMultipleComplete()
    {
        if (IsCompleted || _isCompleting)
        {
            throw new XiHanException("已经要求完成这个工作单元。");
        }
    }
}
