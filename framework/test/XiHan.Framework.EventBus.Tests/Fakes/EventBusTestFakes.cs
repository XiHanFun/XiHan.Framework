// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Core.Tracing;
using XiHan.Framework.MultiTenancy.Abstractions;
using XiHan.Framework.Uow;
using XiHan.Framework.Uow.Abstracts;
using XiHan.Framework.Uow.Options;

namespace XiHan.Framework.EventBus.Tests.Fakes;

/// <summary>
/// 测试替身：可写入、可嵌套还原的租户上下文
/// </summary>
/// <remarks>
/// 既有 <c>StubCurrentTenant</c> 恒为空租户且 Change 不生效，无法验证「触发处理器前切换到事件所属租户」这一契约，
/// 故另起一个会真正记录并还原切换的替身。
/// </remarks>
public sealed class FakeCurrentTenant : ICurrentTenant
{
    private readonly Stack<long?> _previousIds = new();

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="id">初始租户唯一标识</param>
    public FakeCurrentTenant(long? id = null)
    {
        Id = id;
    }

    /// <summary>
    /// 当前租户是否可用
    /// </summary>
    public bool IsAvailable => Id.HasValue;

    /// <summary>
    /// 当前租户唯一标识
    /// </summary>
    public long? Id { get; private set; }

    /// <summary>
    /// 当前租户名称
    /// </summary>
    public string? Name { get; private set; }

    /// <summary>
    /// 按调用顺序记录每一次切换到的租户唯一标识
    /// </summary>
    public List<long?> ChangedIds { get; } = [];

    /// <summary>
    /// 临时切换租户上下文
    /// </summary>
    /// <param name="id">要切换到的租户唯一标识</param>
    /// <param name="name">租户名称</param>
    /// <returns>用于还原上下文的释放器</returns>
    public IDisposable Change(long? id, string? name = null)
    {
        ChangedIds.Add(id);
        _previousIds.Push(Id);
        Id = id;
        Name = name;

        return new TenantScope(this);
    }

    /// <summary>
    /// 还原上一层租户上下文
    /// </summary>
    private void Restore()
    {
        Id = _previousIds.Count > 0 ? _previousIds.Pop() : null;
    }

    private sealed class TenantScope : IDisposable
    {
        private readonly FakeCurrentTenant _owner;
        private bool _disposed;

        public TenantScope(FakeCurrentTenant owner)
        {
            _owner = owner;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _owner.Restore();
        }
    }
}

/// <summary>
/// 测试替身：可嵌套还原的关联标识提供器
/// </summary>
public sealed class FakeCorrelationIdProvider : ICorrelationIdProvider
{
    private readonly Stack<string?> _previousIds = new();

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="correlationId">初始关联标识</param>
    public FakeCorrelationIdProvider(string? correlationId = null)
    {
        Current = correlationId;
    }

    /// <summary>
    /// 当前关联标识
    /// </summary>
    public string? Current { get; private set; }

    /// <summary>
    /// 按调用顺序记录每一次切换到的关联标识
    /// </summary>
    public List<string?> ChangedIds { get; } = [];

    /// <summary>
    /// 获取当前关联标识
    /// </summary>
    /// <returns>关联标识</returns>
    public string? Get() => Current;

    /// <summary>
    /// 临时切换关联标识
    /// </summary>
    /// <param name="correlationId">关联标识</param>
    /// <returns>用于还原上下文的释放器</returns>
    public IDisposable Change(string? correlationId)
    {
        ChangedIds.Add(correlationId);
        _previousIds.Push(Current);
        Current = correlationId;

        return new CorrelationScope(this);
    }

    /// <summary>
    /// 还原上一层关联标识
    /// </summary>
    private void Restore()
    {
        Current = _previousIds.Count > 0 ? _previousIds.Pop() : null;
    }

    private sealed class CorrelationScope : IDisposable
    {
        private readonly FakeCorrelationIdProvider _owner;
        private bool _disposed;

        public CorrelationScope(FakeCorrelationIdProvider owner)
        {
            _owner = owner;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _owner.Restore();
        }
    }
}

/// <summary>
/// 测试替身：当前工作单元可写的工作单元管理器
/// </summary>
/// <remarks>
/// 既有 <c>StubUnitOfWorkManager</c> 的 Current 恒为 null，无法覆盖「有环境工作单元时延迟发布 / 走发件箱」两条分支。
/// </remarks>
public sealed class FakeUnitOfWorkManager : IUnitOfWorkManager
{
    /// <summary>
    /// 当前工作单元
    /// </summary>
    public IUnitOfWork? Current { get; set; }

    /// <summary>
    /// 开始一个新的工作单元，测试替身不实现该操作
    /// </summary>
    /// <param name="options">工作单元选项</param>
    /// <param name="requiresNew">是否要求新的工作单元</param>
    /// <returns>工作单元实例</returns>
    public IUnitOfWork Begin(XiHanUnitOfWorkOptions options, bool requiresNew = false) => throw new NotSupportedException();

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
/// 测试替身：只记录事件登记、不做任何持久化的工作单元
/// </summary>
public sealed class FakeUnitOfWork : IUnitOfWork
{
    private readonly Dictionary<string, IDatabaseApi> _databaseApis = [];
    private readonly Dictionary<string, ITransactionApi> _transactionApis = [];

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="serviceProvider">工作单元作用域的服务提供器</param>
    public FakeUnitOfWork(IServiceProvider serviceProvider)
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
    public IXiHanUnitOfWorkOptions Options { get; } = new XiHanUnitOfWorkOptions();

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
