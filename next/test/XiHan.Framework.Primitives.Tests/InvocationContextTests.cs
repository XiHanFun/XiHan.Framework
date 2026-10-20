// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Primitives.Tests;

public sealed class InvocationContextTests
{
    private const string TraceId = "4bf92f3577b34da6a3ce929d0e0e4736";
    private const string SpanId = "00f067aa0ba902b7";

    private sealed record BaggageVectors(string[] WellFormed, string[] IllFormed, string[] Registered);

    private sealed class EmptyServices : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }

    private static InvocationContext Create(
        DateTimeOffset? deadline = null,
        IReadOnlyDictionary<string, string>? baggage = null,
        string traceId = TraceId,
        string spanId = SpanId) => new()
    {
        CancellationToken = CancellationToken.None,
        Deadline = deadline,
        TraceId = traceId,
        SpanId = spanId,
        Baggage = baggage ?? new Dictionary<string, string>(StringComparer.Ordinal),
        Services = new EmptyServices(),
    };

    [Fact]
    public void BaggageKeys_MatchConformanceVectors()
    {
        var vectors = ConformanceVectors.Load<BaggageVectors>("baggage-keys.json");

        Assert.All(vectors.WellFormed, static key => Assert.True(BaggageKeys.IsWellFormed(key), key));
        Assert.All(vectors.IllFormed, static key => Assert.False(BaggageKeys.IsWellFormed(key), key));
        Assert.Equal(vectors.Registered, BaggageKeys.Registered);
        Assert.All(vectors.Registered, static key => Assert.True(BaggageKeys.IsRegistered(key), key));
    }

    [Fact]
    public void TraceAndSpanId_MustBeW3CHex()
    {
        Assert.Throws<ArgumentException>(() => Create(traceId: "ABC"));
        Assert.Throws<ArgumentException>(() => Create(spanId: "00f067aa0ba902b"));
    }

    [Fact]
    public void Baggage_RejectsIllFormedKeys()
    {
        Assert.Throws<ArgumentException>(() => Create(baggage: new Dictionary<string, string>(StringComparer.Ordinal) { ["tenant"] = "t-1" }));
    }

    [Fact]
    public void WithBaggage_DerivesContextSharingItems()
    {
        var context = Create();
        context.Items["marker"] = 1;

        var derived = context.WithBaggage(BaggageKeys.Tenant, "t-42");

        Assert.True(derived.TryGetBaggage(BaggageKeys.Tenant, out var tenant));
        Assert.Equal("t-42", tenant);
        Assert.False(context.TryGetBaggage(BaggageKeys.Tenant, out _));
        Assert.Same(context.Items, derived.Items);
        Assert.Equal(TraceId, derived.TraceId);
        Assert.Throws<ArgumentException>(() => context.WithBaggage("tenant", "t-42"));
    }

    [Fact]
    public void WithCancellation_DerivesContextWithNewTokenSharingItems()
    {
        using var cancellation = new CancellationTokenSource();
        var context = Create(baggage: new Dictionary<string, string>(StringComparer.Ordinal) { [BaggageKeys.Tenant] = "t-1" });

        var derived = context.WithCancellation(cancellation.Token);

        Assert.Equal(cancellation.Token, derived.CancellationToken);
        Assert.Same(context.Items, derived.Items);
        Assert.Same(context.Baggage, derived.Baggage);
        Assert.Equal(TraceId, derived.TraceId);
    }

    [Fact]
    public void Deadline_DrivesExpiryAndRemainingTime()
    {
        var now = new DateTimeOffset(2026, 10, 21, 0, 0, 0, TimeSpan.Zero);
        var context = Create(deadline: now.AddSeconds(30));

        Assert.False(context.IsExpired(now));
        Assert.Equal(TimeSpan.FromSeconds(30), context.RemainingTime(now));
        Assert.True(context.IsExpired(now.AddSeconds(30)));
        Assert.Equal(TimeSpan.Zero, context.RemainingTime(now.AddMinutes(1)));
        Assert.Null(Create().RemainingTime(now));
        Assert.False(Create().IsExpired(now));
    }
}
