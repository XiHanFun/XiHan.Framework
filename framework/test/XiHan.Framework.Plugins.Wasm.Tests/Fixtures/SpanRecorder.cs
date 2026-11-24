// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Collections.Concurrent;
using System.Diagnostics;
using XiHan.Framework.Kernel.Diagnostics;

namespace XiHan.Framework.Plugins.Wasm.Tests.Fixtures;

/// <summary>
/// 监听内核的 ActivitySource，收集结束的 Span
/// </summary>
/// <remarks>
/// 源是进程级的，同一程序集里别的用例的调用也会经过它：Span 按 trace-id 取。采样规则同 OpenTelemetry 的 ParentBased：有父级随父级的采样位，链路的根一律采样。释放时移除监听器
/// </remarks>
internal sealed class SpanRecorder : IDisposable
{
    /// <summary>
    /// Span 监听器
    /// </summary>
    private readonly ActivityListener _listener;

    /// <summary>
    /// 结束的 Span，按结束顺序
    /// </summary>
    private readonly ConcurrentQueue<Activity> _stopped = new();

    /// <summary>
    /// 开始监听
    /// </summary>
    public SpanRecorder()
    {
        _listener = new ActivityListener
        {
            ShouldListenTo = static source => source.Name == FrameworkDiagnostics.SourceName,
            Sample = static (ref ActivityCreationOptions<ActivityContext> options) =>
                options.Parent.TraceId == default || (options.Parent.TraceFlags & ActivityTraceFlags.Recorded) != 0 ? ActivitySamplingResult.AllDataAndRecorded : ActivitySamplingResult.None,
            ActivityStopped = _stopped.Enqueue,
        };
        ActivitySource.AddActivityListener(_listener);
    }

    /// <summary>
    /// 一条链路上已经结束的 Span，按结束顺序
    /// </summary>
    public IReadOnlyList<Activity> Spans(string traceId)
        => [.. _stopped.Where(activity => activity.TraceId.ToHexString() == traceId)];

    /// <summary>
    /// 一条链路上已经结束的、给定名字的 Span，按结束顺序
    /// </summary>
    public IReadOnlyList<Activity> Spans(string traceId, string name)
        => [.. Spans(traceId).Where(activity => activity.OperationName == name)];

    /// <inheritdoc />
    public void Dispose() => _listener.Dispose();
}
