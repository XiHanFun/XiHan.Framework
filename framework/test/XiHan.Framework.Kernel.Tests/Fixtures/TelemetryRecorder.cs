// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using XiHan.Framework.Kernel.Diagnostics;

namespace XiHan.Framework.Kernel.Tests.Fixtures;

/// <summary>
/// 监听内核的诊断源，收集 Span 与指标
/// </summary>
/// <remarks>
/// 收集结束的 Span、采样器在创建时看到的标签与记下的指标。源与仪表是进程级的，同一程序集里并行的用例也在往里写：Span 按 trace-id 取，采样器看到的标签按标签值找，指标只收 <see cref="Attach"/> 之后在本用例的异步流里记下的（仪表的回调在记录的那个线程上同步执行，读得到流过来的 <see cref="AsyncLocal{T}"/>）。释放时移除监听器
/// </remarks>
internal sealed class TelemetryRecorder : IDisposable
{
    /// <summary>
    /// 当前异步流绑定的收集器
    /// </summary>
    private static readonly AsyncLocal<TelemetryRecorder?> _attached = new();

    /// <summary>
    /// Span 监听器
    /// </summary>
    private readonly ActivityListener _activities;

    /// <summary>
    /// 仪表监听器
    /// </summary>
    private readonly MeterListener _meters = new();

    /// <summary>
    /// 结束的 Span，按结束顺序
    /// </summary>
    private readonly ConcurrentQueue<Activity> _stopped = new();

    /// <summary>
    /// 采样器被询问时看到的（Span 名，标签）
    /// </summary>
    private readonly ConcurrentQueue<(string Name, KeyValuePair<string, object?>[] Tags)> _sampled = new();

    /// <summary>
    /// 本用例的异步流里记下的指标
    /// </summary>
    private readonly ConcurrentQueue<TelemetryMeasurement> _measurements = new();

    /// <summary>
    /// 开始监听
    /// </summary>
    /// <remarks>
    /// 采样规则同 OpenTelemetry 的 ParentBased：有父级随父级的采样位，链路的根一律采样
    /// </remarks>
    public TelemetryRecorder()
    {
        _activities = new ActivityListener
        {
            ShouldListenTo = static source => source.Name == FrameworkDiagnostics.SourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> options) =>
            {
                _sampled.Enqueue((options.Name, options.Tags?.ToArray() ?? []));
                var sampled = options.Parent.TraceId == default || (options.Parent.TraceFlags & ActivityTraceFlags.Recorded) != 0;
                return sampled ? ActivitySamplingResult.AllDataAndRecorded : ActivitySamplingResult.None;
            },
            ActivityStopped = _stopped.Enqueue,
        };
        ActivitySource.AddActivityListener(_activities);

        _meters.InstrumentPublished = static (instrument, listener) =>
        {
            if (instrument.Meter.Name == FrameworkDiagnostics.SourceName)
            {
                listener.EnableMeasurementEvents(instrument);
            }
        };
        _meters.SetMeasurementEventCallback<long>((instrument, value, tags, _) => Record(instrument, value, tags));
        _meters.SetMeasurementEventCallback<double>((instrument, value, tags, _) => Record(instrument, value, tags));
        _meters.Start();
    }

    /// <summary>
    /// 本用例的异步流里记下的指标，按记录顺序
    /// </summary>
    public IReadOnlyList<TelemetryMeasurement> Measurements => [.. _measurements];

    /// <summary>
    /// 把收集器绑到当前异步流：此后这条流里记下的指标才收
    /// </summary>
    public void Attach() => _attached.Value = this;

    /// <summary>
    /// 一条链路上已经结束的 Span，按结束顺序
    /// </summary>
    public IReadOnlyList<Activity> Spans(string traceId)
        => [.. _stopped.Where(activity => activity.TraceId.ToHexString() == traceId)];

    /// <summary>
    /// 采样器在创建某个名字的 Span 时看到过这个标签
    /// </summary>
    public bool SamplerSaw(string name, string tag, string value)
        => _sampled.Any(entry => entry.Name == name && entry.Tags.Any(pair => pair.Key == tag && (string?)pair.Value == value));

    /// <inheritdoc />
    public void Dispose()
    {
        _activities.Dispose();
        _meters.Dispose();
    }

    /// <summary>
    /// 仪表的回调：只收绑定了本收集器的异步流里的记录
    /// </summary>
    private void Record(Instrument instrument, double value, ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        if (ReferenceEquals(_attached.Value, this))
        {
            _measurements.Enqueue(new TelemetryMeasurement(instrument.Name, value, tags.ToArray().ToDictionary(static pair => pair.Key, static pair => pair.Value, StringComparer.Ordinal)));
        }
    }
}
