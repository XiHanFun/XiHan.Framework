// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Logging.Abstractions;
using XiHan.Framework.Uow.Abstracts;
using XiHan.Framework.Uow.Options;

namespace XiHan.Framework.Uow.Tests;

/// <summary>
/// 分布式事件发布时机的测试
/// </summary>
/// <remarks>
/// 覆盖「分布式事件只在事务提交成功之后才投出去」这一契约：
/// 提交前入队会让回滚掉的事务照样把事件发出去，下游据此看到一份从未落库的数据。
/// 本地事件相反，必须在提交前发布——其处理器可能继续写库，写入要落在同一事务里。
/// </remarks>
public class DistributedEventPublishOrderTests
{
    /// <summary>
    /// 提交成功后分布式事件被发布
    /// </summary>
    [Fact]
    public async Task CompleteAsync_PublishesDistributedEventsAfterCommit()
    {
        var publisher = new RecordingEventPublisher();
        var unitOfWork = CreateUnitOfWork(publisher);
        unitOfWork.AddOrReplaceDistributedEvent(CreateEventRecord("order.created"));

        await unitOfWork.CompleteAsync(TestContext.Current.CancellationToken);

        Assert.Equal(["order.created"], publisher.PublishedDistributedEvents);
    }

    /// <summary>
    /// 回滚后不发布任何分布式事件
    /// </summary>
    [Fact]
    public async Task RollbackAsync_PublishesNoDistributedEvents()
    {
        var publisher = new RecordingEventPublisher();
        var unitOfWork = CreateUnitOfWork(publisher);
        unitOfWork.AddOrReplaceDistributedEvent(CreateEventRecord("order.created"));

        await unitOfWork.RollbackAsync(TestContext.Current.CancellationToken);

        Assert.Empty(publisher.PublishedDistributedEvents);
    }

    /// <summary>
    /// 提交失败时不得留下已发出的分布式事件
    /// </summary>
    /// <remarks>
    /// 这是幽灵事件的真实场景：事件在提交之前投出，随后提交失败并回滚，
    /// 下游据此看到一份从未落库的数据。
    /// </remarks>
    [Fact]
    public async Task CompleteAsync_WhenCommitFails_PublishesNoDistributedEvents()
    {
        var publisher = new RecordingEventPublisher();
        var unitOfWork = CreateUnitOfWork(publisher);
        unitOfWork.AddTransactionApi("failing", new FailingTransactionApi());
        unitOfWork.AddOrReplaceDistributedEvent(CreateEventRecord("order.created"));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => unitOfWork.CompleteAsync(TestContext.Current.CancellationToken));

