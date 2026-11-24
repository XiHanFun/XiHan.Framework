// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Diagnostics;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Testing;

/// <summary>
/// 监听给定的 ActivitySource、把 Span 收进内存的收集器
/// </summary>
/// <remarks>
/// <para>
/// 创建时登记一个 <see cref="ActivityListener"/>，只听名字在 <c>sourceNames</c> 里的源，释放时移除。收三样：开始的 Span（<see cref="Started"/>）、结束的 Span（<see cref="Stopped"/>）与采样器被询问的创建（<see cref="SamplingRequests"/>），每样最多留 <see cref="Capacity"/> 条，满了丢掉最早的；可以在多个线程上同时写入与读取。
/// </para>
/// <para>
/// 监听器是进程级的：同一进程里并行的测试经过同一个源时，它们的 Span 也收在这里，用 <see cref="Spans(string)"/> 按 trace-id 只取自己那条链路的。源的采样结果取全部监听器里最宽的那个，测试要靠采样结果断言时，与别的挂监听器的测试放进同一个不并行的集合
/// </para>
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class SpanCapture : IDisposable
{
    /// <summary>
    /// 缺省的条数上限
    /// </summary>
    public const int DefaultCapacity = 10_000;

    /// <summary>
    /// 保护三个队列与 <see cref="_disposed"/> 的锁
    /// </summary>
    private readonly Lock _lock = new();

    /// <summary>
    /// 开始的 Span，按开始顺序
    /// </summary>
    private readonly Queue<Activity> _started = new();

    /// <summary>
    /// 结束的 Span，按结束顺序
    /// </summary>
    private readonly Queue<Activity> _stopped = new();

    /// <summary>
    /// 采样器被询问的创建，按询问顺序
    /// </summary>
    private readonly Queue<SpanSamplingRequest> _requests = new();

    /// <summary>
    /// 要听的源的名字
    /// </summary>
    private readonly HashSet<string> _sources;

    /// <summary>
    /// 登记在 <see cref="ActivitySource"/> 上的监听器
    /// </summary>
    private readonly ActivityListener _listener;

    /// <summary>
    /// 是否已经释放
    /// </summary>
    private bool _disposed;

    /// <summary>
    /// 以缺省的条数上限开始监听
    /// </summary>
    /// <param name="sourceNames">要听的源的名字，按序数比较，至少一个</param>
    public SpanCapture(IEnumerable<string> sourceNames)
        : this(sourceNames, DefaultCapacity)
    {
    }

    /// <summary>
    /// 以给定的条数上限开始监听
    /// </summary>
    /// <param name="sourceNames">要听的源的名字，按序数比较，至少一个</param>
    /// <param name="capacity">每样最多留多少条，必须为正</param>
    public SpanCapture(IEnumerable<string> sourceNames, int capacity)
    {
        ArgumentNullException.ThrowIfNull(sourceNames);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
        _sources = new HashSet<string>(StringComparer.Ordinal);
        foreach (var name in sourceNames)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(name, nameof(sourceNames));
            _sources.Add(name);
        }

        if (_sources.Count == 0)
        {
            throw new ArgumentException("At least one activity source name is required.", nameof(sourceNames));
        }

        Capacity = capacity;
        _listener = new ActivityListener
        {
            ShouldListenTo = source => _sources.Contains(source.Name),
            Sample = Sample,
            ActivityStarted = activity => Add(_started, activity),
            ActivityStopped = activity => Add(_stopped, activity),
        };
        ActivitySource.AddActivityListener(_listener);
    }

    /// <summary>
    /// 每样的条数上限
    /// </summary>
    public int Capacity { get; }

    /// <summary>
    /// 决定要不要创建、要不要记录 Span 的采样器
    /// </summary>
    /// <remarks>
    /// 每次询问时读取。缺省（<see langword="null"/>）同 OpenTelemetry 的 ParentBased(AlwaysOn)：有父级随父级的采样位，链路的根一律采样
    /// </remarks>
    public SampleActivity<ActivityContext>? Sampler { get; init; }

    /// <summary>
    /// 开始的 Span 的快照，按开始顺序
    /// </summary>
    /// <remarks>
    /// 只有采样器要创建的才有。每次读取都是一份新的副本
    /// </remarks>
    public IReadOnlyList<Activity> Started => Snapshot(_started);

    /// <summary>
    /// 结束的 Span 的快照，按结束顺序
    /// </summary>
    /// <remarks>
    /// 每次读取都是一份新的副本
    /// </remarks>
    public IReadOnlyList<Activity> Stopped => Snapshot(_stopped);

    /// <summary>
    /// 采样器被询问的创建的快照，按询问顺序
    /// </summary>
    /// <remarks>
    /// 不论采样结果如何都记下。每次读取都是一份新的副本
    /// </remarks>
    public IReadOnlyList<SpanSamplingRequest> SamplingRequests => Snapshot(_requests);

    /// <summary>
    /// 一条链路上已经结束的 Span，按结束顺序
    /// </summary>
    /// <param name="traceId">trace-id，32 个小写十六进制字符</param>
    public IReadOnlyList<Activity> Spans(string traceId)
    {
        ArgumentNullException.ThrowIfNull(traceId);
        return [.. Stopped.Where(activity => string.Equals(activity.TraceId.ToHexString(), traceId, StringComparison.Ordinal))];
    }

    /// <summary>
    /// 一条链路上已经结束的、给定名字的 Span，按结束顺序
    /// </summary>
    /// <param name="traceId">trace-id，32 个小写十六进制字符</param>
    /// <param name="name">Span 的名字（<see cref="Activity.OperationName"/>）</param>
    public IReadOnlyList<Activity> Spans(string traceId, string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return [.. Spans(traceId).Where(activity => string.Equals(activity.OperationName, name, StringComparison.Ordinal))];
    }

    /// <inheritdoc />
    /// <remarks>
    /// 移除监听器，此后不再收；已经收到的仍可读取
    /// </remarks>
    public void Dispose()
    {
        lock (_lock)
        {
            _disposed = true;
        }

        _listener.Dispose();
    }

    /// <summary>
    /// 链路的根一律采样、其余随父级的采样位
    /// </summary>
    private static ActivitySamplingResult ParentBased(in ActivityCreationOptions<ActivityContext> options)
        => options.Parent.TraceId == default || (options.Parent.TraceFlags & ActivityTraceFlags.Recorded) != 0
            ? ActivitySamplingResult.AllDataAndRecorded
            : ActivitySamplingResult.None;

    /// <summary>
    /// 监听器的采样回调：记下询问，再交给采样器
    /// </summary>
    private ActivitySamplingResult Sample(ref ActivityCreationOptions<ActivityContext> options)
    {
        Add(_requests, new SpanSamplingRequest
        {
            Source = options.Source.Name,
            Name = options.Name,
            Kind = options.Kind,
            TraceId = options.TraceId.ToHexString(),
            Tags = options.Tags is { } tags ? [.. tags] : [],
        });
        return Sampler is { } sampler ? sampler(ref options) : ParentBased(options);
    }

    /// <summary>
    /// 记下一条
    /// </summary>
    private void Add<T>(Queue<T> queue, T item)
    {
        lock (_lock)
        {
            // 释放之后正在途中的回调也不收：释放与写入在同一把锁下判定
            if (_disposed)
            {
                return;
            }

            // 先腾位置再放：条数任何时刻都不超过上限，丢的是最早的
            while (queue.Count >= Capacity)
            {
                queue.Dequeue();
            }

            queue.Enqueue(item);
        }
    }

    /// <summary>
    /// 一个队列的副本
    /// </summary>
    private IReadOnlyList<T> Snapshot<T>(Queue<T> queue)
    {
        lock (_lock)
        {
            return [.. queue];
        }
    }
}
