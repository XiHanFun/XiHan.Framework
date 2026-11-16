// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Primitives.Tests.Invocation;

/// <summary>
/// 回放 Baggage 值约束的一致性向量
/// </summary>
/// <remarks>
/// <c>baggage-keys.json</c> 的 <c>values</c>：已登记键的值能否进入调用上下文（ADR-0204、ADR-0233、ADR-0235）
/// </remarks>
public sealed class BaggageValueVectorTests
{
    /// <summary>
    /// 向量文件的内容
    /// </summary>
    private static readonly BaggageVectors _vectors = ConformanceVectors.Load<BaggageVectors>("baggage-keys.json");

    [Fact]
    public void IsAcceptableValue_MatchesConformanceVectors()
    {
        Assert.NotEmpty(_vectors.Values.Acceptable);
        Assert.All(_vectors.Values.Acceptable, value => Assert.True(BaggageKeys.IsAcceptableValue(value), value));
        Assert.All(_vectors.Values.Unacceptable, value => Assert.False(BaggageKeys.IsAcceptableValue(value), value));
    }

    [Fact]
    public void WithBaggage_RejectsUnacceptableValuesOfRegisteredKeys()
    {
        var context = new InvocationContext { CancellationToken = CancellationToken.None, TraceId = "0af7651916cd43dd8448eb211c80319c", SpanId = "b7ad6b7169203331", Baggage = new Dictionary<string, string>(), Services = EmptyServices.Instance };

        // 程序内逐项派生给出不合法的值是编程错误：抛出，不静默丢弃也不保留原值
        Assert.All(_vectors.Values.Unacceptable, value => Assert.Throws<ArgumentException>(() => context.WithBaggage(BaggageKeys.Correlation, value)));
        Assert.All(_vectors.Values.Acceptable, value => Assert.Equal(value, context.WithBaggage(BaggageKeys.Correlation, value).Baggage[BaggageKeys.Correlation]));

        // 幂等键不按这条拒绝，留给 Idempotency 阶段以校验错误报告；未登记的键不出进程，也不受约束
        Assert.Equal("line\nbreak", context.WithBaggage(BaggageKeys.IdempotencyKey, "line\nbreak").Baggage[BaggageKeys.IdempotencyKey]);
        Assert.Equal("line\nbreak", context.WithBaggage("xihan.local-note", "line\nbreak").Baggage["xihan.local-note"]);
    }

    /// <summary>
    /// 向量文件里本测试用到的部分
    /// </summary>
    private sealed record BaggageVectors(ValueVectors Values);

    /// <summary>
    /// 可以与不可以进入上下文的值
    /// </summary>
    private sealed record ValueVectors(string[] Acceptable, string[] Unacceptable);

    /// <summary>
    /// 不提供任何服务的服务提供器
    /// </summary>
    private sealed class EmptyServices : IServiceProvider
    {
        /// <summary>
        /// 共用的实例
        /// </summary>
        public static EmptyServices Instance { get; } = new();

        /// <inheritdoc />
        public object? GetService(Type serviceType) => null;
    }
}