        Assert.Empty(publisher.PublishedDistributedEvents);
    }

    /// <summary>
    /// 提交失败时本地事件仍已发布
    /// </summary>
    /// <remarks>
    /// 本地事件的处理器与业务写入同处一个事务，随事务一起回滚，
    /// 因此「已发布」在此是预期行为，用于把它与分布式事件的时机区分开。
    /// </remarks>
    [Fact]
    public async Task CompleteAsync_WhenCommitFails_LocalEventsWereStillPublished()
    {
        var publisher = new RecordingEventPublisher();
        var unitOfWork = CreateUnitOfWork(publisher);
        unitOfWork.AddTransactionApi("failing", new FailingTransactionApi());
        unitOfWork.AddOrReplaceLocalEvent(CreateEventRecord("order.local"));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => unitOfWork.CompleteAsync(TestContext.Current.CancellationToken));

        Assert.Equal(["order.local"], publisher.PublishedLocalEvents);
    }

    /// <summary>
    /// 回滚后再提交抛出，且仍未发布任何分布式事件
    /// </summary>
    [Fact]
    public async Task CompleteAfterRollback_PublishesNoDistributedEvents()
    {
        var publisher = new RecordingEventPublisher();
        var unitOfWork = CreateUnitOfWork(publisher);
        unitOfWork.AddOrReplaceDistributedEvent(CreateEventRecord("order.created"));
        await unitOfWork.RollbackAsync(TestContext.Current.CancellationToken);

        await Assert.ThrowsAnyAsync<Exception>(() => unitOfWork.CompleteAsync(TestContext.Current.CancellationToken));

        Assert.Empty(publisher.PublishedDistributedEvents);
    }

    /// <summary>
    /// 本地事件仍在提交前发布
    /// </summary>
    [Fact]
    public async Task CompleteAsync_PublishesLocalEventsBeforeCommit()
    {
        var publisher = new RecordingEventPublisher();
        var unitOfWork = CreateUnitOfWork(publisher);
        unitOfWork.AddOrReplaceLocalEvent(CreateEventRecord("order.local"));

        await unitOfWork.CompleteAsync(TestContext.Current.CancellationToken);

        Assert.Equal(["order.local"], publisher.PublishedLocalEvents);
    }

    /// <summary>
    /// 多个分布式事件按事件序发布
    /// </summary>
    [Fact]
    public async Task CompleteAsync_PublishesDistributedEventsInOrder()
    {
        var publisher = new RecordingEventPublisher();
        var unitOfWork = CreateUnitOfWork(publisher);
        unitOfWork.AddOrReplaceDistributedEvent(CreateEventRecord("first"));
        unitOfWork.AddOrReplaceDistributedEvent(CreateEventRecord("second"));

        await unitOfWork.CompleteAsync(TestContext.Current.CancellationToken);

        Assert.Equal(["first", "second"], publisher.PublishedDistributedEvents);
    }

    /// <summary>
    /// 构造事件记录，以事件数据承载可断言的名称
    /// </summary>
    /// <param name="name">事件名称</param>
    /// <returns>事件记录</returns>
    private static UnitOfWorkEventRecord CreateEventRecord(string name)
    {
        return new UnitOfWorkEventRecord(typeof(string), name, EventOrderGenerator.GetNext());
    }

    /// <summary>
    /// 创建一个已初始化的工作单元
    /// </summary>
    /// <param name="publisher">事件发布器</param>
    /// <returns>工作单元</returns>
    private static UnitOfWork CreateUnitOfWork(IUnitOfWorkEventPublisher publisher)
    {
        var unitOfWork = new UnitOfWork(
            new EmptyServiceProvider(),
            publisher,
            Microsoft.Extensions.Options.Options.Create(new XiHanUnitOfWorkDefaultOptions()),
            NullLogger<UnitOfWork>.Instance);

        unitOfWork.Initialize(new XiHanUnitOfWorkOptions());

        return unitOfWork;
    }
}

/// <summary>
/// 提交时抛出的事务 API
/// </summary>
/// <remarks>
/// 用于构造「循环已跑完、提交失败并回滚」这一幽灵事件的真实触发场景。
/// </remarks>
public sealed class FailingTransactionApi : ITransactionApi, ISupportsRollback
{
    /// <summary>
    /// 异步提交，始终抛出提交失败异常
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>不返回，始终抛出 <see cref="InvalidOperationException"/></returns>
    public Task CommitAsync(CancellationToken cancellationToken = default)
    {
        throw new InvalidOperationException("提交失败");
    }

    /// <summary>
    /// 回滚，直接返回已完成任务
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>已完成的任务</returns>
    public Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// 释放资源
    /// </summary>
    public void Dispose()
    {
    }
}

/// <summary>
/// 记录发布顺序的事件发布器
/// </summary>
public class RecordingEventPublisher : IUnitOfWorkEventPublisher
{
    private readonly List<string> _localEvents = [];
    private readonly List<string> _distributedEvents = [];

    /// <summary>
    /// 已发布的本地事件名称
    /// </summary>
    public IReadOnlyList<string> PublishedLocalEvents => _localEvents;

    /// <summary>
    /// 已发布的分布式事件名称
    /// </summary>
    public IReadOnlyList<string> PublishedDistributedEvents => _distributedEvents;

    /// <summary>
    /// 发布本地事件，把事件数据按顺序记入本地事件列表
    /// </summary>
    /// <param name="localEvents">待发布的本地事件记录</param>
    /// <returns>已完成的任务</returns>
    public Task PublishLocalEventsAsync(IEnumerable<UnitOfWorkEventRecord> localEvents)
    {
        _localEvents.AddRange(localEvents.Select(record => (string)record.EventData));

        return Task.CompletedTask;
    }

    /// <summary>
    /// 发布分布式事件，把事件数据按顺序记入分布式事件列表
    /// </summary>
    /// <param name="distributedEvents">待发布的分布式事件记录</param>
    /// <returns>已完成的任务</returns>
    public Task PublishDistributedEventsAsync(IEnumerable<UnitOfWorkEventRecord> distributedEvents)
    {
        _distributedEvents.AddRange(distributedEvents.Select(record => (string)record.EventData));

        return Task.CompletedTask;
    }
}
