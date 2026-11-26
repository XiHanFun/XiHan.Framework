// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json;
using XiHan.Framework.Testing.Workspace;

namespace XiHan.Framework.Primitives.Tests.Invocation;

/// <summary>
/// 回放 Baggage 值约束的一致性向量
/// </summary>
/// <remarks>
/// <c>baggage-keys.json</c> 的 <c>values</c>：已登记键的值能否进入调用上下文（ADR-0204、ADR-0233、ADR-0235）；含孤立代理项的值写不进 JSON 字符串，以码元写在 <c>acceptableUtf16</c> 与 <c>unacceptableUtf16</c> 里（ADR-0321）
/// </remarks>
public sealed class BaggageValueVectorTests
{
    /// <summary>
    /// 向量文件的内容
    /// </summary>
    private static readonly BaggageVectors _vectors = SpecVectors.Read<BaggageVectors>("primitives", "baggage-keys.json");

    [Fact]
    public void IsAcceptableValue_MatchesConformanceVectors()
    {
        Assert.NotEmpty(_vectors.Values.Acceptable);
        Assert.All(_vectors.Values.Acceptable, value => Assert.True(BaggageKeys.IsAcceptableValue(value), value));
        Assert.All(_vectors.Values.Unacceptable, value => Assert.False(BaggageKeys.IsAcceptableValue(value), value));
    }

    [Fact]
    public void IsAcceptableValue_RejectsLoneSurrogates_WrittenAsCodeUnits()
    {
        // 以码元写出的不合法样例都真有孤立的代理项，合法的那条是平面以外的一个字符（ADR-0321）
        Assert.NotEmpty(_vectors.Values.UnacceptableUtf16);
        Assert.All(_vectors.Values.UnacceptableUtf16, static value =>
        {
            Assert.True(UnicodeText.HasLoneSurrogate(value.Input), value.Name);
            Assert.False(BaggageKeys.IsAcceptableValue(value.Input), value.Name);
        });
        Assert.NotEmpty(_vectors.Values.AcceptableUtf16);
        Assert.All(_vectors.Values.AcceptableUtf16, static value =>
        {
            Assert.Contains(value.Input, char.IsSurrogate);
            Assert.True(BaggageKeys.IsAcceptableValue(value.Input), value.Name);
        });
    }

    [Fact]
    public void WithBaggage_ThrowsForLoneSurrogates_ButKeepsTheIdempotencyKeyForTheIdempotencyStage()
    {
        var context = new InvocationContext { CancellationToken = CancellationToken.None, TraceId = "0af7651916cd43dd8448eb211c80319c", SpanId = "b7ad6b7169203331", Baggage = new Dictionary<string, string>(), Services = EmptyServices.Instance };

        // 两个只差在孤立代理项上的关联 ID 出进程时都编成 61 EF BF BD：程序内派生给出它是编程错误
        Assert.All(_vectors.Values.UnacceptableUtf16, value => Assert.Throws<ArgumentException>(() => context.WithBaggage(BaggageKeys.Correlation, value.Input)));

        // 幂等键照旧不按值约束拒绝，留给 Idempotency 阶段以校验错误报告
        Assert.Equal("k\uD800", context.WithBaggage(BaggageKeys.IdempotencyKey, "k\uD800").Baggage[BaggageKeys.IdempotencyKey]);
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
    /// 向量文件的对象模型
    /// </summary>
    /// <remarks>
    /// 本测试只回放 <c>values</c>，键的三项由 <see cref="InvocationContextTests"/> 按它的模型回放，这里原样留着
    /// </remarks>
    /// <param name="WellFormed">格式合法的键</param>
    /// <param name="IllFormed">格式非法的键</param>
    /// <param name="Registered">已登记的键</param>
    /// <param name="Values">可以与不可以进入上下文的值</param>
    private sealed record BaggageVectors(JsonElement WellFormed, JsonElement IllFormed, JsonElement Registered, ValueVectors Values);

    /// <summary>
    /// 可以与不可以进入上下文的值
    /// </summary>
    /// <param name="Acceptable">可以进入的值</param>
    /// <param name="Unacceptable">不可以进入的值</param>
    /// <param name="AcceptableUtf16">以码元写出的可以进入的值</param>
    /// <param name="UnacceptableUtf16">以码元写出的不可以进入的值（孤立的代理项）</param>
    private sealed record ValueVectors(string[] Acceptable, string[] Unacceptable, CodeUnitCase[] AcceptableUtf16, CodeUnitCase[] UnacceptableUtf16);

    /// <summary>
    /// 以 UTF-16 码元写出的一个值
    /// </summary>
    /// <param name="Name">样例的说明</param>
    /// <param name="Utf16">以四位十六进制写出的 UTF-16 码元</param>
    private sealed record CodeUnitCase(string Name, string[] Utf16)
    {
        /// <summary>
        /// 由码元组成的值
        /// </summary>
        public string Input => SpecVectors.FromCodeUnits(Utf16);
    }

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
