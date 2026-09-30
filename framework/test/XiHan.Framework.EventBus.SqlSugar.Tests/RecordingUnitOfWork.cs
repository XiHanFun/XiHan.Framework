// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Uow;
using XiHan.Framework.Uow.Abstracts;
using XiHan.Framework.Uow.Options;

namespace XiHan.Framework.EventBus.SqlSugar.Tests;

/// <summary>
/// 测试替身：记录每次 Begin 的参数与所开工作单元的工作单元管理器
/// </summary>
internal sealed class RecordingUnitOfWorkManager : IUnitOfWorkManager
{
    /// <summary>
    /// 按调用顺序记录的 Begin 调用
    /// </summary>
    public List<(bool RequiresNew, bool IsTransactional, RecordingUnitOfWork UnitOfWork)> BeginCalls { get; } = [];

    /// <summary>
    /// 当前工作单元
    /// </summary>
    public IUnitOfWork? Current { get; set; }

    /// <summary>
    /// 开始一个新的工作单元并记录参数
    /// </summary>
    /// <param name="options">工作单元选项</param>
    /// <param name="requiresNew">是否要求新的工作单元</param>
    /// <returns>工作单元实例</returns>
    public IUnitOfWork Begin(XiHanUnitOfWorkOptions options, bool requiresNew = false)
    {
        var unitOfWork = new RecordingUnitOfWork(new ServiceCollection().BuildServiceProvider());
        BeginCalls.Add((requiresNew, options.IsTransactional, unitOfWork));

        return unitOfWork;
    }

    /// <summary>
    /// 预留一个工作单元，测试替身不实现该操作
    /// </summary>
    /// <param name="reservationName">预留名称</param>
    /// <param name="requiresNew">是否要求新的工作单元</param>
    /// <returns>工作单元实例</returns>
    public IUnitOfWork Reserve(string reservationName, bool requiresNew = false) => throw new NotSupportedException();

    /// <summary>
    /// 开始一个预留的工作单元，测试替身不实现该操作
    /// </summary>
    /// <param name="reservationName">预留名称</param>
    /// <param name="options">工作单元选项</param>
    public void BeginReserved(string reservationName, XiHanUnitOfWorkOptions options) => throw new NotSupportedException();

    /// <summary>
    /// 尝试开始一个预留的工作单元，测试替身不实现该操作
    /// </summary>
    /// <param name="reservationName">预留名称</param>
    /// <param name="options">工作单元选项</param>
    /// <returns>是否成功开始</returns>
    public bool TryBeginReserved(string reservationName, XiHanUnitOfWorkOptions options) => throw new NotSupportedException();
}

/// <summary>
/// 测试替身：不做任何持久化、只记录释放与完成状态的工作单元
/// </summary>
public sealed class RecordingUnitOfWork : IUnitOfWork
{
    private readonly Dictionary<string, IDatabaseApi> _databaseApis = [];
    private readonly Dictionary<string, ITransactionApi> _transactionApis = [];

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="serviceProvider">工作单元作用域的服务提供器</param>
    public RecordingUnitOfWork(IServiceProvider serviceProvider)
    {
        ServiceProvider = serviceProvider;
    }

    /// <summary>
    /// 工作单元失败事件，测试替身不触发
    /// </summary>
    public event EventHandler<UnitOfWorkFailedEventArgs> Failed
    {
        add { }
        remove { }
    }

    /// <summary>
    /// 工作单元释放事件，测试替身不触发
    /// </summary>
    public event EventHandler<UnitOfWorkEventArgs> Disposed
    {
        add { }
        remove { }
    }

    /// <summary>
    /// 服务提供器
    /// </summary>
    public IServiceProvider ServiceProvider { get; }

    /// <summary>
    /// 工作单元唯一标识
    /// </summary>
    public Guid Id { get; } = Guid.NewGuid();

    /// <summary>
    /// 工作单元项
    /// </summary>
    public Dictionary<string, object> Items { get; } = [];

    /// <summary>
    /// 工作单元选项
    /// </summary>
    public IXiHanUnitOfWorkOptions Options { get; set; } = new XiHanUnitOfWorkOptions();

    /// <summary>
    /// 外层工作单元
    /// </summary>
    public IUnitOfWork? Outer { get; private set; }

    /// <summary>
    /// 是否已预留
    /// </summary>
    public bool IsReserved { get; private set; }

    /// <summary>
    /// 是否已释放
    /// </summary>
    public bool IsDisposed { get; private set; }

    /// <summary>
    /// 是否已完成
    /// </summary>
    public bool IsCompleted { get; private set; }

    /// <summary>
    /// 是否已回滚
    /// </summary>
    public bool IsRolledback { get; private set; }

