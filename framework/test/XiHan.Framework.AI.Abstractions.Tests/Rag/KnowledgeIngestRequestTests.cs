// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Reflection;
using System.Text.Json;
using XiHan.Framework.AI.Abstractions.Rag;

namespace XiHan.Framework.AI.Abstractions.Tests.Rag;

/// <summary>
/// 知识摄取请求测试
/// </summary>
/// <remarks>
/// 摄取请求跨进程传递（后台任务/队列），因此既要保证必填项在编译期与反序列化期都不可省，
/// 也要保证可选项的「未指定」语义（null=用默认）不被序列化环节改写成具体值。
/// </remarks>
public class KnowledgeIngestRequestTests
{
    /// <summary>
    /// 仅给必填项时，可选项保持未指定
    /// </summary>
    /// <remarks>TenantId 是不可空 long，默认 0 即「平台全局」，与其余可选项的 null 语义不同。</remarks>
    [Fact]
    public void Defaults_WhenOnlyRequiredMembersSet_LeaveOptionalsUnspecified()
    {
        var request = new KnowledgeIngestRequest
        {
            DocumentId = "doc-1",
            Text = "正文"
        };

        Assert.Equal("doc-1", request.DocumentId);
        Assert.Equal("正文", request.Text);
        Assert.Equal(0L, request.TenantId);
        Assert.Null(request.Title);
        Assert.Null(request.Source);
        Assert.Null(request.Provider);
        Assert.Null(request.Chunking);
    }

    /// <summary>
    /// 文档标识与正文是 required 成员
    /// </summary>
    /// <remarks>
    /// 缺了任一项，摄取出来的切片都无法溯源或无内容可嵌入；
    /// 用 required 而非运行期校验，是把这条约束提前到编译期。
    /// </remarks>
    [Theory]
    [InlineData(nameof(KnowledgeIngestRequest.DocumentId))]
    [InlineData(nameof(KnowledgeIngestRequest.Text))]
    public void RequiredMembers_AreMarkedRequired(string propertyName)
    {
        Assert.True(IsRequired(propertyName));
    }

    /// <summary>
    /// 溯源与切片配置是可选成员，不得被提升为必填
    /// </summary>
    /// <remarks>把可选项改成 required 会让所有既有构造点编译失败，属破坏性变更。</remarks>
    [Theory]
    [InlineData(nameof(KnowledgeIngestRequest.TenantId))]
    [InlineData(nameof(KnowledgeIngestRequest.Title))]
    [InlineData(nameof(KnowledgeIngestRequest.Source))]
    [InlineData(nameof(KnowledgeIngestRequest.Provider))]
    [InlineData(nameof(KnowledgeIngestRequest.Chunking))]
    public void OptionalMembers_AreNotMarkedRequired(string propertyName)
    {
        Assert.False(IsRequired(propertyName));
    }

    /// <summary>
    /// 所有属性均为 init-only，请求在传递途中不可被改写
    /// </summary>
    [Fact]
    public void Properties_AreAllInitOnly()
    {
        var mutable = typeof(KnowledgeIngestRequest)
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
        var source = new KnowledgeIngestRequest
        {
            DocumentId = "doc-42",
            Text = "曦寒框架的知识正文",
            TenantId = 1024,
            Title = "框架说明",
            Source = "docs/framework.md",
            Provider = "openai",
            Chunking = new ChunkingOptions { MaxChunkSize = 500, Overlap = 50 }
        };

        var json = JsonSerializer.Serialize(source, serializerOptions);
        var restored = JsonSerializer.Deserialize<KnowledgeIngestRequest>(json, serializerOptions)!;

        Assert.Equal("doc-42", restored.DocumentId);
        Assert.Equal("曦寒框架的知识正文", restored.Text);
        Assert.Equal(1024L, restored.TenantId);
        Assert.Equal("框架说明", restored.Title);
        Assert.Equal("docs/framework.md", restored.Source);
        Assert.Equal("openai", restored.Provider);
        Assert.NotNull(restored.Chunking);
        Assert.Equal(500, restored.Chunking!.MaxChunkSize);
        Assert.Equal(50, restored.Chunking.Overlap);
    }

    /// <summary>
    /// 平台全局请求往返后租户仍为 0
    /// </summary>
    [Fact]
    public void JsonRoundTrip_WithPlatformTenant_KeepsZeroTenant()
    {
        var source = new KnowledgeIngestRequest
        {
            DocumentId = "doc-0",
            Text = "平台级知识"
        };

        var restored = JsonSerializer.Deserialize<KnowledgeIngestRequest>(JsonSerializer.Serialize(source))!;

        Assert.Equal(0L, restored.TenantId);
        Assert.Null(restored.Chunking);
    }

    /// <summary>
    /// 反序列化缺失必填项时抛出 JsonException
    /// </summary>
    /// <param name="json">缺少某个必填项的报文</param>
    /// <remarks>
    /// 只断言异常类型不断言消息文本：该消息由 System.Text.Json 生成，措辞随运行时版本变化，
    /// 断言文本等于把测试绑死在 BCL 的实现细节上。
    /// </remarks>
    [Theory]
    [InlineData("{\"DocumentId\":\"doc-1\"}")]
    [InlineData("{\"Text\":\"正文\"}")]
    [InlineData("{}")]
    public void Deserialize_WhenRequiredMemberMissing_ThrowsJsonException(string json)
    {
        Assert.Throws<JsonException>(() =>
        {
            JsonSerializer.Deserialize<KnowledgeIngestRequest>(json);
        });
    }

    /// <summary>
    /// 类型为 sealed
    /// </summary>
    [Fact]
    public void Type_IsSealed()
    {
        Assert.True(typeof(KnowledgeIngestRequest).IsSealed);
    }

    /// <summary>
    /// 判断属性是否带 required 标记
    /// </summary>
    /// <param name="propertyName">属性名</param>
    /// <remarks>按特性名判断而非引用类型，避免依赖编译器实现细节所在的具体程序集。</remarks>
    private static bool IsRequired(string propertyName)
    {
        var property = typeof(KnowledgeIngestRequest).GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance)!;

        return property.GetCustomAttributes(false)
            .Any(attribute => attribute.GetType().Name == "RequiredMemberAttribute");
    }
}
