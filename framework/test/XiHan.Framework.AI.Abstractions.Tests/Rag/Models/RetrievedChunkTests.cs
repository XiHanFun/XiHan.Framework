// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Reflection;
using System.Text.Json;
using XiHan.Framework.AI.Abstractions.Rag.Models;

namespace XiHan.Framework.AI.Abstractions.Tests;

/// <summary>
/// 检索命中切片测试
/// </summary>
/// <remarks>
/// Score 是可空 double 且量纲随连接器而异（余弦可为负、内积可超 1），
/// 因此契约上只保证「原样透传」：任何在 DTO 层归一化或截断的行为都会破坏跨连接器的可比性。
/// </remarks>
public class RetrievedChunkTests
{
    /// <summary>
    /// 仅给必填项时，标题、来源与分数保持未指定
    /// </summary>
    /// <remarks>Score 为 null 表示连接器未返回分数，与「分数为 0」是两回事。</remarks>
    [Fact]
    public void Defaults_WhenOnlyRequiredMembersSet_LeaveOptionalsUnspecified()
    {
        var chunk = new RetrievedChunk
        {
            DocumentId = "doc-1",
            Index = 0,
            Text = "命中片段"
        };

        Assert.Equal("doc-1", chunk.DocumentId);
        Assert.Equal(0, chunk.Index);
        Assert.Equal("命中片段", chunk.Text);
        Assert.Null(chunk.Title);
        Assert.Null(chunk.Source);
        Assert.Null(chunk.Score);
    }

    /// <summary>
    /// 定位与内容三项是 required 成员
    /// </summary>
    /// <remarks>引用溯源要靠 DocumentId + Index 回指原文，缺了就无法在回答里给出可点开的出处。</remarks>
    [Theory]
    [InlineData(nameof(RetrievedChunk.DocumentId))]
    [InlineData(nameof(RetrievedChunk.Index))]
    [InlineData(nameof(RetrievedChunk.Text))]
    public void RequiredMembers_AreMarkedRequired(string propertyName)
    {
        Assert.True(IsRequired(propertyName));
    }

    /// <summary>
    /// 展示信息与分数是可选成员
    /// </summary>
    [Theory]
    [InlineData(nameof(RetrievedChunk.Title))]
    [InlineData(nameof(RetrievedChunk.Source))]
    [InlineData(nameof(RetrievedChunk.Score))]
    public void OptionalMembers_AreNotMarkedRequired(string propertyName)
    {
        Assert.False(IsRequired(propertyName));
    }

    /// <summary>
    /// 分数原样承载，含 0、负数与大于 1 的取值
    /// </summary>
    /// <param name="score">连接器返回的相似度分数</param>
    /// <remarks>
    /// 余弦相似度可落在 [-1,1]，内积/点积可超出 1，L2 距离越小越相近。
    /// DTO 不做任何归一化，这里逐一验证这些取值都能被原样承载。
    /// </remarks>
    [Theory]
    [InlineData(0d)]
    [InlineData(-1d)]
    [InlineData(-0.25d)]
    [InlineData(0.87d)]
    [InlineData(1d)]
    [InlineData(12.5d)]
    public void Score_WithAnyMagnitude_IsCarriedVerbatim(double score)
    {
        var chunk = new RetrievedChunk
        {
            DocumentId = "doc-1",
            Index = 0,
            Text = "命中片段",
            Score = score
        };

        double? expected = score;
        Assert.Equal(expected, chunk.Score);
    }

    /// <summary>
    /// 分数为 0 与分数缺失是两种不同状态
    /// </summary>
    /// <remarks>把「连接器没给分」当成 0 分会让排序把它压到末位，改变引用顺序。</remarks>
    [Fact]
    public void Score_WhenZero_IsDistinctFromAbsent()
    {
        var scored = new RetrievedChunk { DocumentId = "d", Index = 0, Text = "t", Score = 0d };
        var unscored = new RetrievedChunk { DocumentId = "d", Index = 0, Text = "t" };

        Assert.True(scored.Score.HasValue);
        Assert.False(unscored.Score.HasValue);
        Assert.NotEqual(unscored.Score, scored.Score);
    }

