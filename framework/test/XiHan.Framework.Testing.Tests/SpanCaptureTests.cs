// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Diagnostics;
using System.Globalization;

namespace XiHan.Framework.Testing.Tests;

/// <summary>
/// <see cref="SpanCapture"/> 的过滤、采样、条数上限、并发与释放
/// </summary>
/// <remarks>
/// 每个用例用名字唯一的源，并行的别的用例经过不了它
/// </remarks>
public sealed class SpanCaptureTests : IDisposable
{
    /// <summary>
    /// 本用例的源
    /// </summary>
    private readonly ActivitySource _source = new($"xihan.testing.tests.spans.{Guid.NewGuid():N}");

    /// <inheritdoc />
    public void Dispose() => _source.Dispose();

    [Fact]
    public void Spans_OfTheListenedSourcesOnly_AreCaptured_AsSnapshots()
    {
        using var other = new ActivitySource($"xihan.testing.tests.spans.{Guid.NewGuid():N}");
        using var spans = new SpanCapture([_source.Name]);

        using (_source.StartActivity("listened"))
        {
            Assert.Null(other.StartActivity("ignored"));
        }

        var stopped = spans.Stopped;
        using (_source.StartActivity("later"))
        {
        }

        Assert.Equal(["listened", "later"], spans.Started.Select(static s => s.OperationName));
        Assert.Equal(["listened", "later"], spans.Stopped.Select(static s => s.OperationName));
        Assert.Equal(["listened", "later"], spans.SamplingRequests.Select(static r => r.Name));
        Assert.Equal(["listened"], stopped.Select(static s => s.OperationName));
    }

    [Fact]
    public void Spans_OfATrace_AreItsStoppedSpans_InStopOrder()
    {
        using var spans = new SpanCapture([_source.Name]);
        string traceId;
        using (var root = _source.StartActivity("root"))
        {
            Assert.NotNull(root);
            traceId = root.TraceId.ToHexString();
            using (_source.StartActivity("child"))
            {
            }
        }

        using (_source.StartActivity("unrelated"))
        {
        }

        Assert.Equal(["child", "root"], spans.Spans(traceId).Select(static s => s.OperationName));
        Assert.Equal(["child"], spans.Spans(traceId, "child").Select(static s => s.OperationName));
        Assert.Empty(spans.Spans(ActivityTraceId.CreateRandom().ToHexString()));
        Assert.Equal(3, spans.Stopped.Count);
    }

