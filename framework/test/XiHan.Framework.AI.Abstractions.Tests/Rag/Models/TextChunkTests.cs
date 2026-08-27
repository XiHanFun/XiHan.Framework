// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Reflection;
using System.Text.Json;
using XiHan.Framework.AI.Abstractions.Rag.Models;

namespace XiHan.Framework.AI.Abstractions.Tests;

/// <summary>
/// 入库前文本切片测试
/// </summary>
/// <remarks>
/// 切片是写入向量库的最小单元，DocumentId + Index 构成它在文档内的定位；
/// 缺任一项都无法按文档删除或重建，故两者与正文一并强制为 required。
/// </remarks>
public class TextChunkTests
{
    /// <summary>
    /// 仅给必填项时，溯源信息保持未指定、租户为平台全局
    /// </summary>
    [Fact]
    public void Defaults_WhenOnlyRequiredMembersSet_LeaveOptionalsUnspecified()
    {
        var chunk = new TextChunk
        {
            DocumentId = "doc-1",
            Index = 0,
            Text = "片段"
        };

        Assert.Equal("doc-1", chunk.DocumentId);
        Assert.Equal(0, chunk.Index);
        Assert.Equal("片段", chunk.Text);
        Assert.Equal(0L, chunk.TenantId);
        Assert.Null(chunk.Title);
        Assert.Null(chunk.Source);
    }

    /// <summary>
    /// 定位与内容三项是 required 成员
    /// </summary>
    [Theory]
    [InlineData(nameof(TextChunk.DocumentId))]
    [InlineData(nameof(TextChunk.Index))]
    [InlineData(nameof(TextChunk.Text))]
    public void RequiredMembers_AreMarkedRequired(string propertyName)
    {
        Assert.True(IsRequired(propertyName));
    }

    /// <summary>
    /// 租户与溯源信息是可选成员
    /// </summary>
    /// <remarks>TenantId 虽不可空，但不是 required：不写即 0，表示平台全局知识。</remarks>
    [Theory]
    [InlineData(nameof(TextChunk.TenantId))]
    [InlineData(nameof(TextChunk.Title))]
    [InlineData(nameof(TextChunk.Source))]
    public void OptionalMembers_AreNotMarkedRequired(string propertyName)
    {
        Assert.False(IsRequired(propertyName));
    }

    /// <summary>
    /// 切片序号从 0 起且允许递增
    /// </summary>
    /// <param name="index">切片序号</param>
    /// <remarks>序号是 int 而非无符号类型，本用例只覆盖合法区间，不去锁死实现是否校验负数。</remarks>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(9999)]
    public void Index_WithinDocument_AcceptsAscendingValues(int index)
    {
        var chunk = new TextChunk
        {
            DocumentId = "doc-1",
            Index = index,
            Text = "片段"
        };

        Assert.Equal(index, chunk.Index);
    }

    /// <summary>
    /// 所有属性均为 init-only，切片写入向量库前后内容一致
    /// </summary>
    [Fact]
    public void Properties_AreAllInitOnly()
    {
        var mutable = typeof(TextChunk)
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
        var source = new TextChunk
        {
            DocumentId = "doc-42",
            Index = 7,
            Text = "第七片内容",
            TenantId = 1024,
            Title = "框架说明",
            Source = "docs/framework.md"
        };

        var json = JsonSerializer.Serialize(source, serializerOptions);
        var restored = JsonSerializer.Deserialize<TextChunk>(json, serializerOptions)!;

        Assert.Equal("doc-42", restored.DocumentId);
        Assert.Equal(7, restored.Index);
        Assert.Equal("第七片内容", restored.Text);
        Assert.Equal(1024L, restored.TenantId);
        Assert.Equal("框架说明", restored.Title);
        Assert.Equal("docs/framework.md", restored.Source);
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
            JsonSerializer.Deserialize<TextChunk>(json);
        });
    }

    /// <summary>
    /// 类型为 sealed
    /// </summary>
    [Fact]
    public void Type_IsSealed()
    {
        Assert.True(typeof(TextChunk).IsSealed);
    }

    /// <summary>
    /// 判断属性是否带 required 标记
    /// </summary>
    /// <param name="propertyName">属性名</param>
    private static bool IsRequired(string propertyName)
    {
        var property = typeof(TextChunk).GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance)!;

        return property.GetCustomAttributes(false)
            .Any(attribute => attribute.GetType().Name == "RequiredMemberAttribute");
    }
}