    /// <summary>
    /// 所有属性均为 init-only，检索结果不可被下游改写
    /// </summary>
    /// <remarks>结果对象会同时进入排序、增强提示与引用展示三条路径，可变会造成相互串扰。</remarks>
    [Fact]
    public void Properties_AreAllInitOnly()
    {
        var mutable = typeof(RetrievedChunk)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.SetMethod is not null)
            .Where(property => !property.SetMethod!.ReturnParameter
                .GetRequiredCustomModifiers()
                .Any(modifier => modifier.Name == "IsExternalInit"))
            .Select(property => property.Name)
            .ToArray();

        Assert.Empty(mutable);
    }

    /// <summary>
    /// 全字段可经 System.Text.Json 往返且值不丢失
    /// </summary>
    /// <param name="useWebNaming">true 用 Web 驼峰命名策略，false 用默认策略</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void JsonRoundTrip_WithEveryFieldSet_PreservesValues(bool useWebNaming)
    {
        JsonSerializerOptions? serializerOptions = useWebNaming ? new JsonSerializerOptions(JsonSerializerDefaults.Web) : null;
        var source = new RetrievedChunk
        {
            DocumentId = "doc-42",
            Index = 3,
            Text = "命中的第三片",
            Title = "框架说明",
            Source = "docs/framework.md",
            Score = -0.25d
        };

        var json = JsonSerializer.Serialize(source, serializerOptions);
        var restored = JsonSerializer.Deserialize<RetrievedChunk>(json, serializerOptions)!;

        Assert.Equal("doc-42", restored.DocumentId);
        Assert.Equal(3, restored.Index);
        Assert.Equal("命中的第三片", restored.Text);
        Assert.Equal("框架说明", restored.Title);
        Assert.Equal("docs/framework.md", restored.Source);

        double? expectedScore = -0.25d;
        Assert.Equal(expectedScore, restored.Score);
    }

    /// <summary>
    /// 未给分数的结果往返后仍无分数
    /// </summary>
    [Fact]
    public void JsonRoundTrip_WithoutScore_KeepsScoreNull()
    {
        var source = new RetrievedChunk { DocumentId = "d", Index = 0, Text = "t" };

        var restored = JsonSerializer.Deserialize<RetrievedChunk>(JsonSerializer.Serialize(source))!;

        Assert.Null(restored.Score);
        Assert.Null(restored.Title);
        Assert.Null(restored.Source);
    }

    /// <summary>
    /// 反序列化缺失必填项时抛出 JsonException
    /// </summary>
    /// <param name="json">缺少某个必填项的报文</param>
    [Theory]
    [InlineData("{\"DocumentId\":\"doc-1\",\"Index\":0}")]
    [InlineData("{\"DocumentId\":\"doc-1\",\"Text\":\"片段\"}")]
    [InlineData("{\"Index\":0,\"Text\":\"片段\"}")]
    public void Deserialize_WhenRequiredMemberMissing_ThrowsJsonException(string json)
    {
        Assert.Throws<JsonException>(() =>
        {
            JsonSerializer.Deserialize<RetrievedChunk>(json);
        });
    }

    /// <summary>
    /// 类型为 sealed
    /// </summary>
    [Fact]
    public void Type_IsSealed()
    {
        Assert.True(typeof(RetrievedChunk).IsSealed);
    }

    /// <summary>
    /// 判断属性是否带 required 标记
    /// </summary>
    /// <param name="propertyName">属性名</param>
    private static bool IsRequired(string propertyName)
    {
        var property = typeof(RetrievedChunk).GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance)!;

        return property.GetCustomAttributes(false)
            .Any(attribute => attribute.GetType().Name == "RequiredMemberAttribute");
    }
}
