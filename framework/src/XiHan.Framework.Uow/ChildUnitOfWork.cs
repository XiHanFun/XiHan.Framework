#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ChildUnitOfWork
// Guid:790e851b-ecc6-46a9-a2b4-3fb6c6192bb2
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/06/02 05:40:30
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Uow.Abstracts;
using XiHan.Framework.Uow.Options;
using XiHan.Framework.Utils.Diagnostics;

namespace XiHan.Framework.Uow;

/// <summary>
/// 子工作单元
/// 用于复用父工作单元的上下文与生命周期，通常在嵌套事务或作用域控制中使用，所有操作最终委托给父工作单元
/// </summary>
internal class ChildUnitOfWork : IUnitOfWork
{
    private readonly IUnitOfWork _parent;

    /// <summary>
    /// 构造函数，初始化子工作单元并监听父工作单元的事件
    /// </summary>
    /// <param name="parent">父工作单元实例</param>
    public ChildUnitOfWork(IUnitOfWork parent)
    {
        Guard.NotNull(parent, nameof(parent));

        _parent = parent;

        _parent.Failed += (sender, args) => { Failed.InvokeSafely(sender!, args); };
        _parent.Disposed += (sender, args) => { Disposed.InvokeSafely(sender!, args); };
    }

    /// <summary>
    /// 工作单元失败事件
    /// </summary>
    public event EventHandler<UnitOfWorkFailedEventArgs> Failed = default!;

    /// <summary>
    /// 工作单元释放事件
    /// </summary>
    public event EventHandler<UnitOfWorkEventArgs> Disposed = default!;

    /// <summary>
    /// 当前工作单元的唯一标识符(与父工作单元一致)
    /// </summary>
    public Guid Id => _parent.Id;

    /// <summary>
    /// 工作单元配置选项(与父工作单元共享)
    /// </summary>
    public IXiHanUnitOfWorkOptions Options => _parent.Options;

    /// <summary>
    /// 外层的工作单元(用于嵌套 UoW)
    /// </summary>
    public IUnitOfWork? Outer => _parent.Outer;

    /// <summary>
    /// 是否已保留当前工作单元
    /// </summary>
    public bool IsReserved => _parent.IsReserved;

    /// <summary>
    /// 是否已释放
    /// </summary>
    public bool IsDisposed => _parent.IsDisposed;

    /// <summary>
    /// 是否已完成
    /// </summary>
    public bool IsCompleted => _parent.IsCompleted;

    /// <summary>
    /// 当前工作单元的保留名(如果有)
    /// </summary>
    public string? ReservationName => _parent.ReservationName;

    /// <summary>
    /// 获取当前工作单元的服务提供器
    /// </summary>
    public IServiceProvider ServiceProvider => _parent.ServiceProvider;

    /// <summary>
    /// 当前工作单元的上下文字典，用于跨方法或模块传递共享数据
    /// </summary>
    public Dictionary<string, object> Items => _parent.Items;

    /// <summary>
    /// 设置当前工作单元的外部工作单元引用
    /// </summary>
    /// <param name="outer">外层工作单元</param>
    public void SetOuter(IUnitOfWork? outer)
    {
        _parent.SetOuter(outer);
    }

    /// <summary>
    /// 初始化当前工作单元(委托给父工作单元)
    /// </summary>
    public void Initialize(XiHanUnitOfWorkOptions options)
    {
        _parent.Initialize(options);
    }

    /// <summary>
    /// 保留当前工作单元，防止被重复创建或释放
    /// </summary>
    /// <param name="reservationName">保留标识</param>
    public void Reserve(string reservationName)
    {
        _parent.Reserve(reservationName);
    }

    /// <summary>
    /// 保存所有更改(由父工作单元执行)
    /// </summary>
    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return _parent.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// 标记完成(子工作单元不实际完成父事务，因此为空实现)
    /// </summary>
    public Task CompleteAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// 回滚事务(由父工作单元处理)
    /// </summary>
    public Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        return _parent.RollbackAsync(cancellationToken);
    }

    /// <summary>
    /// 注册一个在工作单元完成时执行的回调
    /// </summary>
    public void OnCompleted(Func<Task> handler)
    {
        _parent.OnCompleted(handler);
    }

    /// <summary>
    /// 添加或替换本地事件(通常用于领域事件发布)
    /// </summary>
    public void AddOrReplaceLocalEvent(UnitOfWorkEventRecord eventRecord, Predicate<UnitOfWorkEventRecord>? replacementSelector = null)
    {
        _parent.AddOrReplaceLocalEvent(eventRecord, replacementSelector);
    }

    /// <summary>
    /// 添加或替换分布式事件(用于集成事件等分布式场景)
    /// </summary>
    public void AddOrReplaceDistributedEvent(UnitOfWorkEventRecord eventRecord, Predicate<UnitOfWorkEventRecord>? replacementSelector = null)
    {
        _parent.AddOrReplaceDistributedEvent(eventRecord, replacementSelector);
    }

    /// <summary>
    /// 查找指定键的数据库 API 实现
    /// </summary>
    public IDatabaseApi? FindDatabaseApi(string key)
    {
        return _parent.FindDatabaseApi(key);
    }

    /// <summary>
    /// 添加一个数据库 API 实现
    /// </summary>
    public void AddDatabaseApi(string key, IDatabaseApi api)
    {
        _parent.AddDatabaseApi(key, api);
    }

    /// <summary>
    /// 获取或添加一个数据库 API
    /// </summary>
    public IDatabaseApi GetOrAddDatabaseApi(string key, Func<IDatabaseApi> factory)
    {
        return _parent.GetOrAddDatabaseApi(key, factory);
    }

    /// <summary>
    /// 查找指定键的事务 API
    /// </summary>
    public ITransactionApi? FindTransactionApi(string key)
    {
        return _parent.FindTransactionApi(key);
    }

    /// <summary>
    /// 添加事务 API
    /// </summary>
    public void AddTransactionApi(string key, ITransactionApi api)
    {
        _parent.AddTransactionApi(key, api);
    }

    /// <summary>
    /// 获取或添加事务 API
    /// </summary>
    public ITransactionApi GetOrAddTransactionApi(string key, Func<ITransactionApi> factory)
    {
        return _parent.GetOrAddTransactionApi(key, factory);
    }

    /// <summary>
    /// 释放资源(子工作单元不实际释放父对象，因此为空实现)
    /// </summary>
    public void Dispose()
    {
    }

    /// <summary>
    /// 返回工作单元的调试字符串
    /// </summary>
    public override string ToString()
    {
        return $"[UnitOfWork {Id}]";
    }
}