    /// <summary>
    /// 预留名称
    /// </summary>
    public string? ReservationName { get; private set; }

    /// <summary>
    /// 按登记顺序记录的本地事件
    /// </summary>
    public List<UnitOfWorkEventRecord> LocalEvents { get; } = [];

    /// <summary>
    /// 按登记顺序记录的分布式事件
    /// </summary>
    public List<UnitOfWorkEventRecord> DistributedEvents { get; } = [];

    /// <summary>
    /// 注册的完成回调
    /// </summary>
    public List<Func<Task>> CompletedHandlers { get; } = [];

    /// <summary>
    /// 设置外层工作单元
    /// </summary>
    /// <param name="outer">外层工作单元</param>
    public void SetOuter(IUnitOfWork? outer) => Outer = outer;

    /// <summary>
    /// 初始化，测试替身不做任何处理
    /// </summary>
    /// <param name="options">工作单元选项</param>
    public void Initialize(XiHanUnitOfWorkOptions options)
    {
    }

    /// <summary>
    /// 预留工作单元
    /// </summary>
    /// <param name="reservationName">预留名称</param>
    public void Reserve(string reservationName)
    {
        IsReserved = true;
        ReservationName = reservationName;
    }

    /// <summary>
    /// 保存更改，测试替身不做任何处理
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务</returns>
    public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    /// <summary>
    /// 完成工作单元
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务</returns>
    public Task CompleteAsync(CancellationToken cancellationToken = default)
    {
        IsCompleted = true;
        return Task.CompletedTask;
    }

    /// <summary>
    /// 回滚工作单元
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务</returns>
    public Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        IsRolledback = true;
        return Task.CompletedTask;
    }

    /// <summary>
    /// 注册完成回调
    /// </summary>
    /// <param name="handler">回调</param>
    public void OnCompleted(Func<Task> handler) => CompletedHandlers.Add(handler);

    /// <summary>
    /// 登记本地事件
    /// </summary>
    /// <param name="eventRecord">事件记录</param>
    /// <param name="replacementSelector">替换选择器</param>
    public void AddOrReplaceLocalEvent(UnitOfWorkEventRecord eventRecord, Predicate<UnitOfWorkEventRecord>? replacementSelector = null)
    {
        LocalEvents.Add(eventRecord);
    }

    /// <summary>
    /// 登记分布式事件
    /// </summary>
    /// <param name="eventRecord">事件记录</param>
    /// <param name="replacementSelector">替换选择器</param>
    public void AddOrReplaceDistributedEvent(UnitOfWorkEventRecord eventRecord, Predicate<UnitOfWorkEventRecord>? replacementSelector = null)
    {
        DistributedEvents.Add(eventRecord);
    }

    /// <summary>
    /// 查找数据库接口
    /// </summary>
    /// <param name="key">键</param>
    /// <returns>数据库接口</returns>
    public IDatabaseApi? FindDatabaseApi(string key) => _databaseApis.TryGetValue(key, out var api) ? api : null;

    /// <summary>
    /// 添加数据库接口
    /// </summary>
    /// <param name="key">键</param>
    /// <param name="api">数据库接口</param>
    public void AddDatabaseApi(string key, IDatabaseApi api) => _databaseApis[key] = api;

    /// <summary>
    /// 获取或添加数据库接口
    /// </summary>
    /// <param name="key">键</param>
    /// <param name="factory">工厂</param>
    /// <returns>数据库接口</returns>
    public IDatabaseApi GetOrAddDatabaseApi(string key, Func<IDatabaseApi> factory)
    {
        if (!_databaseApis.TryGetValue(key, out var api))
        {
            api = factory();
            _databaseApis[key] = api;
        }

        return api;
    }

    /// <summary>
    /// 查找事务接口
    /// </summary>
    /// <param name="key">键</param>
    /// <returns>事务接口</returns>
    public ITransactionApi? FindTransactionApi(string key) => _transactionApis.TryGetValue(key, out var api) ? api : null;

    /// <summary>
    /// 添加事务接口
    /// </summary>
    /// <param name="key">键</param>
    /// <param name="api">事务接口</param>
    public void AddTransactionApi(string key, ITransactionApi api) => _transactionApis[key] = api;

    /// <summary>
    /// 获取或添加事务接口
    /// </summary>
    /// <param name="key">键</param>
    /// <param name="factory">工厂</param>
    /// <returns>事务接口</returns>
    public ITransactionApi GetOrAddTransactionApi(string key, Func<ITransactionApi> factory)
    {
        if (!_transactionApis.TryGetValue(key, out var api))
        {
            api = factory();
            _transactionApis[key] = api;
        }

        return api;
    }

    /// <summary>
    /// 释放资源
    /// </summary>
    public void Dispose()
    {
        IsDisposed = true;
    }
}