    [Fact]
    public void DefaultSampler_SamplesTheRoot_AndFollowsTheParent()
    {
        using var spans = new SpanCapture([_source.Name]);
        var unsampled = new ActivityContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.None, isRemote: true);
        var sampled = new ActivityContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.Recorded, isRemote: true);

        using (var root = _source.StartActivity("root", ActivityKind.Internal, default(ActivityContext)))
        {
            Assert.True(root?.Recorded);
        }

        Assert.Null(_source.StartActivity("dropped", ActivityKind.Internal, unsampled));
        using (var kept = _source.StartActivity("kept", ActivityKind.Internal, sampled))
        {
            Assert.True(kept?.Recorded);
        }

        Assert.Equal(["root", "kept"], spans.Stopped.Select(static s => s.OperationName));
    }

    [Fact]
    public void Sampler_WhenGiven_DecidesEveryCreation()
    {
        var result = ActivitySamplingResult.PropagationData;
        using var spans = new SpanCapture([_source.Name]) { Sampler = (ref ActivityCreationOptions<ActivityContext> _) => result };

        using (var propagated = _source.StartActivity("propagated"))
        {
            Assert.NotNull(propagated);
            Assert.False(propagated.Recorded);
        }

        result = ActivitySamplingResult.None;
        Assert.Null(_source.StartActivity("refused"));

        // 采样器被问到的每一次都记下，不论它的回答
        Assert.Equal(["propagated", "refused"], spans.SamplingRequests.Select(static r => r.Name));
        Assert.Equal(["propagated"], spans.Started.Select(static s => s.OperationName));
    }

    [Fact]
    public void SamplingRequests_HoldWhatTheSamplerSawAtCreation()
    {
        using var spans = new SpanCapture([_source.Name]);
        var parent = new ActivityContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.Recorded, isRemote: true);

        using (var created = _source.StartActivity("created", ActivityKind.Client, parent, [new("given", "at creation")]))
        {
            created?.SetTag("later", "after creation");
        }

        var request = Assert.Single(spans.SamplingRequests);
        Assert.Equal((_source.Name, "created", ActivityKind.Client, parent.TraceId.ToHexString()), (request.Source, request.Name, request.Kind, request.TraceId));
        Assert.Equal([new KeyValuePair<string, object?>("given", "at creation")], request.Tags);

        // 链路的根：采样器看到的就是新 Span 用的 trace-id
        using (var root = _source.StartActivity("root", ActivityKind.Internal, default(ActivityContext)))
        {
            Assert.NotNull(root);
            Assert.Equal(root.TraceId.ToHexString(), spans.SamplingRequests[^1].TraceId);
        }
    }

    [Fact]
    public void Capacity_DropsTheOldest_OfEachKind()
    {
        using var spans = new SpanCapture([_source.Name], capacity: 2);

        for (var index = 1; index <= 4; index++)
        {
            using (_source.StartActivity($"span {index}"))
            {
            }
        }

        Assert.Equal(2, spans.Capacity);
        Assert.Equal(["span 3", "span 4"], spans.Started.Select(static s => s.OperationName));
        Assert.Equal(["span 3", "span 4"], spans.Stopped.Select(static s => s.OperationName));
        Assert.Equal(["span 3", "span 4"], spans.SamplingRequests.Select(static r => r.Name));
    }

    [Fact]
    public void Capacity_DefaultsToTenThousand_AndTheArgumentsAreChecked()
    {
        using var spans = new SpanCapture([_source.Name]);

        Assert.Equal(SpanCapture.DefaultCapacity, spans.Capacity);
        Assert.Equal(10_000, SpanCapture.DefaultCapacity);
        Assert.Throws<ArgumentOutOfRangeException>(() => new SpanCapture([_source.Name], 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SpanCapture([_source.Name], -1));
        Assert.Throws<ArgumentNullException>(() => new SpanCapture(null!));
        Assert.Throws<ArgumentException>(() => new SpanCapture([]));
        Assert.Throws<ArgumentException>(() => new SpanCapture([" "]));
        Assert.Throws<ArgumentNullException>(() => spans.Spans(null!));
        Assert.Throws<ArgumentNullException>(() => spans.Spans(ActivityTraceId.CreateRandom().ToHexString(), null!));
    }

    [Fact]
    public void Dispose_StopsListening_AndKeepsWhatWasCaptured()
    {
        var spans = new SpanCapture([_source.Name]);
        using (_source.StartActivity("before"))
        {
        }

        var open = _source.StartActivity("open");
        Assert.NotNull(open);
        spans.Dispose();
        open.Stop();

        Assert.False(_source.HasListeners());
        Assert.Null(_source.StartActivity("after"));
        Assert.Equal(["before", "open"], spans.Started.Select(static s => s.OperationName));
        Assert.Equal(["before"], spans.Stopped.Select(static s => s.OperationName));
    }

    [Fact]
    public async Task ConcurrentSpans_LoseNothing_AndNeverExceedTheCapacity()
    {
        const int Workers = 8;
        const int PerWorker = 250;
        using var bounded = new SpanCapture([_source.Name], capacity: 100);
        using var roomy = new SpanCapture([_source.Name]);
        using var stop = new CancellationTokenSource();

        // 一边写一边读快照：读到的条数任何时刻都不超过上限
        var reader = Task.Run(
            () =>
            {
                var largest = 0;
                while (!stop.IsCancellationRequested)
                {
                    largest = Math.Max(largest, bounded.Stopped.Count);
                }

                return largest;
            },
            TestContext.Current.CancellationToken);
        await Task.WhenAll(Enumerable.Range(0, Workers).Select(worker => Task.Run(
            () =>
            {
                for (var index = 0; index < PerWorker; index++)
                {
                    using var activity = _source.StartActivity(string.Create(CultureInfo.InvariantCulture, $"{worker}:{index}"), ActivityKind.Internal, default(ActivityContext));
                }
            },
            TestContext.Current.CancellationToken)));
        await stop.CancelAsync();

        Assert.True(await reader <= 100);
        Assert.Equal(100, bounded.Stopped.Count);
        // 上限够大时一个不丢，每个写者自己的顺序保持
        var stopped = roomy.Stopped;
        Assert.Equal(Workers * PerWorker, stopped.Count);
        Assert.All(
            stopped.Select(static s => s.OperationName.Split(':')).GroupBy(static parts => parts[0]),
            group => Assert.Equal(Enumerable.Range(0, PerWorker).Select(static i => i.ToString(CultureInfo.InvariantCulture)), group.Select(static parts => parts[1])));
    }
}
