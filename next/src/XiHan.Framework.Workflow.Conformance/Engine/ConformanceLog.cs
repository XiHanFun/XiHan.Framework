// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Pipeline;
using XiHan.Framework.Messaging.Abstractions.Envelopes;
using XiHan.Framework.Messaging.Abstractions.Handlers;
using XiHan.Framework.Messaging.Abstractions.Publishing;
using XiHan.Framework.Primitives;
using XiHan.Framework.Workflow.Abstractions.Events;
using XiHan.Framework.Workflow.Abstractions.Serialization;

namespace XiHan.Framework.Workflow.Conformance.Engine;

/// <summary>
/// 套件的记录器（单例）：执行过的节点、补偿、结果与送达代理的事件。
/// </summary>
/// <remarks>
/// 事件在代理一侧记录：记录器登记成 <see cref="IMessageBroker"/>（ADR-0096 第六节）。宿主没装发件箱时，引擎经 <see cref="DirectPublisher"/> 直接发到这里（与真实宿主一样可能早于提交）；装了发件箱时，事件随事务写进发件箱，提交后才由投递器送到这里，回滚的不会送达——记录的时机就是「事件随事务提交」的语义。同一信封（按 ID）只记一次：发件箱至少一次投递，后台投递循环与夹具的主动投递可能把同一条送达两次。只有工作流事件（信封的 <c>Source</c> 是 <see cref="WorkflowTopics.Source"/>）按 <see cref="WorkflowEventData"/> 解析并记进 <see cref="Events"/>；别的发布端（调度、应用自己的事件）的消息照常确认成功、不解析，主题记进 <see cref="OtherTopics"/>（ADR-0096 修订）。只记录、不消费：宿主别的消息订阅在套件里收不到消息。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class ConformanceLog : IMessageBroker
{
    /// <summary>
    /// 保护列表的锁。
    /// </summary>
    private readonly Lock _lock = new();

    /// <summary>
    /// 执行过的节点：(实例 ID, 节点 ID, 尝试次数)。
    /// </summary>
    private readonly List<(string InstanceId, string NodeId, int Attempt)> _executed = [];

    /// <summary>
    /// 补偿过的节点 ID。
    /// </summary>
    private readonly List<string> _compensated = [];

    /// <summary>
    /// 结果处理器收到的结果。
    /// </summary>
    private readonly List<string> _outcomes = [];

    /// <summary>
    /// 发布过的事件：(主题, 数据)。
    /// </summary>
    private readonly List<(string Topic, WorkflowEventData Data)> _events = [];

    /// <summary>
    /// 送达代理的非工作流消息的主题。
    /// </summary>
    private readonly List<string> _otherTopics = [];

    /// <summary>
    /// 已记录的信封 ID。
    /// </summary>
    /// <remarks>
    /// 至少一次投递的重复送达按它去重。
    /// </remarks>
    private readonly HashSet<string> _envelopeIds = new(StringComparer.Ordinal);

    /// <summary>
    /// 活动执行时调用上下文里的来源。
    /// </summary>
    private readonly List<InvocationOrigin?> _origins = [];

    /// <summary>
    /// 执行过的节点。
    /// </summary>
    public IReadOnlyList<(string InstanceId, string NodeId, int Attempt)> Executed
    {
        get
        {
            lock (_lock)
            {
                return _executed.ToList();
            }
        }
    }

    /// <summary>
    /// 活动执行时调用上下文里的来源（RFC-0003），按执行顺序。
    /// </summary>
    /// <remarks>
    /// 没有来源记 <see langword="null"/>。
    /// </remarks>
    public IReadOnlyList<InvocationOrigin?> Origins
    {
        get
        {
            lock (_lock)
            {
                return _origins.ToList();
            }
        }
    }

    /// <summary>
    /// 补偿过的节点 ID。
    /// </summary>
    public IReadOnlyList<string> Compensated
    {
        get
        {
            lock (_lock)
            {
                return _compensated.ToList();
            }
        }
    }

    /// <summary>
    /// 结果处理器收到的结果。
    /// </summary>
    public IReadOnlyList<string> Outcomes
    {
        get
        {
            lock (_lock)
            {
                return _outcomes.ToList();
            }
        }
    }

    /// <summary>
    /// 送达代理的事件，按送达顺序。
    /// </summary>
    public IReadOnlyList<(string Topic, WorkflowEventData Data)> Events
    {
        get
        {
            lock (_lock)
            {
                return _events.ToList();
            }
        }
    }

    /// <summary>
    /// 送达代理的非工作流消息的主题，按送达顺序。
    /// </summary>
    /// <remarks>
    /// 这些消息照常确认成功，不当工作流事件解析；同一信封（按 ID）只记一次。
    /// </remarks>
    public IReadOnlyList<string> OtherTopics
    {
        get
        {
            lock (_lock)
            {
                return _otherTopics.ToList();
            }
        }
    }

    /// <summary>
    /// 某主题的事件。
    /// </summary>
    public IReadOnlyList<WorkflowEventData> EventsOf(string topic) => Events.Where(pending => string.Equals(pending.Topic, topic, StringComparison.Ordinal)).Select(static pending => pending.Data).ToList();

    /// <summary>
    /// 记一次执行。
    /// </summary>
    public void RecordExecution(string instanceId, string nodeId, int attempt)
    {
        lock (_lock)
        {
            _executed.Add((instanceId, nodeId, attempt));
        }
    }

    /// <summary>
    /// 记一次活动执行的调用来源。
    /// </summary>
    public void RecordOrigin(InvocationOrigin? origin)
    {
        lock (_lock)
        {
            _origins.Add(origin);
        }
    }

    /// <summary>
    /// 记一次补偿。
    /// </summary>
    public void RecordCompensation(string nodeId)
    {
        lock (_lock)
        {
            _compensated.Add(nodeId);
        }
    }

    /// <summary>
    /// 记一个结果。
    /// </summary>
    public void RecordOutcome(string outcome)
    {
        lock (_lock)
        {
            _outcomes.Add(outcome);
        }
    }

    /// <inheritdoc />
    public ValueTask<Result> PublishAsync(string topic, MessageEnvelope envelope, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        lock (_lock)
        {
            Record(topic, envelope);
        }

        return ValueTask.FromResult(Result.Success());
    }

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<Result>> PublishManyAsync(string topic, IReadOnlyList<MessageEnvelope> envelopes, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(envelopes);
        lock (_lock)
        {
            foreach (var envelope in envelopes)
            {
                Record(topic, envelope);
            }
        }

        return ValueTask.FromResult<IReadOnlyList<Result>>(envelopes.Select(static _ => Result.Success()).ToList());
    }

    /// <inheritdoc />
    /// <remarks>
    /// 只记录、不消费：订阅登记了也不会收到消息。
    /// </remarks>
    public Task StartAsync(IReadOnlyCollection<MessageSubscription> subscriptions, MessageDispatch dispatch, CancellationToken cancellationToken) => Task.CompletedTask;

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>
    /// 记一个信封（调用方持锁）。
    /// </summary>
    /// <remarks>
    /// 同一 ID 的信封只记第一次：发件箱是至少一次投递。只有工作流事件按 <see cref="WorkflowEventData"/> 解析：宿主里别的发布端（调度、应用自己的事件）也经这个代理发布，按工作流事件解析它们会抛出，调度的发布端随之记告警、发件箱的整批投递失败（ADR-0096 修订）。
    /// </remarks>
    private void Record(string topic, MessageEnvelope envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        if (!_envelopeIds.Add(envelope.Id))
        {
            return;
        }

        // 工作流事件由 WorkflowEventPublisher 发出，信封的来源固定是 /workflow；别的来源照常确认、不解析
        if (string.Equals(envelope.Source, WorkflowTopics.Source, StringComparison.Ordinal))
        {
            _events.Add((topic, WorkflowJson.DeserializeEvent(envelope.Data.Span)));
        }
        else
        {
            _otherTopics.Add(topic);
        }
    }
}
