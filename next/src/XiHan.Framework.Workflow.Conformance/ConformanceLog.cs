// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Messaging.Abstractions;
using XiHan.Framework.Primitives;
using XiHan.Framework.Workflow.Abstractions;

namespace XiHan.Framework.Workflow.Conformance;

/// <summary>
/// 套件的记录器（单例）：执行过的节点、补偿过的节点、处理过的结果、发布过的事件。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class ConformanceLog : IMessagePublisher
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
    /// 发布过的事件。
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
            _events.Add((topic, WorkflowJson.DeserializeEvent(envelope.Data.Span)));
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
                _events.Add((topic, WorkflowJson.DeserializeEvent(envelope.Data.Span)));
            }
        }

        return ValueTask.FromResult<IReadOnlyList<Result>>(envelopes.Select(static _ => Result.Success()).ToList());
    }
}
