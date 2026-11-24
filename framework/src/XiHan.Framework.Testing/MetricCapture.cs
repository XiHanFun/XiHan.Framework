// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Diagnostics.Metrics;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Testing;

/// <summary>
/// 监听给定的 Meter、把测量值收进内存的收集器
/// </summary>
/// <remarks>
/// <para>
/// 创建时启动一个 <see cref="MeterListener"/>，只打开名字在 <c>meterNames</c> 里的 Meter 的仪表，释放时停止。各种数值类型的仪表都收，最多留 <see cref="Capacity"/> 条，满了丢掉最早的；可以在多个线程上同时写入与读取。
/// </para>
/// <para>
/// 仪表多是进程级的静态对象，同一进程里并行的测试记下的值也会到这里。调用 <see cref="RestrictToCurrentFlow"/> 之后只收当前异步流（及由它派生的流）里记下的：仪表的回调在记录的那个线程上同步执行，读得到流过来的 <see cref="AsyncLocal{T}"/>。不随异步流传过去的记录（如另起的后台线程、不带执行上下文的测试服务器）这时就收不到
/// </para>
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class MetricCapture : IDisposable
{
    /// <summary>
    /// 缺省的条数上限
    /// </summary>
    public const int DefaultCapacity = 10_000;

    /// <summary>
    /// 当前异步流绑定的收集器
    /// </summary>
    private static readonly AsyncLocal<FlowBinding?> _flow = new();

    /// <summary>
    /// 保护 <see cref="_entries"/> 与 <see cref="_disposed"/> 的锁
    /// </summary>
    private readonly Lock _lock = new();

    /// <summary>
    /// 收到的测量值，按记录顺序
    /// </summary>
    private readonly Queue<MetricCaptureEntry> _entries = new();

    /// <summary>
    /// 要听的 Meter 的名字
    /// </summary>
    private readonly HashSet<string> _meters;

    /// <summary>
    /// 仪表监听器
    /// </summary>
    private readonly MeterListener _listener = new();

    /// <summary>
    /// 是否只收绑定了本收集器的异步流里的记录
    /// </summary>
    private volatile bool _restricted;

    /// <summary>
    /// 是否已经释放
    /// </summary>
    private bool _disposed;

    /// <summary>
    /// 以缺省的条数上限开始监听
    /// </summary>
    /// <param name="meterNames">要听的 Meter 的名字，按序数比较，至少一个</param>
    public MetricCapture(IEnumerable<string> meterNames)
        : this(meterNames, DefaultCapacity)
    {
    }

    /// <summary>
    /// 以给定的条数上限开始监听
    /// </summary>
    /// <param name="meterNames">要听的 Meter 的名字，按序数比较，至少一个</param>
    /// <param name="capacity">最多留多少条，必须为正</param>
    public MetricCapture(IEnumerable<string> meterNames, int capacity)
    {
        ArgumentNullException.ThrowIfNull(meterNames);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
        _meters = new HashSet<string>(StringComparer.Ordinal);
        foreach (var name in meterNames)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(name, nameof(meterNames));
            _meters.Add(name);
        }

        if (_meters.Count == 0)
        {
            throw new ArgumentException("At least one meter name is required.", nameof(meterNames));
        }

        Capacity = capacity;
        _listener.InstrumentPublished = (instrument, listener) =>
        {
            if (_meters.Contains(instrument.Meter.Name))
            {
                listener.EnableMeasurementEvents(instrument);
            }
        };
        _listener.SetMeasurementEventCallback<byte>((instrument, value, tags, _) => Record(instrument, value, tags));
        _listener.SetMeasurementEventCallback<short>((instrument, value, tags, _) => Record(instrument, value, tags));
        _listener.SetMeasurementEventCallback<int>((instrument, value, tags, _) => Record(instrument, value, tags));
        _listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) => Record(instrument, value, tags));
        _listener.SetMeasurementEventCallback<float>((instrument, value, tags, _) => Record(instrument, value, tags));
        _listener.SetMeasurementEventCallback<double>((instrument, value, tags, _) => Record(instrument, value, tags));
        _listener.SetMeasurementEventCallback<decimal>((instrument, value, tags, _) => Record(instrument, (double)value, tags));
        _listener.Start();
    }

    /// <summary>
    /// 条数上限
    /// </summary>
    public int Capacity { get; }

    /// <summary>
    /// 收到的测量值的快照，按记录顺序
    /// </summary>
    /// <remarks>
    /// 每次读取都是一份新的副本，读到之后再记下的不在里面
    /// </remarks>
    public IReadOnlyList<MetricCaptureEntry> Entries
    {
        get
        {
            lock (_lock)
            {
                return [.. _entries];
            }
        }
    }

    /// <summary>
    /// 此后只收当前异步流里记下的测量值
    /// </summary>
    /// <remarks>
    /// 绑定随异步流传给它派生的流，不影响调用方之外的流；同一条流可以绑定几个收集器。调用之前收到的照样保留
    /// </remarks>
    public void RestrictToCurrentFlow()
    {
        _flow.Value = new FlowBinding(this, _flow.Value);
        _restricted = true;
    }

    /// <inheritdoc />
    /// <remarks>
    /// 停止监听，此后不再收；已经收到的仍可读取
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
    /// 仪表的回调：按异步流过滤后记下一条
    /// </summary>
    private void Record(Instrument instrument, double value, ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        if (_restricted && !FlowBinding.Binds(_flow.Value, this))
        {
            return;
        }

        // 同一个键给了几次时取最后一次：回调在被测组件的调用里执行，不能因为标签重复而抛出
        var copied = new Dictionary<string, object?>(tags.Length, StringComparer.Ordinal);
        foreach (var tag in tags)
        {
            copied[tag.Key] = tag.Value;
        }

        var entry = new MetricCaptureEntry { Meter = instrument.Meter.Name, Instrument = instrument.Name, Value = value, Tags = copied };
        lock (_lock)
        {
            // 释放之后正在途中的回调也不收：释放与写入在同一把锁下判定
            if (_disposed)
            {
                return;
            }

            // 先腾位置再放：条数任何时刻都不超过上限，丢的是最早的
            while (_entries.Count >= Capacity)
            {
                _entries.Dequeue();
            }

            _entries.Enqueue(entry);
        }
    }

    /// <summary>
    /// 一条异步流上绑定的收集器，后绑定的在前
    /// </summary>
    /// <param name="Capture">绑定的收集器</param>
    /// <param name="Next">更早绑定的</param>
    private sealed record FlowBinding(MetricCapture Capture, FlowBinding? Next)
    {
        /// <summary>
        /// 链上是否有给定的收集器
        /// </summary>
        public static bool Binds(FlowBinding? binding, MetricCapture capture)
        {
            for (var current = binding; current is not null; current = current.Next)
            {
                if (ReferenceEquals(current.Capture, capture))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
