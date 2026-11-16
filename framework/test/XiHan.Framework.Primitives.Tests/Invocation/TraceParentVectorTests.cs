// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Primitives.Tests.Invocation;

/// <summary>
/// 回放 <c>trace-context.json</c>：<see cref="TraceParent"/> 的解析与写出（RFC-0003 规则 7，ADR-0235）
/// </summary>
public sealed class TraceParentVectorTests
{
    /// <summary>
    /// 向量文件的内容
    /// </summary>
    private static readonly TraceVectors _vectors = ConformanceVectors.Load<TraceVectors>("trace-context.json");

    [Fact]
    public void TryParse_AcceptsTheValidTexts()
    {
        Assert.NotEmpty(_vectors.Parse.Valid);
        foreach (var vector in _vectors.Parse.Valid)
        {
            Assert.True(TraceParent.TryParse(vector.Text, out var parsed), vector.Text);
            Assert.Equal((vector.TraceId, vector.SpanId, vector.Sampled), (parsed.TraceId, parsed.SpanId, parsed.Sampled));
        }
    }

    [Fact]
    public void TryParse_RejectsTheInvalidTexts()
    {
        Assert.NotEmpty(_vectors.Parse.Invalid);
        Assert.All(_vectors.Parse.Invalid, text => Assert.False(TraceParent.TryParse(text, out _), text));
    }

    [Fact]
    public void ToString_WritesTheVectorText()
    {
        Assert.NotEmpty(_vectors.Format);

        // 没有标志（sampled 为 null）的上下文按已采样写出，由下游的采样器决定
        Assert.All(_vectors.Format, vector => Assert.Equal(vector.Text, new TraceParent(vector.TraceId, vector.SpanId, vector.Sampled ?? true).ToString()));
    }

    [Fact]
    public void Default_HasNoIdentifiers()
    {
        var value = default(TraceParent);
        Assert.Equal(string.Empty, value.ToString());
        Assert.Throws<InvalidOperationException>(() => value.TraceId);
    }

    /// <summary>
    /// 向量文件的对象模型
    /// </summary>
    private sealed record TraceVectors(ParseVectors Parse, FormatVector[] Format);

    /// <summary>
    /// 合法与不合法的文本
    /// </summary>
    private sealed record ParseVectors(ValidTrace[] Valid, string[] Invalid);

    /// <summary>
    /// 一条合法文本及其解析结果
    /// </summary>
    private sealed record ValidTrace(string Text, string TraceId, string SpanId, bool Sampled);

    /// <summary>
    /// 一次写出：<see cref="Sampled"/> 为 <see langword="null"/> 表示上下文没有标志
    /// </summary>
    private sealed record FormatVector(string TraceId, string SpanId, bool? Sampled, string Text);
}
